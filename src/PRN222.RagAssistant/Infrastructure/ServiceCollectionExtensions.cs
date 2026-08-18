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
    private static readonly string[] SupportedAiProviders = ["Ollama", "OpenAI", "Gemini", "OpenRouter"];

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

        AddAiProviders(services, configuration);

        // Member 3: Document Indexing & Ingestion Services.
        services.AddSingleton<IDocumentIndexingQueue, InMemoryDocumentIndexingQueue>();
        services.AddSingleton<DocumentParserFactory>();
        services.Configure<ChunkingOptions>(configuration.GetSection(ChunkingOptions.SectionName));
        services.AddSingleton<TextChunker>();
        services.AddScoped<TextEmbeddingBatcher>();
        services.AddScoped<IDocumentIndexingService, DocumentIndexingService>();
        services.AddHostedService<DocumentIndexingWorker>();

        // Member 4: RAG Query Pipeline.
        services.Configure<Infrastructure.Rag.RagOptions>(configuration.GetSection(Infrastructure.Rag.RagOptions.SectionName));
        services.AddSingleton<Infrastructure.Rag.GroundedPromptBuilder>();
        services.AddScoped<Infrastructure.Rag.IDocumentChunkRetriever, Infrastructure.Rag.PgVectorDocumentChunkRetriever>();
        services.AddScoped<IRagQueryService, Features.Rag.RagQueryService>();

        return services;
    }

    private static void AddAiProviders(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var legacyProvider = FirstNonEmpty(
            configuration["Rag:Provider"],
            configuration["RAG_PROVIDER"],
            "Ollama");
        var chatProvider = FirstNonEmpty(
            configuration["Rag:ChatProvider"],
            configuration["RAG_CHAT_PROVIDER"],
            legacyProvider);
        var embeddingProvider = FirstNonEmpty(
            configuration["Rag:EmbeddingProvider"],
            configuration["RAG_EMBEDDING_PROVIDER"],
            legacyProvider);

        ValidateProvider(chatProvider, "Rag:ChatProvider / RAG_CHAT_PROVIDER");
        ValidateProvider(embeddingProvider, "Rag:EmbeddingProvider / RAG_EMBEDDING_PROVIDER");

        foreach (var provider in new[] { chatProvider, embeddingProvider }
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            AddProviderHttpClient(services, configuration, provider);
        }

        AddEmbeddingService(services, embeddingProvider);
        AddChatService(services, chatProvider);
    }

    private static void AddProviderHttpClient(
        IServiceCollection services,
        IConfiguration configuration,
        string provider)
    {
        switch (provider.ToUpperInvariant())
        {
            case "OLLAMA":
                AddOllama(services, configuration);
                break;
            case "OPENAI":
                AddOpenAi(services, configuration);
                break;
            case "GEMINI":
                AddGemini(services, configuration);
                break;
            case "OPENROUTER":
                AddOpenRouter(services, configuration);
                break;
        }
    }

    private static void AddEmbeddingService(IServiceCollection services, string provider)
    {
        switch (provider.ToUpperInvariant())
        {
            case "OLLAMA":
                services.AddScoped<ITextEmbeddingService, OllamaTextEmbeddingService>();
                break;
            case "OPENAI":
                services.AddScoped<ITextEmbeddingService, OpenAiTextEmbeddingService>();
                break;
            case "GEMINI":
                services.AddScoped<ITextEmbeddingService, GeminiTextEmbeddingService>();
                break;
            case "OPENROUTER":
                services.AddScoped<ITextEmbeddingService, OpenRouterTextEmbeddingService>();
                break;
        }
    }

    private static void AddChatService(IServiceCollection services, string provider)
    {
        switch (provider.ToUpperInvariant())
        {
            case "OLLAMA":
                services.AddScoped<IChatCompletionService, OllamaChatCompletionService>();
                break;
            case "OPENAI":
                services.AddScoped<IChatCompletionService, OpenAiChatCompletionService>();
                break;
            case "GEMINI":
                services.AddScoped<IChatCompletionService, GeminiChatCompletionService>();
                break;
            case "OPENROUTER":
                services.AddScoped<IChatCompletionService, OpenRouterChatCompletionService>();
                break;
        }
    }

    private static void ValidateProvider(string provider, string settingName)
    {
        if (!SupportedAiProviders.Contains(provider, StringComparer.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"Unsupported {settingName} value '{provider}'. Supported values: {string.Join(", ", SupportedAiProviders)}.");
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

    private static void AddOpenRouter(
        IServiceCollection services,
        IConfiguration configuration)
    {
        var baseUri = GetRequiredAbsoluteUri(
            FirstNonEmpty(configuration["Rag:OpenRouter:BaseUrl"], configuration["OPENROUTER_BASE_URL"]),
            "Rag:OpenRouter:BaseUrl / OPENROUTER_BASE_URL",
            "https://openrouter.ai/api/v1/");
        var apiKey = GetRequiredValue(
            FirstNonEmpty(configuration["Rag:OpenRouter:ApiKey"], configuration["OPENROUTER_API_KEY"]),
            "Rag:OpenRouter:ApiKey / OPENROUTER_API_KEY");
        var httpReferer = FirstNonEmpty(
            configuration["Rag:OpenRouter:HttpReferer"],
            configuration["OPENROUTER_HTTP_REFERER"]);
        var appTitle = FirstNonEmpty(
            configuration["Rag:OpenRouter:AppTitle"],
            configuration["OPENROUTER_APP_TITLE"],
            "PRN222 RAG Assistant");

        services.AddHttpClient("OpenRouter", client =>
        {
            client.BaseAddress = baseUri;
            client.Timeout = TimeSpan.FromMinutes(3);
            client.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", apiKey);

            if (!string.IsNullOrWhiteSpace(httpReferer))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("HTTP-Referer", httpReferer);
            }

            if (!string.IsNullOrWhiteSpace(appTitle))
            {
                client.DefaultRequestHeaders.TryAddWithoutValidation("X-Title", appTitle);
            }
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
