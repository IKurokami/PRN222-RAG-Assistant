namespace PRN222.RagAssistant.Infrastructure.Rag.Exceptions;

/// <summary>
/// Thrown when a user attempts to execute a RAG query without sufficient quota.
/// </summary>
public sealed class InsufficientQuotaException : RagException
{
    public Guid? UserId { get; }

    public InsufficientQuotaException(Guid userId)
        : base("Bạn đã hết lượt hỏi. Vui lòng nạp thêm quota để tiếp tục.")
    {
        UserId = userId;
    }

    public InsufficientQuotaException(string message = "Bạn đã hết lượt hỏi. Vui lòng nạp thêm quota để tiếp tục.")
        : base(message)
    {
    }
}
