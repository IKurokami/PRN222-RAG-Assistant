using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222.RagAssistant.Controllers;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class ChapterAuthorizationTests
{
    [Fact]
    public async Task ManageDocuments_policy_requires_SubjectLeader_role_only()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(AppPolicies.ManageDocuments);

        Assert.NotNull(policy);
        var roleRequirement = Assert.Single(policy!.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Contains(AppRoles.SubjectLeader, roleRequirement.AllowedRoles);
        Assert.DoesNotContain(AppRoles.Student, roleRequirement.AllowedRoles);
        Assert.Single(roleRequirement.AllowedRoles);
    }

    [Fact]
    public async Task SubjectLeader_satisfies_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.SubjectLeader),
            resource: null,
            AppPolicies.ManageDocuments);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task Student_does_not_satisfy_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.Student),
            resource: null,
            AppPolicies.ManageDocuments);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task Anonymous_user_does_not_satisfy_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.Anonymous(),
            resource: null,
            AppPolicies.ManageDocuments);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void Flow1_presentation_uses_MVC_controllers()
    {
        Assert.True(typeof(Controller).IsAssignableFrom(typeof(DocumentsController)));
        Assert.True(typeof(Controller).IsAssignableFrom(typeof(ChaptersController)));
    }

    [Fact]
    public void Flow1_does_not_keep_legacy_Razor_Page_types()
    {
        var legacyNamespacePrefixes = new[]
        {
            "PRN222.RagAssistant.Pages.Documents",
            "PRN222.RagAssistant.Pages.Chapters"
        };

        var legacyTypes = typeof(DocumentsController).Assembly
            .GetTypes()
            .Where(type => type.Namespace is not null
                           && legacyNamespacePrefixes.Any(prefix =>
                               type.Namespace == prefix
                               || type.Namespace.StartsWith($"{prefix}.", StringComparison.Ordinal)))
            .ToList();

        Assert.Empty(legacyTypes);
    }

    [Theory]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.Create))]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.Edit))]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.Delete))]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.DeleteConfirmed))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Upload))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Edit))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Delete))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Reindex))]
    public void Flow1_write_actions_have_ManageDocuments_authorization_attribute(Type controllerType, string actionName)
    {
        var methods = controllerType
            .GetMethods()
            .Where(method => method.Name == actionName)
            .ToList();

        Assert.NotEmpty(methods);
        Assert.All(methods, method =>
        {
            var authorizeAttributes = method
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>();

            Assert.Contains(authorizeAttributes, attribute => attribute.Policy == AppPolicies.ManageDocuments);
        });
    }

    [Theory]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Upload))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Edit))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Delete))]
    [InlineData(typeof(DocumentsController), nameof(DocumentsController.Reindex))]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.Create))]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.Edit))]
    [InlineData(typeof(ChaptersController), nameof(ChaptersController.DeleteConfirmed))]
    public void Flow1_POST_actions_validate_anti_forgery_tokens(Type controllerType, string actionName)
    {
        var postMethods = controllerType
            .GetMethods()
            .Where(method => method.Name == actionName)
            .Where(method => method.GetCustomAttributes(typeof(HttpPostAttribute), inherit: true).Any()
                          || method.GetCustomAttributes(typeof(ActionNameAttribute), inherit: true).Any())
            .ToList();

        Assert.NotEmpty(postMethods);
        Assert.All(postMethods, method => Assert.Contains(
            method.GetCustomAttributes(typeof(ValidateAntiForgeryTokenAttribute), inherit: true),
            attribute => attribute is ValidateAntiForgeryTokenAttribute));
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
