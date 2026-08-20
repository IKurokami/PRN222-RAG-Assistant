using System.Diagnostics;
using System.Text.Json;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Infrastructure.Services;

public sealed class EvaluationService : IEvaluationService
{
    private readonly IRagQueryService _ragService;
    private readonly ILogger<EvaluationService> _logger;
    private readonly List<EvaluationQuestion> _questions;

    public EvaluationService(
        IRagQueryService ragService,
        ILogger<EvaluationService> logger)
    {
        _ragService = ragService;
        _logger = logger;
        _questions = LoadEmbeddedDataset();
    }

    public IReadOnlyList<EvaluationQuestion> GetQuestions()
    {
        return _questions.AsReadOnly();
    }

    public async Task<EvaluationResult> EvaluateQuestionAsync(
        Guid userId,
        int questionId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var question = _questions.FirstOrDefault(q => q.Id == questionId)
            ?? throw new ArgumentException($"Evaluation question with ID '{questionId}' not found.", nameof(questionId));

        var sessionId = await _ragService.GetOrCreateUserSessionAsync(userId, subjectId, cancellationToken);

        var stopwatch = Stopwatch.StartNew();
        var answer = await _ragService.AskAsync(userId, sessionId, question.QuestionText, subjectId, cancellationToken);
        stopwatch.Stop();

        var matchedKeywords = new List<string>();
        var missingKeywords = new List<string>();

        var systemAnswerText = answer.Answer ?? string.Empty;

        foreach (var keyword in question.ExpectedKeywords)
        {
            if (systemAnswerText.Contains(keyword, StringComparison.OrdinalIgnoreCase))
            {
                matchedKeywords.Add(keyword);
            }
            else
            {
                missingKeywords.Add(keyword);
            }
        }

        double accuracy = question.ExpectedKeywords.Count > 0
            ? (double)matchedKeywords.Count / question.ExpectedKeywords.Count * 100.0
            : 100.0;

        return new EvaluationResult
        {
            QuestionId = question.Id,
            Module = question.Module,
            QuestionText = question.QuestionText,
            GroundTruthAnswer = question.GroundTruthAnswer,
            SystemAnswer = systemAnswerText,
            CitationsCount = answer.Citations?.Count ?? 0,
            MatchedKeywords = matchedKeywords,
            MissingKeywords = missingKeywords,
            KeywordAccuracyPercent = Math.Round(accuracy, 1),
            HasCitations = (answer.Citations?.Count ?? 0) > 0,
            ExecutionTimeMs = stopwatch.ElapsedMilliseconds
        };
    }

    public async Task<EvaluationReportSummary> RunFullEvaluationAsync(
        Guid userId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default)
    {
        var results = new List<EvaluationResult>();
        var overallStopwatch = Stopwatch.StartNew();

        foreach (var q in _questions)
        {
            try
            {
                var res = await EvaluateQuestionAsync(userId, q.Id, subjectId, cancellationToken);
                results.Add(res);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to evaluate question ID {QuestionId}", q.Id);
                results.Add(new EvaluationResult
                {
                    QuestionId = q.Id,
                    Module = q.Module,
                    QuestionText = q.QuestionText,
                    GroundTruthAnswer = q.GroundTruthAnswer,
                    SystemAnswer = $"[Error] {ex.Message}",
                    KeywordAccuracyPercent = 0,
                    HasCitations = false,
                    ExecutionTimeMs = 0
                });
            }
        }

        overallStopwatch.Stop();

        int withCitations = results.Count(r => r.HasCitations);
        double avgAccuracy = results.Count > 0 ? results.Average(r => r.KeywordAccuracyPercent) : 0;
        double citationCoverage = results.Count > 0 ? (double)withCitations / results.Count * 100.0 : 0;

        return new EvaluationReportSummary
        {
            EvaluatedAtUtc = DateTime.UtcNow,
            TotalQuestions = _questions.Count,
            EvaluatedQuestions = results.Count,
            AverageKeywordAccuracyPercent = Math.Round(avgAccuracy, 1),
            QuestionsWithCitationsCount = withCitations,
            CitationCoveragePercent = Math.Round(citationCoverage, 1),
            TotalDurationMs = overallStopwatch.ElapsedMilliseconds,
            Results = results
        };
    }

    private List<EvaluationQuestion> LoadEmbeddedDataset()
    {
        try
        {
            var assemblyLocation = AppDomain.CurrentDomain.BaseDirectory;
            var jsonPath = Path.Combine(assemblyLocation, "Infrastructure", "Data", "evaluation_dataset_50.json");

            if (!File.Exists(jsonPath))
            {
                // Fallback to project root directory
                jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "Infrastructure", "Data", "evaluation_dataset_50.json");
            }

            if (File.Exists(jsonPath))
            {
                var content = File.ReadAllText(jsonPath);
                var items = JsonSerializer.Deserialize<List<EvaluationQuestion>>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });
                if (items != null && items.Count > 0)
                {
                    return items;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not load evaluation_dataset_50.json from disk.");
        }

        return new List<EvaluationQuestion>();
    }
}
