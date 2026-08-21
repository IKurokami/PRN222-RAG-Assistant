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

            Yêu cầu: Dựa CHÍNH XÁC vào các đoạn tài liệu trong [CONTEXT] ở trên để trả lời câu hỏi. Nêu đúng các dữ kiện thực tế (như năm xuất bản, tác giả, tên sách, phiên bản) và gắn marker trích dẫn [n] ngay tại vị trí thông tin đó.
            """;

        return (systemPrompt, userPrompt);
    }

    private string BuildSystemPrompt()
    {
        return """
            Bạn là trợ lý học tập AI chuyên sâu. Nhiệm vụ của bạn là giải đáp câu hỏi một cách chính xác, trung thực dựa trên các đoạn tài liệu được cung cấp trong [CONTEXT].

            QUY TẮC BẮT BUỘC:
            1. Căn cứ tài liệu: CHỈ trả lời dựa trên các dữ kiện có trong [CONTEXT]. Tuyệt đối KHÔNG suy đoán hoặc sử dụng kiến thức bên ngoài về các dữ kiện thực tế (năm xuất bản, tác giả, phiên bản, thông số...). Phải lấy chính xác các chi tiết (ví dụ năm 2024) y như trong tài liệu.
            2. Gắn marker trích dẫn: Đặt marker [n] (ví dụ: [1], [2]) ngay sau câu hoặc dữ kiện được trích xuất từ đoạn [n] tương ứng. Không gom toàn bộ marker về cuối đoạn.
            3. Nếu không đủ thông tin trong tài liệu: Hãy nói rõ "không tìm thấy thông tin phù hợp trong tài liệu".
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
