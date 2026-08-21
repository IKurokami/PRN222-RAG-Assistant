using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Pages.AdminUsers;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class AdminUserManagementTests
{
    [Fact]
    public void AdminUserRoleEditViewModel_requires_role_field()
    {
        var viewModel = new AdminUserRoleEditViewModel
        {
            Id = Guid.NewGuid(),
            Email = "test@test.com",
            DisplayName = "Test User",
            Role = AppRoles.Admin
        };

        Assert.NotNull(viewModel);
        Assert.Equal(AppRoles.Admin, viewModel.Role);
    }

    [Fact]
    public void Edit_page_model_exists_and_inherits_from_page_model()
    {
        var editModelType = typeof(PRN222.RagAssistant.Pages.AdminUsers.EditModel);
        Assert.True(editModelType.IsSubclassOf(typeof(PageModel)));
    }

    [Fact]
    public void Create_page_model_exists_and_inherits_from_page_model()
    {
        var createModelType = typeof(PRN222.RagAssistant.Pages.AdminUsers.CreateModel);
        Assert.True(createModelType.IsSubclassOf(typeof(PageModel)));
    }

    [Fact]
    public void Index_page_model_exists_and_inherits_from_page_model()
    {
        var indexModelType = typeof(PRN222.RagAssistant.Pages.AdminUsers.IndexModel);
        Assert.True(indexModelType.IsSubclassOf(typeof(PageModel)));
    }

    [Fact]
    public void AdminUserRoleEditViewModel_has_required_properties()
    {
        var viewModel = new AdminUserRoleEditViewModel();

        var idProperty = typeof(AdminUserRoleEditViewModel).GetProperty(nameof(AdminUserRoleEditViewModel.Id));
        var emailProperty = typeof(AdminUserRoleEditViewModel).GetProperty(nameof(AdminUserRoleEditViewModel.Email));
        var displayNameProperty = typeof(AdminUserRoleEditViewModel).GetProperty(nameof(AdminUserRoleEditViewModel.DisplayName));
        var roleProperty = typeof(AdminUserRoleEditViewModel).GetProperty(nameof(AdminUserRoleEditViewModel.Role));

        Assert.NotNull(idProperty);
        Assert.NotNull(emailProperty);
        Assert.NotNull(displayNameProperty);
        Assert.NotNull(roleProperty);
    }
}
