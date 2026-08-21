namespace PRN222.RagAssistant.Infrastructure.Rag;

/// <summary>
/// Tuning knobs for the RAG query pipeline exposed through configuration.
/// </summary>
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public RetrievalOptions Retrieval { get; set; } = new();
    public ChatOptions Chat { get; set; } = new();
    public AgenticOptions Agentic { get; set; } = new();

    public sealed class RetrievalOptions
    {
        public int TopK { get; set; } = 5;
        public double MinimumSimilarityScore { get; set; } = 0.3;
        public int MaxContextChars { get; set; } = 4000;
        public bool IncludeConversationHistory { get; set; } = true;
        public int HistoryTurns { get; set; } = 5;
        public int ExcerptChars { get; set; } = 4000;
    }

    public sealed class ChatOptions
    {
        public string NoEvidenceMessage { get; set; } =
            "Tôi chỉ có thể trả lời dựa trên tài liệu đã được index. Hiện không tìm thấy thông tin phù hợp cho câu hỏi này.";
    }

    public sealed class AgenticOptions
    {
        /// <summary>
        /// Enables model-directed retrieval when the selected chat provider exposes
        /// IAgenticChatCompletionService. Unsupported providers automatically fall back
        /// to the deterministic RAG pipeline.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>Default maximum number of chunks returned from a retrieval tool.</summary>
        public int ToolTopK { get; set; } = 6;

        /// <summary>Maximum characters returned by one tool invocation.</summary>
        public int MaxToolResultChars { get; set; } = 7000;
    }
}
