using Npgsql;

namespace PRN222.RagAssistant.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres");

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "Connection string 'Postgres' is required. Configure ConnectionStrings:Postgres or the ConnectionStrings__Postgres environment variable.");
        }

        var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
        dataSourceBuilder.UseVector();
        services.AddSingleton(dataSourceBuilder.Build());

        var ollamaBaseUrl = configuration["Rag:Ollama:BaseUrl"];

        if (!Uri.TryCreate(ollamaBaseUrl, UriKind.Absolute, out var ollamaUri))
        {
            throw new InvalidOperationException(
                "Rag:Ollama:BaseUrl must be configured with an absolute URL, for example http://localhost:11434.");
        }

        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = ollamaUri;
            client.Timeout = TimeSpan.FromMinutes(5);
        });

        return services;
    }
}
