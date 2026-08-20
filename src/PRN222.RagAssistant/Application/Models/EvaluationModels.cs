namespace PRN222.RagAssistant.Application.Models;

public sealed class EvaluationQuestion
{
    public int Id { get; set; }
    public string Module { get; set; } = string.Empty;
    public string Topic { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string GroundTruthAnswer { get; set; } = string.Empty;
    public List<string> ExpectedKeywords { get; set; } = new();
    public string SubjectCode { get; set; } = "PRN222";
}

public sealed class EvaluationResult
{
    public int QuestionId { get; set; }
    public string Module { get; set; } = string.Empty;
    public string QuestionText { get; set; } = string.Empty;
    public string GroundTruthAnswer { get; set; } = string.Empty;
    public string SystemAnswer { get; set; } = string.Empty;
    public int CitationsCount { get; set; }
    public List<string> MatchedKeywords { get; set; } = new();
    public List<string> MissingKeywords { get; set; } = new();
    public double KeywordAccuracyPercent { get; set; }
    public bool HasCitations { get; set; }
    public long ExecutionTimeMs { get; set; }
}

public sealed class EvaluationReportSummary
{
    public DateTime EvaluatedAtUtc { get; set; } = DateTime.UtcNow;
    public int TotalQuestions { get; set; }
    public int EvaluatedQuestions { get; set; }
    public double AverageKeywordAccuracyPercent { get; set; }
    public int QuestionsWithCitationsCount { get; set; }
    public double CitationCoveragePercent { get; set; }
    public long TotalDurationMs { get; set; }
    public List<EvaluationResult> Results { get; set; } = new();
}
