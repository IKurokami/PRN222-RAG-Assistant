using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Pages.Evaluation;

[Authorize]
public class IndexModel(
    IEvaluationService evaluationService,
    ISubjectCatalogService subjectCatalogService,
    UserManager<ApplicationUser> userManager,
    ILogger<IndexModel> logger) : PageModel
{
    public IReadOnlyList<EvaluationQuestion> Questions { get; set; } = Array.Empty<EvaluationQuestion>();
    public List<Subject> Subjects { get; set; } = new();
    public Guid? SelectedSubjectId { get; set; }

    public async Task<IActionResult> OnGetAsync(CancellationToken cancellationToken)
    {
        var questions = evaluationService.GetQuestions();
        var subjects = (await subjectCatalogService.GetSubjectsAsync(
                activeOnly: true,
                cancellationToken))
            .ToList();

        var datasetSubjectCodes = questions
            .Select(question => question.SubjectCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        Questions = questions;
        Subjects = subjects;
        SelectedSubjectId = subjects
            .FirstOrDefault(subject => datasetSubjectCodes.Contains(subject.Code))?.Id
            ?? subjects.FirstOrDefault()?.Id;

        return Page();
    }

    public async Task<IActionResult> OnPostRunSingleAsync(
        int questionId,
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var question = evaluationService.GetQuestions().FirstOrDefault(candidate => candidate.Id == questionId)
                ?? throw new ArgumentException(
                    $"Evaluation question with ID '{questionId}' not found.",
                    nameof(questionId));

            var effectiveSubjectId = await ResolveEvaluationSubjectIdAsync(
                question.SubjectCode,
                subjectId,
                cancellationToken);

            var result = await evaluationService.EvaluateQuestionAsync(
                user.Id,
                questionId,
                effectiveSubjectId,
                cancellationToken);

            return new JsonResult(new { success = true, result });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Single evaluation failed for question {QuestionId}", questionId);
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    public async Task<IActionResult> OnPostRunAllAsync(
        Guid? subjectId,
        CancellationToken cancellationToken)
    {
        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Unauthorized();
        }

        try
        {
            var subjectCodes = evaluationService.GetQuestions()
                .Select(question => question.SubjectCode)
                .Where(code => !string.IsNullOrWhiteSpace(code))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

            if (subjectCodes.Count != 1)
            {
                throw new InvalidOperationException(
                    "Full evaluation requires all questions to target exactly one subject code.");
            }

            var effectiveSubjectId = await ResolveEvaluationSubjectIdAsync(
                subjectCodes[0],
                subjectId,
                cancellationToken);

            var report = await evaluationService.RunFullEvaluationAsync(
                user.Id,
                effectiveSubjectId,
                cancellationToken);

            return new JsonResult(new { success = true, report });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Full evaluation suite failed");
            return new JsonResult(new { success = false, message = ex.Message });
        }
    }

    private async Task<Guid> ResolveEvaluationSubjectIdAsync(
        string subjectCode,
        Guid? requestedSubjectId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(subjectCode))
        {
            throw new InvalidOperationException("Evaluation question does not define a subject code.");
        }

        var activeSubjects = await subjectCatalogService.GetSubjectsAsync(
            activeOnly: true,
            cancellationToken);

        if (requestedSubjectId.HasValue)
        {
            var requestedSubject = activeSubjects.FirstOrDefault(
                    subject => subject.Id == requestedSubjectId.Value)
                ?? throw new InvalidOperationException(
                    "The requested evaluation subject is missing or inactive.");

            if (!string.Equals(
                    requestedSubject.Code,
                    subjectCode,
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Evaluation dataset targets subject '{subjectCode}', but subject '{requestedSubject.Code}' was requested.");
            }

            return requestedSubject.Id;
        }

        var resolvedSubject = activeSubjects.FirstOrDefault(subject =>
            string.Equals(subject.Code, subjectCode, StringComparison.OrdinalIgnoreCase));

        return resolvedSubject?.Id
            ?? throw new InvalidOperationException(
                $"No active subject with code '{subjectCode}' exists for this evaluation dataset.");
    }
}
