namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Tuning knobs for the RAG query pipeline exposed through configuration.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public RetrievalOptions Retrieval { get; set; } = new();
    public ChatOptions Chat { get; set; } = new();

    public sealed class RetrievalOptions
    {
        public int TopK { get; set; } = 5;
        public double MinimumSimilarityScore { get; set; } = 0.3;
        public int MaxContextChars { get; set; } = 4000;
        public bool IncludeConversationHistory { get; set; } = true;
        public int HistoryTurns { get; set; } = 5;
        public int ExcerptChars { get; set; } = 240;
    }

    public sealed class ChatOptions
    {
        public string NoEvidenceMessage { get; set; } =
            "Tôi chỉ có thể trả lời dựa trên tài liệu PRN222 đã được index. Hiện không tìm thấy thông tin phù hợp cho câu hỏi này.";

        public string? SystemPromptTemplate { get; set; }

        public string NoEvidenceAssistantContent { get; set; } = "(no-evidence)";
    }
}
