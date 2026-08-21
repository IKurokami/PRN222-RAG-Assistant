using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Security;
using PRN222.RagAssistant.Pages.Documents;
using PRN222.RagAssistant.Pages.Chapters;
using PRN222.RagAssistant.Pages.Subjects;
using PRN222.RagAssistant.Pages.Evaluation;
using AdminUsersPages = PRN222.RagAssistant.Pages.AdminUsers;
using AdminSubjectsPages = PRN222.RagAssistant.Pages.AdminSubjects;
using DocumentsPages = PRN222.RagAssistant.Pages.Documents;
using ChaptersPages = PRN222.RagAssistant.Pages.Chapters;

namespace PRN222.RagAssistant.Tests;

public sealed class ChapterAuthorizationTests
{
    [Fact]
    public async Task ManageDocuments_policy_requires_Admin_or_SubjectLeader()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(AppPolicies.ManageDocuments);

        Assert.NotNull(policy);
        var roleRequirement = Assert.Single(policy!.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Contains(AppRoles.Admin, roleRequirement.AllowedRoles);
        Assert.Contains(AppRoles.SubjectLeader, roleRequirement.AllowedRoles);
        Assert.DoesNotContain(AppRoles.Student, roleRequirement.AllowedRoles);
        Assert.Equal(2, roleRequirement.AllowedRoles.Count());
    }

