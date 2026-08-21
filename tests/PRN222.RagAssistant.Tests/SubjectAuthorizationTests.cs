using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Security;
using PRN222.RagAssistant.Pages.AdminSubjects;
using PRN222.RagAssistant.Pages.Subjects;

namespace PRN222.RagAssistant.Tests;

public sealed class SubjectAuthorizationTests
{
    [Fact]
    public async Task ManageSubjects_policy_requires_Admin_only()
    {
        await using var provider = BuildServices().BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync(AppPolicies.ManageSubjects);

        Assert.NotNull(policy);
        var roleRequirement = Assert.Single(policy!.Requirements.OfType<RolesAuthorizationRequirement>());
        var allowedRole = Assert.Single(roleRequirement.AllowedRoles);
        Assert.Equal(AppRoles.Admin, allowedRole);
    }

    [Fact]
    public async Task Admin_satisfies_ManageSubjects_policy()
    {
        await using var provider = BuildServices().BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.Admin),
            resource: null,
            AppPolicies.ManageSubjects);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SubjectLeader_does_not_satisfy_ManageSubjects_policy()
    {
        await using var provider = BuildServices().BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.SubjectLeader),
            resource: null,
            AppPolicies.ManageSubjects);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AdminSubjects_page_model_requires_ManageSubjects_policy()
    {
        var authorizeAttribute = Assert.Single(
            typeof(PRN222.RagAssistant.Pages.AdminSubjects.IndexModel)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AppPolicies.ManageSubjects, authorizeAttribute.Policy);
    }

    [Fact]
    public void AdminSubjects_Create_Edit_Leaders_require_ManageSubjects_policy()
    {
        var pageTypes = new[]
        {
            typeof(PRN222.RagAssistant.Pages.AdminSubjects.CreateModel),
            typeof(PRN222.RagAssistant.Pages.AdminSubjects.EditModel),
            typeof(PRN222.RagAssistant.Pages.AdminSubjects.LeadersModel)
        };

        foreach (var pageType in pageTypes)
        {
            var attributes = pageType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .ToList();

            Assert.NotEmpty(attributes);
            Assert.Contains(attributes, a => a.Policy == AppPolicies.ManageSubjects);
        }
    }

    [Fact]
    public void Subjects_Index_requires_authentication()
    {
        var authorizeAttribute = Assert.Single(
            typeof(PRN222.RagAssistant.Pages.Subjects.IndexModel)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Null(authorizeAttribute.Policy);
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
