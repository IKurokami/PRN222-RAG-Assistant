using System.Net.Http.Headers;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure.Parsing;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Security;

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

        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

        services.AddIdentity<ApplicationUser, IdentityRole<Guid>>(options =>
            {
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedAccount = false;
                options.Password.RequiredLength = 8;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

        services.ConfigureApplicationCookie(options =>
        {
            options.LoginPath = "/Account/Login";
            options.AccessDeniedPath = "/Account/AccessDenied";
            options.SlidingExpiration = true;
        });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AppPolicies.ManageDocuments,
                policy => policy.RequireRole(AppRoles.Admin, AppRoles.SubjectLeader));

            options.AddPolicy(
                AppPolicies.ManageUsers,
                policy => policy.RequireRole(AppRoles.Admin));

            options.AddPolicy(
                AppPolicies.ManageSubjects,
                policy => policy.RequireRole(AppRoles.Admin));
        });

        services.AddScoped<ISubjectAccessRepository, SubjectAccessRepository>();
        services.AddScoped<ISubjectAccessService, SubjectAccessService>();

        AddAiProvider(services, configuration);

        // Member 3: Document Indexing & Ingestion Services.
        services.AddSingleton<IDocumentIndexingQueue, InMemoryDocumentIndexingQueue>();
        services.AddSingleton<DocumentParserFactory>();
        services.AddSingleton<TextChunker>();
        services.AddScoped<TextEmbeddingBatcher>();
        services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
        services.AddHostedService<DocumentIndexingWorker>();

        return services;
    }

    private static void AddAiProvider(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var provider = FirstNonEmpty(
            configuration["Rag:Provider"],
            configuration["RAG_PROVIDER"],
            "Ollama");

        switch (provider.ToUpperInvariant())
        {
            case "OLLAMA":
                AddOllama(services, configuration);
                services.AddScoped<ITextEmbeddingService, OllamaTextEmbeddingService>();
                services.AddScoped<IChatCompletionService, OllamaChatCompletionService>();
                break;

            case "OPENAI":
                AddOpenAi(services, configuration);
                services.AddScoped<ITextEmbeddingService, OpenAiTextEmbeddingService>();
                services.AddScoped<IChatCompletionService, OpenAiChatCompletionService>();
                break;

            case "GEMINI":
                AddGemini(services, configuration);
                services.AddScoped<ITextEmbeddingService, GeminiTextEmbeddingService>();
                services.AddScoped<IChatCompletionService, GeminiChatCompletionService>();
                break;

            default:
                throw new InvalidOperationException(
                    $"Unsupported Rag:Provider '{provider}'. Supported values: Ollama, OpenAI, Gemini.");
        }
    }

    private static void AddOllama(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUri = GetRequiredAbsoluteUri(
            FirstNonEmpty(configuration["Rag:Ollama:BaseUrl"], configuration["OLLAMA_BASE_URL"]),
            "Rag:Ollama:BaseUrl / OLLAMA_BASE_URL",
            "http://localhost:11434");

        services.AddHttpClient("Ollama", client =>
        {
            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromMinutes(5);
        });
    }

    private static void AddOpenAi(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUri = GetRequiredAbsoluteUri(
            FirstNonEmpty(configuration["Rag:OpenAI:BaseUrl"], configuration["OPENAI_BASE_URL"]),
            "Rag:OpenAI:BaseUrl / OPENAI_BASE_URL",
            "https://api.openai.com/v1/");
        var apiKey = GetRequiredValue(
            FirstNonEmpty(configuration["Rag:OpenAI:ApiKey"], configuration["OPENAI_API_KEY"]),
            "Rag:OpenAI:ApiKey / OPENAI_API_KEY");

        services.AddHttpClient("OpenAI", client =>
        {
            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);
        });
    }

    private static void AddGemini(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUri = GetRequiredAbsoluteUri(
            FirstNonEmpty(configuration["Rag:Gemini:BaseUrl"], configuration["GEMINI_BASE_URL"]),
            "Rag:Gemini:BaseUrl / GEMINI_BASE_URL",
            "https://generativelanguage.googleapis.com/");
        var apiKey = GetRequiredValue(
            FirstNonEmpty(configuration["Rag:Gemini:ApiKey"], configuration["GEMINI_API_KEY"]),
            "Rag:Gemini:ApiKey / GEMINI_API_KEY");

        services.AddHttpClient("Gemini", client =>
        {
            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromMinutes(2);
            client.DefaultRequestHeaders.Add("x-goog-api-key", apiKey);
        });
    }

    private static Uri GetRequiredAbsoluteUri(
        string? rawValue,
        string settingName,
        string example)
    {
        if (string.IsNullOrWhiteSpace(rawValue)
            || !Uri.TryCreate(rawValue, UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException(
                $"{settingName} must be configured with an absolute URL, for example {example}.");
        }

        return rawValue.EndsWith("/", StringComparison.Ordinal)
            ? uri
            : new Uri(rawValue + "/", UriKind.Absolute);
    }

    private static string GetRequiredValue(
        string? value,
        string settingName)
    {
        return !string.IsNullOrWhiteSpace(value)
            ? value
            : throw new InvalidOperationException(
                $"{settingName} is required when its AI provider is selected.");
    }

    private static string FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim() ?? string.Empty;
    }
}
