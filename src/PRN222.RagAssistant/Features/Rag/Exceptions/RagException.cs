namespace PRN222.RagAssistant.Features.Rag.Exceptions;

/// <summary>
/// Base exception for RAG-related errors.
/// </summary>
public abstract class RagException : Exception
{
    protected RagException(string message) : base(message) { }
    protected RagException(string message, Exception innerException) : base(message, innerException) { }
}
