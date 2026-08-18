using Microsoft.Extensions.Options;
using PRN222.RagAssistant.Infrastructure.Rag;

namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Composes system and user prompts for the LLM using configured templates and chunk metadata.
/// </summary>
public sealed class GroundedPromptBuilder
{
    private readonly RagOptions _options;

    public GroundedPromptBuilder(IOptions<RagOptions> options)
    {
        _options = options.Value;
    }

    public (string SystemPrompt, string UserPrompt) Build(
        string question,
        IReadOnlyList<RetrievedChunk> chunks,
        IReadOnlyList<ChatHistoryEntry> history)
    {
        var systemPrompt = BuildSystemPrompt();
        var contextBlock = BuildContextBlock(chunks);
        var historyBlock = BuildHistoryBlock(history);

        var userPrompt = $"""
            Câu hỏi: {question}

            [CONTEXT]
            {contextBlock}
            [/CONTEXT]

            {historyBlock}
            """;

        return (systemPrompt, userPrompt);
    }

    private string BuildSystemPrompt()
    {
        return """
            Bạn là trợ lý học tập. CHỈ trả lời dựa trên các đoạn tài liệu dưới đây.
            Nếu không đủ thông tin, hãy nói rõ "không tìm thấy".
            Với mỗi thông tin bạn sử dụng, hãy ghi marker [n] theo số đoạn tài liệu tương ứng.
            """;
    }

    private string BuildContextBlock(IReadOnlyList<RetrievedChunk> chunks)
    {
        if (chunks.Count == 0)
            return "(Không có tài liệu liên quan)";

        var lines = new List<string>();
        var maxCharsPerChunk = _options.Retrieval.MaxContextChars / Math.Max(1, chunks.Count);

        for (int i = 0; i < chunks.Count; i++)
        {
            var chunk = chunks[i];
            var location = FormatLocation(chunk.PageNumber, chunk.SlideNumber);
            var truncated = TruncateText(chunk.Content, maxCharsPerChunk);
            lines.Add($"[{i + 1}] {location} {truncated}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private string BuildHistoryBlock(IReadOnlyList<ChatHistoryEntry> history)
    {
        if (!_options.Retrieval.IncludeConversationHistory || history.Count == 0)
            return string.Empty;

        var lines = new List<string> { "Lịch sử hội thoại gần đây:" };
        foreach (var entry in history)
        {
            var role = entry.Role.ToLowerInvariant() switch
            {
                "user" => "Người dùng",
                "assistant" => "Trợ lý",
                _ => entry.Role
            };
            lines.Add($"{role}: {entry.Content}");
        }

        return string.Join(Environment.NewLine, lines);
    }

    private static string FormatLocation(int? pageNumber, int? slideNumber)
    {
        if (pageNumber.HasValue)
            return $"(Trang {pageNumber})";
        if (slideNumber.HasValue)
            return $"(Slide {slideNumber})";
        return string.Empty;
    }

    private static string TruncateText(string text, int maxChars)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxChars)
            return text;

        var truncated = text.Substring(0, maxChars);
        var lastSpace = truncated.LastIndexOf(' ');
        if (lastSpace > maxChars / 2)
            truncated = truncated.Substring(0, lastSpace);

        return truncated + "...";
    }
}
