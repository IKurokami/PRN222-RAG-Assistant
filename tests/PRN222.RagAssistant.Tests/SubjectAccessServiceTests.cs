using System.Security.Claims;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class SubjectAccessServiceTests
{
    [Fact]
    public async Task Admin_can_manage_any_existing_subject()
    {
        var subject = CreateSubject("PRJ301", isActive: false);
        var service = CreateService([subject]);
        var admin = CreatePrincipal(Guid.NewGuid(), AppRoles.Admin);

        Assert.True(await service.CanViewSubjectAsync(admin, subject.Id));
        Assert.True(await service.CanManageSubjectAsync(admin, subject.Id));
    }

    [Fact]
    public async Task SubjectLeader_can_manage_only_assigned_subjects()
    {
        var assigned = CreateSubject("PRN222", isActive: true);
        var unassigned = CreateSubject("SWT301", isActive: true);
        var leaderId = Guid.NewGuid();
        var service = CreateService(
            [assigned, unassigned],
            new Dictionary<Guid, IReadOnlySet<Guid>>
            {
                [leaderId] = new HashSet<Guid> { assigned.Id }
            });
        var leader = CreatePrincipal(leaderId, AppRoles.SubjectLeader);

        Assert.True(await service.CanManageSubjectAsync(leader, assigned.Id));
        Assert.False(await service.CanManageSubjectAsync(leader, unassigned.Id));
        Assert.True(await service.CanViewSubjectAsync(leader, unassigned.Id));
    }

    [Fact]
    public async Task Student_can_view_active_but_not_inactive_subjects()
    {
        var active = CreateSubject("PRN222", isActive: true);
        var inactive = CreateSubject("PRJ301", isActive: false);
        var service = CreateService([active, inactive]);
        var student = CreatePrincipal(Guid.NewGuid(), AppRoles.Student);

        Assert.True(await service.CanViewSubjectAsync(student, active.Id));
        Assert.False(await service.CanViewSubjectAsync(student, inactive.Id));
        Assert.False(await service.CanManageSubjectAsync(student, active.Id));
    }

    [Fact]
    public async Task SubjectLeader_can_view_and_manage_an_assigned_inactive_subject()
    {
        var inactive = CreateSubject("SWP391", isActive: false);
        var leaderId = Guid.NewGuid();
        var service = CreateService(
            [inactive],
            new Dictionary<Guid, IReadOnlySet<Guid>>
            {
                [leaderId] = new HashSet<Guid> { inactive.Id }
            });
        var leader = CreatePrincipal(leaderId, AppRoles.SubjectLeader);

        Assert.True(await service.CanViewSubjectAsync(leader, inactive.Id));
        Assert.True(await service.CanManageSubjectAsync(leader, inactive.Id));
    }

    [Fact]
    public async Task Accessible_subjects_are_filtered_by_role_and_activity()
    {
        var active = CreateSubject("PRN222", isActive: true);
        var inactiveAssigned = CreateSubject("PRJ301", isActive: false);
        var inactiveUnassigned = CreateSubject("SWT301", isActive: false);
        var leaderId = Guid.NewGuid();
        var service = CreateService(
            [active, inactiveAssigned, inactiveUnassigned],
            new Dictionary<Guid, IReadOnlySet<Guid>>
            {
                [leaderId] = new HashSet<Guid> { inactiveAssigned.Id }
            });

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

    [Fact]
    public async Task Principal_without_a_valid_user_id_cannot_use_SubjectLeader_assignments()
    {
        var subject = CreateSubject("PRN222", isActive: false);
        var repository = new FakeSubjectAccessRepository([subject]);
        var service = new SubjectAccessService(repository);
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(ClaimTypes.Role, AppRoles.SubjectLeader)],
            authenticationType: "TestAuth"));

        Assert.False(await service.CanManageSubjectAsync(principal, subject.Id));
    }

    private static SubjectAccessService CreateService(
        IReadOnlyList<Subject> subjects,
        IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>? assignments = null)
    {
        return new SubjectAccessService(new FakeSubjectAccessRepository(subjects, assignments));
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

    private sealed class FakeSubjectAccessRepository : ISubjectAccessRepository
    {
        private readonly IReadOnlyList<Subject> _subjects;
        private readonly IReadOnlyDictionary<Guid, IReadOnlySet<Guid>> _assignments;

        public FakeSubjectAccessRepository(
            IReadOnlyList<Subject> subjects,
            IReadOnlyDictionary<Guid, IReadOnlySet<Guid>>? assignments = null)
        {
            _subjects = subjects;
            _assignments = assignments ?? new Dictionary<Guid, IReadOnlySet<Guid>>();
        }

        public Task<IReadOnlyList<Subject>> GetSubjectsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_subjects);
        }

        public Task<Subject?> FindSubjectAsync(Guid subjectId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_subjects.FirstOrDefault(subject => subject.Id == subjectId));
        }

        public Task<IReadOnlySet<Guid>> GetAssignedSubjectIdsAsync(
            Guid userId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                _assignments.TryGetValue(userId, out var ids)
                    ? ids
                    : (IReadOnlySet<Guid>)new HashSet<Guid>());
        }
    }
}
