using Microsoft.AspNetCore.Identity;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Infrastructure.Identity;

public static class IdentitySeeder
{
    public static async Task SeedAsync(
        IServiceProvider services,
        IConfiguration configuration)
    {
        var roleManager = services.GetRequiredService<RoleManager<IdentityRole<Guid>>>();
        var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();

        foreach (var roleName in AppRoles.All)
        {
            if (!await roleManager.RoleExistsAsync(roleName))
            {
                var result = await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                EnsureSucceeded(result, $"create role '{roleName}'");
            }
        }

        if (!configuration.GetValue<bool>("Auth:SeedUsers:Enabled"))
        {
            return;
        }

        await SeedUserAsync(
            userManager,
            configuration,
            "SubjectLeader",
            AppRoles.SubjectLeader);

        await SeedUserAsync(
            userManager,
            configuration,
            "Student",
            AppRoles.Student);
    }

    private static async Task SeedUserAsync(
        UserManager<ApplicationUser> userManager,
        IConfiguration configuration,
        string configurationKey,
        string roleName)
    {
        var section = configuration.GetSection($"Auth:SeedUsers:{configurationKey}");
        var email = section["Email"];
        var password = section["Password"];
        var displayName = section["DisplayName"];

        if (string.IsNullOrWhiteSpace(email) ||
            string.IsNullOrWhiteSpace(password) ||
            string.IsNullOrWhiteSpace(displayName))
        {
            throw new InvalidOperationException(
                $"Auth:SeedUsers:{configurationKey} must provide Email, Password, and DisplayName when demo user seeding is enabled.");
        }

        var user = await userManager.FindByEmailAsync(email);

        if (user is null)
        {
            user = new ApplicationUser
            {
                Id = Guid.NewGuid(),
                UserName = email,
                Email = email,
                EmailConfirmed = true,
                DisplayName = displayName,
                CreatedAtUtc = DateTime.UtcNow
            };

            var createResult = await userManager.CreateAsync(user, password);
            EnsureSucceeded(createResult, $"create demo user '{email}'");
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            var roleResult = await userManager.AddToRoleAsync(user, roleName);
            EnsureSucceeded(roleResult, $"assign role '{roleName}' to '{email}'");
        }
    }

    private static void EnsureSucceeded(IdentityResult result, string operation)
    {
        if (result.Succeeded)
        {
            return;
        }

        var errors = string.Join("; ", result.Errors.Select(x => x.Description));
        throw new InvalidOperationException($"Failed to {operation}: {errors}");
    }
}
