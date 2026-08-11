using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Infrastructure.Identity;

namespace PRN222.RagAssistant.Infrastructure;

public static class DatabaseInitializer
{
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        await using var scope = app.Services.CreateAsyncScope();

        if (app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await dbContext.Database.MigrateAsync();
        }

        await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
    }
}
