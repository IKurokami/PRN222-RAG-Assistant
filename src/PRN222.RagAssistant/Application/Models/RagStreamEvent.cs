namespace PRN222.RagAssistant.Application.Models;

/// <summary>
/// Structured events emitted by the RAG pipeline while a chat response is running.
/// The Razor Page translates these events to Server-Sent Events without inventing
/// synthetic model tokens.
/// </summary>
public abstract record RagStreamEvent;

public sealed record RagToolCallEvent(
    string Id,
    string Tool,
    string Status,
    string Title,
    string? Detail = null) : RagStreamEvent;

public sealed record RagDeltaEvent(string Content) : RagStreamEvent;

public sealed record RagCitationsEvent(
    IReadOnlyList<RagCitation> Citations) : RagStreamEvent;

public sealed record RagCompletedEvent(RagAnswer Answer) : RagStreamEvent;

/// <summary>
/// Emitted when the RAG pipeline encounters a recoverable, named error
/// (e.g. provider rate-limit) that the frontend should surface distinctly
/// from a normal "no documents found" result.
/// </summary>
/// <param name="ErrorCode">
/// Machine-readable code — e.g. <c>AI_PROVIDER_RATE_LIMITED</c> or <c>STREAM_ERROR</c>.
/// </param>
/// <param name="Message">Localised, user-facing message (no stack trace).</param>
public sealed record RagErrorEvent(string ErrorCode, string Message) : RagStreamEvent;

