using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Application.Models;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
public sealed class EvaluationController : Controller
{
    private readonly IEvaluationService _evaluationService;
    private readonly ApplicationDbContext _dbContext;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILogger<EvaluationController> _logger;

    public EvaluationController(
        IEvaluationService evaluationService,
        ApplicationDbContext dbContext,
        UserManager<ApplicationUser> userManager,
        ILogger<EvaluationController> logger)
    {
        _evaluationService = evaluationService;
        _dbContext = dbContext;
        _userManager = userManager;
        _logger = logger;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var questions = _evaluationService.GetQuestions();
        var subjects = await _dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.IsActive)
            .OrderBy(s => s.Code)
            .ToListAsync(cancellationToken);

        var datasetSubjectCodes = questions
            .Select(q => q.SubjectCode)
            .Where(code => !string.IsNullOrWhiteSpace(code))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var viewModel = new EvaluationIndexViewModel
        {
            Questions = questions,
            Subjects = subjects,
            SelectedSubjectId = subjects
                .FirstOrDefault(s => datasetSubjectCodes.Contains(s.Code))?.Id
                ?? subjects.FirstOrDefault()?.Id
        };

        return View(viewModel);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunSingle(int questionId, Guid? subjectId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            var question = _evaluationService.GetQuestions().FirstOrDefault(q => q.Id == questionId)
                ?? throw new ArgumentException($"Evaluation question with ID '{questionId}' not found.", nameof(questionId));

            var effectiveSubjectId = await ResolveEvaluationSubjectIdAsync(
                question.SubjectCode,
                subjectId,
                cancellationToken);

            var result = await _evaluationService.EvaluateQuestionAsync(
                user.Id,
                questionId,
                effectiveSubjectId,
                cancellationToken);

            return Json(new { success = true, result });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Single evaluation failed for question {QuestionId}", questionId);
            return Json(new { success = false, message = ex.Message });
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RunAll(Guid? subjectId, CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user == null)
        {
            return Unauthorized();
        }

        try
        {
            var subjectCodes = _evaluationService.GetQuestions()
                .Select(q => q.SubjectCode)
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

            var report = await _evaluationService.RunFullEvaluationAsync(
                user.Id,
                effectiveSubjectId,
                cancellationToken);

            return Json(new { success = true, report });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full evaluation suite failed");
            return Json(new { success = false, message = ex.Message });
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

        var activeSubjects = _dbContext.Subjects
            .AsNoTracking()
            .Where(s => s.IsActive);

        if (requestedSubjectId.HasValue)
        {
            var requestedSubject = await activeSubjects
                .FirstOrDefaultAsync(s => s.Id == requestedSubjectId.Value, cancellationToken)
                ?? throw new InvalidOperationException("The requested evaluation subject is missing or inactive.");

            if (!string.Equals(requestedSubject.Code, subjectCode, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Evaluation dataset targets subject '{subjectCode}', but subject '{requestedSubject.Code}' was requested.");
            }

            return requestedSubject.Id;
        }

        var resolvedSubjectId = await activeSubjects
            .Where(s => s.Code == subjectCode)
            .Select(s => (Guid?)s.Id)
            .FirstOrDefaultAsync(cancellationToken);

        return resolvedSubjectId
            ?? throw new InvalidOperationException(
                $"No active subject with code '{subjectCode}' exists for this evaluation dataset.");
    }
}

public sealed class EvaluationIndexViewModel
{
    public IReadOnlyList<EvaluationQuestion> Questions { get; set; } = Array.Empty<EvaluationQuestion>();
    public List<Subject> Subjects { get; set; } = new();
    public Guid? SelectedSubjectId { get; set; }
}
