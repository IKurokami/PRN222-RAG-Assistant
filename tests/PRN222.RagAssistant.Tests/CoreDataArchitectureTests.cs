using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Infrastructure;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Tests;

public sealed class CoreDataArchitectureTests
{
    [Fact]
    public void Core_relationships_keep_explicit_delete_behaviors()
    {
        using var context = CreateContext();

        AssertDeleteBehavior<Document>(context, nameof(Document.SubjectId), DeleteBehavior.Restrict);
        AssertDeleteBehavior<Document>(context, nameof(Document.ChapterId), DeleteBehavior.Restrict);
        AssertDeleteBehavior<Document>(context, nameof(Document.UploadedByUserId), DeleteBehavior.Restrict);
        AssertDeleteBehavior<DocumentChunk>(context, nameof(DocumentChunk.DocumentId), DeleteBehavior.Cascade);
        AssertDeleteBehavior<ChatSession>(context, nameof(ChatSession.UserId), DeleteBehavior.Cascade);
        AssertDeleteBehavior<ChatMessage>(context, nameof(ChatMessage.ChatSessionId), DeleteBehavior.Cascade);
        AssertDeleteBehavior<MessageCitation>(context, nameof(MessageCitation.ChatMessageId), DeleteBehavior.Cascade);
        AssertDeleteBehavior<MessageCitation>(context, nameof(MessageCitation.DocumentChunkId), DeleteBehavior.Restrict);
    }

    [Fact]
    public void Rag_persistence_fields_are_part_of_the_ef_model()
    {
        using var context = CreateContext();

        AssertRequiredProperty<Document>(context, nameof(Document.IndexStatus));
        AssertOptionalProperty<Document>(context, nameof(Document.IndexError));
        AssertOptionalProperty<Document>(context, nameof(Document.IndexedAtUtc));
        AssertRequiredProperty<DocumentChunk>(context, nameof(DocumentChunk.Content));
        AssertOptionalProperty<DocumentChunk>(context, nameof(DocumentChunk.PageNumber));
        AssertOptionalProperty<DocumentChunk>(context, nameof(DocumentChunk.SlideNumber));
        AssertOptionalProperty<DocumentChunk>(context, nameof(DocumentChunk.Embedding));
        AssertRequiredProperty<ChatSession>(context, nameof(ChatSession.Title));
        AssertRequiredProperty<ChatMessage>(context, nameof(ChatMessage.Role));
        AssertRequiredProperty<ChatMessage>(context, nameof(ChatMessage.Content));
        AssertRequiredProperty<MessageCitation>(context, nameof(MessageCitation.Rank));
    }

    [Fact]
    public async Task ManageDocuments_policy_allows_admin_and_subject_leader_only()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=localhost;Port=5432;Database=prn222_test;Username=postgres;Password=postgres",
                ["Rag:Ollama:BaseUrl"] = "http://localhost:11434"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddLogging();
        services.AddInfrastructure(configuration);

        await using var provider = services.BuildServiceProvider();
        var policyProvider = provider.GetRequiredService<IAuthorizationPolicyProvider>();
        var policy = await policyProvider.GetPolicyAsync(AppPolicies.ManageDocuments);

        Assert.NotNull(policy);

        var roleRequirement = Assert.Single(
            policy!.Requirements.OfType<RolesAuthorizationRequirement>());

        Assert.Contains(AppRoles.Admin, roleRequirement.AllowedRoles);
        Assert.Contains(AppRoles.SubjectLeader, roleRequirement.AllowedRoles);
        Assert.DoesNotContain(AppRoles.Student, roleRequirement.AllowedRoles);
        Assert.Equal(2, roleRequirement.AllowedRoles.Count());
    }

    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql(
                "Host=localhost;Port=5432;Database=prn222_test;Username=postgres;Password=postgres",
                npgsql => npgsql.UseVector())
            .Options;

        return new ApplicationDbContext(options);
    }

    private static void AssertDeleteBehavior<TEntity>(
        ApplicationDbContext context,
        string foreignKeyPropertyName,
        DeleteBehavior expected)
    {
        var entityType = GetEntityType<TEntity>(context);
        var foreignKey = Assert.Single(
            entityType.GetForeignKeys(),
            candidate => candidate.Properties.Any(
                property => property.Name == foreignKeyPropertyName));

        Assert.Equal(expected, foreignKey.DeleteBehavior);
    }

    private static void AssertRequiredProperty<TEntity>(
        ApplicationDbContext context,
        string propertyName)
    {
        var property = GetEntityType<TEntity>(context).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.False(property!.IsNullable);
    }

    private static void AssertOptionalProperty<TEntity>(
        ApplicationDbContext context,
        string propertyName)
    {
        var property = GetEntityType<TEntity>(context).FindProperty(propertyName);

        Assert.NotNull(property);
        Assert.True(property!.IsNullable);
    }

    private static IEntityType GetEntityType<TEntity>(ApplicationDbContext context)
    {
        return context.Model.FindEntityType(typeof(TEntity))
            ?? throw new InvalidOperationException($"{typeof(TEntity).Name} is not part of the EF Core model.");
    }
}
