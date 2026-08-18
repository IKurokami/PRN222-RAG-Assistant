using Microsoft.EntityFrameworkCore;
using Npgsql;
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

            // Render Postgres supports pgvector, but PostgreSQL requires the extension
            // to be explicitly enabled before migrations create vector columns.
            await dbContext.Database.ExecuteSqlRawAsync("CREATE EXTENSION IF NOT EXISTS vector;");

            // Npgsql caches database type metadata. On a brand-new Render database the
            // datasource can be initialized before pgvector exists, so reload the type
            // metadata after CREATE EXTENSION and before any vector-valued query runs.
            var sharedDataSource = scope.ServiceProvider.GetRequiredService<NpgsqlDataSource>();
            await sharedDataSource.ReloadTypesAsync();

            await dbContext.Database.OpenConnectionAsync();
            try
            {
                if (dbContext.Database.GetDbConnection() is NpgsqlConnection connection)
                {
                    await connection.ReloadTypesAsync();
                }
            }
            finally
            {
                await dbContext.Database.CloseConnectionAsync();
            }

            await dbContext.Database.MigrateAsync();
        }

        await IdentitySeeder.SeedAsync(scope.ServiceProvider, app.Configuration);
    }
}
