using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
public sealed class ChaptersController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;

    public ChaptersController(ApplicationDbContext dbContext, IAuthorizationService authorizationService)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, AppPolicies.ManageDocuments);

        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(c => c.SubjectId == SeedData.Prn222SubjectId)
            .OrderBy(c => c.Number)
            .ToListAsync(cancellationToken);

        var chapterIds = chapters.Select(c => c.Id).ToList();

        var docCounts = await _dbContext.Documents
            .AsNoTracking()
            .Where(d => d.SubjectId == SeedData.Prn222SubjectId
                        && d.ChapterId.HasValue
                        && chapterIds.Contains(d.ChapterId.Value))
            .GroupBy(d => d.ChapterId!.Value)
            .Select(g => new { ChapterId = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken);

        var countMap = docCounts.ToDictionary(x => x.ChapterId, x => x.Count);

        var viewModel = new ChapterIndexViewModel
        {
            CanManageDocuments = authResult.Succeeded,
            StatusMessage = TempData["StatusMessage"] as string,
            Chapters = chapters.Select(c => new ChapterItemViewModel
            {
                Id = c.Id,
                Number = c.Number,
                Title = c.Title,
                DocumentCount = countMap.TryGetValue(c.Id, out var count) ? count : 0
            }).ToList()
        };

        return View(viewModel);
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public IActionResult Create() => View(new ChapterCreateViewModel());

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Create(ChapterCreateViewModel viewModel, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var duplicateExists = await _dbContext.Chapters
            .AnyAsync(
                c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == viewModel.Input.Number!.Value,
                cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {viewModel.Input.Number} đã tồn tại trong môn PRN222.");
            return View(viewModel);
        }

        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            SubjectId = SeedData.Prn222SubjectId,
            Number = viewModel.Input.Number!.Value,
            Title = viewModel.Input.Title.Trim()
        };

        _dbContext.Chapters.Add(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã tạo chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId,
                cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        return View(new ChapterEditViewModel
        {
            Id = chapter.Id,
            OriginalNumber = chapter.Number,
            OriginalTitle = chapter.Title,
            Input = new ChapterInputModel
            {
                Number = chapter.Number,
                Title = chapter.Title
            }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(Guid id, ChapterEditViewModel viewModel, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .FirstOrDefaultAsync(
                c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId,
                cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        viewModel.Id = id;
        viewModel.OriginalNumber = chapter.Number;
        viewModel.OriginalTitle = chapter.Title;

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var duplicateExists = await _dbContext.Chapters
            .AnyAsync(
                c => c.SubjectId == SeedData.Prn222SubjectId
                     && c.Number == viewModel.Input.Number!.Value
                     && c.Id != id,
                cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {viewModel.Input.Number} đã tồn tại trong môn PRN222.");
            return View(viewModel);
        }

        chapter.Number = viewModel.Input.Number!.Value;
        chapter.Title = viewModel.Input.Title.Trim();

        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(
                c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId,
                cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        var affectedDocumentCount = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(d => d.ChapterId == id, cancellationToken);

        return View(new ChapterDeleteViewModel
        {
            Id = chapter.Id,
            ChapterNumber = chapter.Number,
            ChapterTitle = chapter.Title,
            AffectedDocumentCount = affectedDocumentCount
        });
    }

    [HttpPost]
    [ActionName("Delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> DeleteConfirmed(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .FirstOrDefaultAsync(
                c => c.Id == id && c.SubjectId == SeedData.Prn222SubjectId,
                cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affectedDocuments = await _dbContext.Documents
            .Where(d => d.ChapterId == id)
            .ToListAsync(cancellationToken);

        foreach (var document in affectedDocuments)
        {
            document.ChapterId = null;
        }

        _dbContext.Chapters.Remove(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        TempData["StatusMessage"] = affectedDocuments.Count > 0
            ? $"Đã xóa chương {chapter.Number}: {chapter.Title}. {affectedDocuments.Count} tài liệu liên quan đã được bỏ gán chương (tài liệu vẫn còn trong hệ thống)."
            : $"Đã xóa chương {chapter.Number}: {chapter.Title} thành công.";

        return RedirectToAction(nameof(Index));
    }
}
