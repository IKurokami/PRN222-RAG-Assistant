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

            // Render Postgres supports pgvector, but PostgreSQL 13+ requires the
            // extension to be explicitly enabled before migrations create vector columns.
            await dbContext.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");
            await dbContext.Database.MigrateAsync();
        }

        await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
    }
}
