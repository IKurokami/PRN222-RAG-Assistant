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

        var viewModel = new EvaluationIndexViewModel
        {
            Questions = questions,
            Subjects = subjects,
            SelectedSubjectId = subjects.FirstOrDefault()?.Id
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
            var result = await _evaluationService.EvaluateQuestionAsync(user.Id, questionId, subjectId, cancellationToken);
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
            var report = await _evaluationService.RunFullEvaluationAsync(user.Id, subjectId, cancellationToken);
            return Json(new { success = true, report });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Full evaluation suite failed");
            return Json(new { success = false, message = ex.Message });
        }
    }
}

public sealed class EvaluationIndexViewModel
{
    public IReadOnlyList<EvaluationQuestion> Questions { get; set; } = Array.Empty<EvaluationQuestion>();
    public List<Subject> Subjects { get; set; } = new();
    public Guid? SelectedSubjectId { get; set; }
}
