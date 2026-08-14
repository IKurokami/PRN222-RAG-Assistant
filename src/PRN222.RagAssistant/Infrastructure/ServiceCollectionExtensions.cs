using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
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

        services.AddScoped<ISubjectAccessService, SubjectAccessService>();

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

        // Member 3: Document Indexing & Ingestion Services
        services.AddSingleton<PRN222.RagAssistant.Application.Abstractions.IDocumentIndexingQueue, PRN222.RagAssistant.Infrastructure.Services.InMemoryDocumentIndexingQueue>();
        services.AddSingleton<PRN222.RagAssistant.Infrastructure.Parsing.DocumentParserFactory>();
        services.AddSingleton<PRN222.RagAssistant.Infrastructure.Parsing.TextChunker>();
        services.AddScoped<PRN222.RagAssistant.Application.Abstractions.ITextEmbeddingService, PRN222.RagAssistant.Infrastructure.Services.OllamaTextEmbeddingService>();
        services.AddScoped<PRN222.RagAssistant.Infrastructure.Services.TextEmbeddingBatcher>();
        services.AddScoped<PRN222.RagAssistant.Application.Abstractions.IDocumentIndexingService, PRN222.RagAssistant.Infrastructure.Services.DocumentIndexingService>();
        services.AddHostedService<PRN222.RagAssistant.Infrastructure.Services.DocumentIndexingWorker>();

        return services;
    }
}
