using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Data.Configurations;

public sealed class MessageCitationConfiguration : IEntityTypeConfiguration<MessageCitation>
{
    public void Configure(EntityTypeBuilder<MessageCitation> builder)
    {
        builder.ToTable("MessageCitations");

        builder.HasKey(x => x.Id);

        builder.HasIndex(x => new { x.ChatMessageId, x.Rank })
            .IsUnique();

        builder.HasIndex(x => new { x.ChatMessageId, x.DocumentChunkId })
            .IsUnique();

        builder.HasOne<ChatMessage>()
            .WithMany()
            .HasForeignKey(x => x.ChatMessageId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<DocumentChunk>()
            .WithMany()
            .HasForeignKey(x => x.DocumentChunkId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
