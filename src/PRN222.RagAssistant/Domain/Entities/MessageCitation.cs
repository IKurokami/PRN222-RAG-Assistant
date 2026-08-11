namespace PRN222.RagAssistant.Domain.Entities;

public sealed class MessageCitation
{
    public Guid Id { get; set; }

    public Guid ChatMessageId { get; set; }

    public Guid DocumentChunkId { get; set; }

    public int Rank { get; set; }
}
