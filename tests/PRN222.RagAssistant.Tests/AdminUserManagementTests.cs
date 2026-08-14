using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using PRN222.RagAssistant.Controllers;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Admin;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class AdminUserManagementTests
{
    [Fact]
    public async Task Edit_keeps_Admin_role_when_current_Admin_requests_self_demotion()
    {
        using var userManager = new TestUserManager();
        var admin = userManager.AddUser("current-admin@prn222.test", AppRoles.Admin);
        var controller = CreateController(userManager, admin.Id);
        var viewModel = new AdminUserRoleEditViewModel
        {
            Id = admin.Id,
            Role = AppRoles.Student
        };

        var result = await controller.Edit(admin.Id, viewModel);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(AdminUserRoleEditViewModel.Role)]!.Errors,
            error => error.ErrorMessage == "You cannot remove your own Admin role.");
        Assert.True(await userManager.IsInRoleAsync(admin, AppRoles.Admin));
        Assert.False(await userManager.IsInRoleAsync(admin, AppRoles.Student));
    }

    [Fact]
    public async Task Edit_keeps_Admin_role_when_target_is_the_last_Admin()
    {
        using var userManager = new TestUserManager();
        var admin = userManager.AddUser("last-admin@prn222.test", AppRoles.Admin);
        var controller = CreateController(userManager, Guid.NewGuid());
        var viewModel = new AdminUserRoleEditViewModel
        {
            Id = admin.Id,
            Role = AppRoles.SubjectLeader
        };

        var result = await controller.Edit(admin.Id, viewModel);

        Assert.IsType<ViewResult>(result);
        Assert.Contains(
            controller.ModelState[nameof(AdminUserRoleEditViewModel.Role)]!.Errors,
            error => error.ErrorMessage == "The last Admin account cannot be demoted.");
        Assert.True(await userManager.IsInRoleAsync(admin, AppRoles.Admin));
        Assert.False(await userManager.IsInRoleAsync(admin, AppRoles.SubjectLeader));
    }

    private static AdminUsersController CreateController(TestUserManager userManager, Guid currentUserId)
    {
        var identity = new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
                new Claim(ClaimTypes.Name, "admin-test@prn222.test"),
                new Claim(ClaimTypes.Role, AppRoles.Admin)
            ],
            authenticationType: "TestAuth");

        return new AdminUsersController(userManager)
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext
                {
                    User = new ClaimsPrincipal(identity)
                }
            }
        };
    }

    private sealed class TestUserManager : UserManager<ApplicationUser>
    {
        private readonly Dictionary<Guid, ApplicationUser> _users = [];
        private readonly Dictionary<Guid, HashSet<string>> _roles = [];

        public TestUserManager()
            : base(
                new StubUserStore(),
                Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
                new PasswordHasher<ApplicationUser>(),
                [],
                [],
                new UpperInvariantLookupNormalizer(),
                new IdentityErrorDescriber(),
                EmptyServiceProvider.Instance,
                NullLogger<UserManager<ApplicationUser>>.Instance)
        {
        }

        public ApplicationUser AddUser(string email, params string[] roles)
        {
            var user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = email,
                CreatedAtUtc = DateTime.UtcNow
            };

            _users.Add(user.Id, user);
            _roles.Add(user.Id, new HashSet<string>(roles, StringComparer.Ordinal));
            return user;
        }

        public override Task<ApplicationUser?> FindByIdAsync(string userId)
        {
            var user = Guid.TryParse(userId, out var id) && _users.TryGetValue(id, out var match)
                ? match
                : null;
            return Task.FromResult(user);
        }

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            var userId = principal.FindFirstValue(ClaimTypes.NameIdentifier);
            return userId is null ? Task.FromResult<ApplicationUser?>(null) : FindByIdAsync(userId);
        }

        public override Task<IList<string>> GetRolesAsync(ApplicationUser user)
        {
            IList<string> roles = _roles[user.Id].ToList();
            return Task.FromResult(roles);
        }

        public override Task<IList<ApplicationUser>> GetUsersInRoleAsync(string roleName)
        {
            IList<ApplicationUser> users = _users.Values
                .Where(user => _roles[user.Id].Contains(roleName))
                .ToList();
            return Task.FromResult(users);
        }

        public override Task<bool> IsInRoleAsync(ApplicationUser user, string role)
        {
            return Task.FromResult(_roles[user.Id].Contains(role));
        }
    }

    private sealed class StubUserStore : IUserStore<ApplicationUser>
    {
        public void Dispose()
        {
        }

        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.Id.ToString());

        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.UserName);

        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken)
        {
            user.UserName = userName;
            return Task.CompletedTask;
        }

        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(user.NormalizedUserName);

        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken)
        {
            user.NormalizedUserName = normalizedName;
            return Task.CompletedTask;
        }

        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) =>
            Task.FromResult(IdentityResult.Success);

        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationUser?>(null);

        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) =>
            Task.FromResult<ApplicationUser?>(null);
    }

    private sealed class EmptyServiceProvider : IServiceProvider
    {
        public static readonly EmptyServiceProvider Instance = new();

        public object? GetService(Type serviceType) => null;
    }
}
