using PRN222.RagAssistant.Application.Models;

namespace PRN222.RagAssistant.Application.Abstractions;

public interface IEvaluationService
{
    IReadOnlyList<EvaluationQuestion> GetQuestions();
    
    Task<EvaluationResult> EvaluateQuestionAsync(
        Guid userId,
        int questionId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);

    Task<EvaluationReportSummary> RunFullEvaluationAsync(
        Guid userId,
        Guid? subjectId = null,
        CancellationToken cancellationToken = default);
}