    [Fact]
    public async Task Admin_satisfies_ManageDocuments_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.Admin),
            resource: null,
            AppPolicies.ManageDocuments);

        Assert.True(result.Succeeded);
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
    public async Task ManageUsers_policy_requires_Admin_only()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();

        var policy = await policyProvider.GetPolicyAsync(AppPolicies.ManageUsers);

        Assert.NotNull(policy);
        var roleRequirement = Assert.Single(policy!.Requirements.OfType<RolesAuthorizationRequirement>());
        Assert.Contains(AppRoles.Admin, roleRequirement.AllowedRoles);
        Assert.DoesNotContain(AppRoles.SubjectLeader, roleRequirement.AllowedRoles);
        Assert.DoesNotContain(AppRoles.Student, roleRequirement.AllowedRoles);
        Assert.Single(roleRequirement.AllowedRoles);
    }

    [Fact]
    public async Task Admin_satisfies_ManageUsers_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.Admin),
            resource: null,
            AppPolicies.ManageUsers);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public async Task SubjectLeader_does_not_satisfy_ManageUsers_policy()
    {
        var services = BuildServices();
        await using var provider = services.BuildServiceProvider();
        var authService = provider.GetRequiredService<IAuthorizationService>();

        var result = await authService.AuthorizeAsync(
            TestPrincipals.WithRole(AppRoles.SubjectLeader),
            resource: null,
            AppPolicies.ManageUsers);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AdminUsers_page_model_requires_ManageUsers_policy()
    {
        var attributes = typeof(AdminUsersPages.IndexModel)
            .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
            .Cast<AuthorizeAttribute>()
            .ToList();

        var authorizeAttribute = Assert.Single(attributes);
        Assert.Equal(AppPolicies.ManageUsers, authorizeAttribute.Policy);
    }

    [Fact]
    public void Application_role_catalog_contains_Admin_SubjectLeader_and_Student()
    {
        Assert.Equal(3, AppRoles.All.Length);
        Assert.Contains(AppRoles.Admin, AppRoles.All);
        Assert.Contains(AppRoles.SubjectLeader, AppRoles.All);
        Assert.Contains(AppRoles.Student, AppRoles.All);
    }

    [Fact]
    public void Razor_Pages_exist_for_Documents_module()
    {
        var documentPageTypes = new[]
        {
            typeof(DocumentsPages.IndexModel),
            typeof(DocumentsPages.DetailsModel),
            typeof(DocumentsPages.UploadModel),
            typeof(DocumentsPages.EditModel)
        };

        foreach (var pageType in documentPageTypes)
        {
            Assert.NotNull(pageType);
            Assert.True(pageType.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)));
        }
    }

    [Fact]
    public void Razor_Pages_exist_for_Chapters_module()
    {
        var chapterPageTypes = new[]
        {
            typeof(ChaptersPages.IndexModel),
            typeof(ChaptersPages.CreateModel),
            typeof(ChaptersPages.EditModel),
            typeof(ChaptersPages.DeleteModel)
        };

        foreach (var pageType in chapterPageTypes)
        {
            Assert.NotNull(pageType);
            Assert.True(pageType.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)));
        }
    }

    [Fact]
    public void Razor_Pages_exist_for_AdminSubjects_module()
    {
        var pageTypes = new[]
        {
            typeof(AdminSubjectsPages.IndexModel),
            typeof(AdminSubjectsPages.CreateModel),
            typeof(AdminSubjectsPages.EditModel),
            typeof(AdminSubjectsPages.LeadersModel)
        };

        foreach (var pageType in pageTypes)
        {
            Assert.NotNull(pageType);
            Assert.True(pageType.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)));
        }
    }

    [Fact]
    public void Razor_Pages_exist_for_AdminUsers_module()
    {
        var pageTypes = new[]
        {
            typeof(AdminUsersPages.IndexModel),
            typeof(AdminUsersPages.CreateModel),
            typeof(AdminUsersPages.EditModel)
        };

        foreach (var pageType in pageTypes)
        {
            Assert.NotNull(pageType);
            Assert.True(pageType.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)));
        }
    }

    [Fact]
    public void Razor_Pages_exist_for_Subjects_module()
    {
        var pageTypes = new[]
        {
            typeof(PRN222.RagAssistant.Pages.Subjects.IndexModel)
        };

        foreach (var pageType in pageTypes)
        {
            Assert.NotNull(pageType);
            Assert.True(pageType.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)));
        }
    }

    [Fact]
    public void Razor_Pages_exist_for_Evaluation_module()
    {
        var pageTypes = new[]
        {
            typeof(PRN222.RagAssistant.Pages.Evaluation.IndexModel)
        };

        foreach (var pageType in pageTypes)
        {
            Assert.NotNull(pageType);
            Assert.True(pageType.IsSubclassOf(typeof(Microsoft.AspNetCore.Mvc.RazorPages.PageModel)));
        }
    }

    [Fact]
    public void Documents_Upload_Edit_require_ManageDocuments_policy()
    {
        var protectedPages = new[]
        {
            typeof(DocumentsPages.UploadModel),
            typeof(DocumentsPages.EditModel)
        };

        foreach (var pageType in protectedPages)
        {
            var attributes = pageType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .ToList();

            Assert.NotEmpty(attributes);
            Assert.Contains(attributes, a => a.Policy == AppPolicies.ManageDocuments);
        }
    }

    [Fact]
    public void Chapters_Create_Edit_Delete_require_ManageDocuments_policy()
    {
        var protectedPages = new[]
        {
            typeof(ChaptersPages.CreateModel),
            typeof(ChaptersPages.EditModel),
            typeof(ChaptersPages.DeleteModel)
        };

        foreach (var pageType in protectedPages)
        {
            var attributes = pageType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .ToList();

            Assert.NotEmpty(attributes);
            Assert.Contains(attributes, a => a.Policy == AppPolicies.ManageDocuments);
        }
    }

    [Fact]
    public void AdminUsers_pages_require_ManageUsers_policy()
    {
        var protectedPages = new[]
        {
            typeof(AdminUsersPages.IndexModel),
            typeof(AdminUsersPages.CreateModel),
            typeof(AdminUsersPages.EditModel)
        };

        foreach (var pageType in protectedPages)
        {
            var attributes = pageType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .ToList();

            Assert.NotEmpty(attributes);
            Assert.Contains(attributes, a => a.Policy == AppPolicies.ManageUsers);
        }
    }

    [Fact]
    public void AdminSubjects_pages_require_ManageSubjects_policy()
    {
        var protectedPages = new[]
        {
            typeof(AdminSubjectsPages.IndexModel),
            typeof(AdminSubjectsPages.CreateModel),
            typeof(AdminSubjectsPages.EditModel),
            typeof(AdminSubjectsPages.LeadersModel)
        };

        foreach (var pageType in protectedPages)
        {
            var attributes = pageType
                .GetCustomAttributes(typeof(AuthorizeAttribute), inherit: true)
                .Cast<AuthorizeAttribute>()
                .ToList();

            Assert.NotEmpty(attributes);
            Assert.Contains(attributes, a => a.Policy == AppPolicies.ManageSubjects);
        }
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
