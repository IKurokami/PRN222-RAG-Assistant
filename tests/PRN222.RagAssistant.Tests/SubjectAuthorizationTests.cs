using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222.RagAssistant.Controllers;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Security;

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
        Assert.Equal([AppRoles.Admin], roleRequirement.AllowedRoles);
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
    public void AdminSubjects_controller_requires_ManageSubjects_policy()
    {
        var authorizeAttribute = Assert.Single(
            typeof(AdminSubjectsController)
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>());

        Assert.Equal(AppPolicies.ManageSubjects, authorizeAttribute.Policy);
    }

    [Theory]
    [InlineData(nameof(AdminSubjectsController.Create))]
    [InlineData(nameof(AdminSubjectsController.Edit))]
    [InlineData(nameof(AdminSubjectsController.Leaders))]
    public void AdminSubjects_POST_actions_validate_anti_forgery_tokens(string actionName)
    {
        var postMethods = typeof(AdminSubjectsController)
            .GetMethods()
            .Where(method => method.Name == actionName)
            .Where(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Any())
            .ToList();

        Assert.NotEmpty(postMethods);
        Assert.All(postMethods, method => Assert.Contains(
            method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
            attribute => attribute is ValidateAntiForgeryTokenAttribute));
    }

    [Fact]
    public void Subject_catalog_requires_authentication()
    {
        var authorizeAttribute = Assert.Single(
            typeof(SubjectsController)
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
