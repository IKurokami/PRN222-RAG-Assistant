using System.Security.Claims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class SubjectAccessServiceTests
{
    [Fact]
    public async Task Admin_can_manage_any_existing_subject()
    {
        await using var context = CreateContext();
        var subject = CreateSubject("PRJ301", isActive: false);
        context.Subjects.Add(subject);
        await context.SaveChangesAsync();

        var service = new SubjectAccessService(context);
        var admin = CreatePrincipal(Guid.NewGuid(), AppRoles.Admin);

        Assert.True(await service.CanViewSubjectAsync(admin, subject.Id));
        Assert.True(await service.CanManageSubjectAsync(admin, subject.Id));
    }

    [Fact]
    public async Task SubjectLeader_can_manage_only_assigned_subjects()
    {
        await using var context = CreateContext();
        var assigned = CreateSubject("PRN222", isActive: true);
        var unassigned = CreateSubject("SWT301", isActive: true);
        var leaderId = Guid.NewGuid();

        context.Subjects.AddRange(assigned, unassigned);
        context.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = leaderId,
            ClaimType = AppClaimTypes.ManagedSubject,
            ClaimValue = assigned.Id.ToString("D")
        });
        await context.SaveChangesAsync();

        var service = new SubjectAccessService(context);
        var leader = CreatePrincipal(leaderId, AppRoles.SubjectLeader);

        Assert.True(await service.CanManageSubjectAsync(leader, assigned.Id));
        Assert.False(await service.CanManageSubjectAsync(leader, unassigned.Id));
        Assert.True(await service.CanViewSubjectAsync(leader, unassigned.Id));
    }

    [Fact]
    public async Task Student_can_view_active_but_not_inactive_subjects()
    {
        await using var context = CreateContext();
        var active = CreateSubject("PRN222", isActive: true);
        var inactive = CreateSubject("PRJ301", isActive: false);
        context.Subjects.AddRange(active, inactive);
        await context.SaveChangesAsync();

        var service = new SubjectAccessService(context);
        var student = CreatePrincipal(Guid.NewGuid(), AppRoles.Student);

        Assert.True(await service.CanViewSubjectAsync(student, active.Id));
        Assert.False(await service.CanViewSubjectAsync(student, inactive.Id));
        Assert.False(await service.CanManageSubjectAsync(student, active.Id));
    }

    [Fact]
    public async Task SubjectLeader_can_view_and_manage_an_assigned_inactive_subject()
    {
        await using var context = CreateContext();
        var inactive = CreateSubject("SWP391", isActive: false);
        var leaderId = Guid.NewGuid();

        context.Subjects.Add(inactive);
        context.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = leaderId,
            ClaimType = AppClaimTypes.ManagedSubject,
            ClaimValue = inactive.Id.ToString("D")
        });
        await context.SaveChangesAsync();

        var service = new SubjectAccessService(context);
        var leader = CreatePrincipal(leaderId, AppRoles.SubjectLeader);

        Assert.True(await service.CanViewSubjectAsync(leader, inactive.Id));
        Assert.True(await service.CanManageSubjectAsync(leader, inactive.Id));
    }

    [Fact]
    public async Task Accessible_subjects_are_filtered_by_role_and_activity()
    {
        await using var context = CreateContext();
        var active = CreateSubject("PRN222", isActive: true);
        var inactiveAssigned = CreateSubject("PRJ301", isActive: false);
        var inactiveUnassigned = CreateSubject("SWT301", isActive: false);
        var leaderId = Guid.NewGuid();

        context.Subjects.AddRange(active, inactiveAssigned, inactiveUnassigned);
        context.UserClaims.Add(new IdentityUserClaim<Guid>
        {
            UserId = leaderId,
            ClaimType = AppClaimTypes.ManagedSubject,
            ClaimValue = inactiveAssigned.Id.ToString("D")
        });
        await context.SaveChangesAsync();

        var service = new SubjectAccessService(context);
        var leaderSubjects = await service.GetAccessibleSubjectsAsync(
            CreatePrincipal(leaderId, AppRoles.SubjectLeader));
        var studentSubjects = await service.GetAccessibleSubjectsAsync(
            CreatePrincipal(Guid.NewGuid(), AppRoles.Student));

        Assert.Contains(leaderSubjects, subject => subject.Id == active.Id);
        Assert.Contains(leaderSubjects, subject => subject.Id == inactiveAssigned.Id);
        Assert.DoesNotContain(leaderSubjects, subject => subject.Id == inactiveUnassigned.Id);
        Assert.Single(studentSubjects);
        Assert.Equal(active.Id, studentSubjects[0].Id);
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"subject-access-{Guid.NewGuid():N}")
            // The production model contains Pgvector.Vector, while these tests only touch
            // Subjects and Identity claims. The InMemory provider cannot validate that
            // provider-specific CLR type, so replace validation in this test-only context.
            .ReplaceService<IModelValidator, InMemoryPgvectorModelValidator>()
            .Options;

        return new ApplicationDbContext(options);
    }

    private static Subject CreateSubject(string code, bool isActive)
    {
        return new Subject
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = $"{code} test subject",
            IsActive = isActive
        };
    }

    private static ClaimsPrincipal CreatePrincipal(Guid userId, string role)
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString("D")),
                new Claim(ClaimTypes.Name, $"{role.ToLowerInvariant()}@test.local"),
                new Claim(ClaimTypes.Role, role)
            ],
            authenticationType: "TestAuth"));
    }

    private sealed class InMemoryPgvectorModelValidator : IModelValidator
    {
        public void Validate(
            IModel model,
            IDiagnosticsLogger<DbLoggerCategory.Model.Validation> logger)
        {
            // Intentionally empty for the narrow InMemory authorization test harness.
            // Production/Npgsql model validation remains covered by the normal build,
            // pending-model check, migrations, and PostgreSQL CI validation.
        }
    }
}
