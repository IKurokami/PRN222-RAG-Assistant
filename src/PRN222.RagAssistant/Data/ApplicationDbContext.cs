using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Data;

public sealed class ApplicationDbContext
    : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public DbSet<Subject> Subjects => Set<Subject>();

    public DbSet<Chapter> Chapters => Set<Chapter>();

    public DbSet<Document> Documents => Set<Document>();

    public DbSet<DocumentChunk> DocumentChunks => Set<DocumentChunk>();

    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();

    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();

    public DbSet<MessageCitation> MessageCitations => Set<MessageCitation>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(ApplicationDbContext).Assembly);

        // EF Core InMemory is used by unit tests and cannot map PostgreSQL's vector type.
        // Production PostgreSQL keeps the vector property and its native column mapping.
        if (Database.ProviderName == "Microsoft.EntityFrameworkCore.InMemory")
        {
            builder.Entity<Domain.Entities.DocumentChunk>().Ignore(chunk => chunk.Embedding);
        }
    }
}
