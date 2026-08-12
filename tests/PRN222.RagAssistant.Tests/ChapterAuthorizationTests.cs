using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

/// <summary>
/// Tests that verify server-side authorization rules for Chapter Management write operations.
///
/// These tests confirm:
/// - <see cref="AppPolicies.ManageDocuments"/> is enforced on Chapter Create/Edit/Delete
/// - Only <see cref="AppRoles.SubjectLeader"/> satisfies the policy
/// - <see cref="AppRoles.Student"/> is explicitly excluded
/// - Attribute-level checks on PageModel classes match the expected policy
/// </summary>
public sealed class ChapterAuthorizationTests
{
    // ─── Policy Configuration ─────────────────────────────────────────────────

    [Fact]
    public async Task ManageDocuments_policy_requires_SubjectLeader_role_only()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(AppPolicies.ManageDocuments);

        Assert.NotNull(policy);

        var roleRequirement = Assert.Single(
            policy!.Requirements.OfType<RolesAuthorizationRequirement>());

        // SubjectLeader must be in the allowed list
        Assert.Contains(AppRoles.SubjectLeader, roleRequirement.AllowedRoles);
        // Student must NOT be allowed
        Assert.DoesNotContain(AppRoles.Student, roleRequirement.AllowedRoles);
        // No other roles should be added
        Assert.Single(roleRequirement.AllowedRoles);
    }

    [Fact]
    public async Task SubjectLeader_satisfies_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var user = TestPrincipals.WithRole(AppRoles.SubjectLeader);
        var result = await authService.AuthorizeAsync(user, resource: null, AppPolicies.ManageDocuments);

        Assert.True(result.Succeeded, "SubjectLeader should be authorized for ManageDocuments.");
    }

    [Fact]
    public async Task Student_does_not_satisfy_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var user = TestPrincipals.WithRole(AppRoles.Student);
        var result = await authService.AuthorizeAsync(user, resource: null, AppPolicies.ManageDocuments);

        Assert.False(result.Succeeded, "Student should NOT be authorized for ManageDocuments.");
    }

    [Fact]
    public async Task Anonymous_user_does_not_satisfy_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var user = TestPrincipals.Anonymous();
        var result = await authService.AuthorizeAsync(user, resource: null, AppPolicies.ManageDocuments);

        Assert.False(result.Succeeded, "Anonymous user should NOT be authorized for ManageDocuments.");
    }

    // ─── PageModel Attribute Verification ────────────────────────────────────
    // Verifies that Chapter write pages declare [Authorize(Policy = ManageDocuments)]
    // so authorization is enforced at the framework level, not just hidden in UI.

    [Fact]
    public void Chapter_Create_page_has_ManageDocuments_authorization_attribute()
    {
        AssertHasManageDocumentsAttribute(typeof(Pages.Chapters.CreateModel));
    }

    [Fact]
    public void Chapter_Edit_page_has_ManageDocuments_authorization_attribute()
    {
        AssertHasManageDocumentsAttribute(typeof(Pages.Chapters.EditModel));
    }

    [Fact]
    public void Chapter_Delete_page_has_ManageDocuments_authorization_attribute()
    {
        AssertHasManageDocumentsAttribute(typeof(Pages.Chapters.DeleteModel));
    }

    [Fact]
    public void Document_Upload_page_has_ManageDocuments_authorization_attribute()
    {
        AssertHasManageDocumentsAttribute(typeof(Pages.Documents.UploadModel));
    }

    [Fact]
    public void Document_Edit_page_has_ManageDocuments_authorization_attribute()
    {
        AssertHasManageDocumentsAttribute(typeof(Pages.Documents.EditModel));
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static void AssertHasManageDocumentsAttribute(Type pageModelType)
    {
        var authorizeAttrs = pageModelType
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        Assert.True(
            authorizeAttrs.Any(),
            $"{pageModelType.Name} must have at least one [Authorize] attribute.");

        var hasManageDocuments = authorizeAttrs
            .Any(a => a.Policy == AppPolicies.ManageDocuments);

        Assert.True(
            hasManageDocuments,
            $"{pageModelType.Name} must declare [Authorize(Policy = \"{AppPolicies.ManageDocuments}\")].");
    }

    private static IServiceCollection BuildServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=prn222_test;Username=postgres;Password=postgres",
                ["Rag:Ollama:BaseUrl"] = "http://localhost:11434"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);
        return services;
    }
}
