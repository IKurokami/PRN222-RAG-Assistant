namespace PRN222.RagAssistant.Infrastructure.Rag.Exceptions;


/// <summary>
/// Thrown when the requested chat session does not exist or does not belong to the user.
/// </summary>
public sealed class ChatSessionNotFoundException : RagException
{
    public Guid SessionId { get; }
    public Guid UserId { get; }

    public ChatSessionNotFoundException(Guid sessionId, Guid userId)
        : base($"Chat session '{sessionId}' not found or does not belong to user '{userId}'.")
    {
        SessionId = sessionId;
        UserId = userId;
    }
}
