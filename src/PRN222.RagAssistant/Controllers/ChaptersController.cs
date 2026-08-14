using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Chapters;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Controllers;

[Authorize]
public sealed class ChaptersController : Controller
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ISubjectAccessService _subjectAccessService;

    public ChaptersController(
        ApplicationDbContext dbContext,
        ISubjectAccessService subjectAccessService)
    {
        _dbContext = dbContext;
        _subjectAccessService = subjectAccessService;
    }

    [HttpGet]
    public async Task<IActionResult> Index(Guid subjectId, CancellationToken cancellationToken)
    {
        if (subjectId == Guid.Empty)
        {
            return RedirectToAction(nameof(SubjectsController.Index), "Subjects");
        }

        if (!await _subjectAccessService.CanViewSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await _dbContext.Subjects
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == subjectId, cancellationToken);

        if (subject is null)
        {
            return NotFound();
        }

        var chapters = await _dbContext.Chapters
            .AsNoTracking()
            .Where(chapter => chapter.SubjectId == subjectId)
            .OrderBy(chapter => chapter.Number)
            .ToListAsync(cancellationToken);

        var chapterIds = chapters.Select(chapter => chapter.Id).ToList();
        var docCounts = await _dbContext.Documents
            .AsNoTracking()
            .Where(document => document.SubjectId == subjectId
                               && document.ChapterId.HasValue
                               && chapterIds.Contains(document.ChapterId.Value))
            .GroupBy(document => document.ChapterId!.Value)
            .Select(group => new { ChapterId = group.Key, Count = group.Count() })
            .ToListAsync(cancellationToken);

        var countMap = docCounts.ToDictionary(item => item.ChapterId, item => item.Count);

        return View(new ChapterIndexViewModel
        {
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
            CanManageDocuments = await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken),
            StatusMessage = TempData["StatusMessage"] as string,
            Chapters = chapters.Select(chapter => new ChapterItemViewModel
            {
                Id = chapter.Id,
                Number = chapter.Number,
                Title = chapter.Title,
                DocumentCount = countMap.GetValueOrDefault(chapter.Id)
            }).ToList()
        });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Create(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        var viewModel = new ChapterCreateViewModel { SubjectId = subjectId };
        return await PopulateSubjectMetadataAsync(viewModel, cancellationToken)
            ? View(viewModel)
            : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Create(
        ChapterCreateViewModel viewModel,
        CancellationToken cancellationToken)
    {
        if (!await _subjectAccessService.CanManageSubjectAsync(User, viewModel.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        if (!await PopulateSubjectMetadataAsync(viewModel, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var duplicateExists = await _dbContext.Chapters.AnyAsync(
            chapter => chapter.SubjectId == viewModel.SubjectId
                       && chapter.Number == viewModel.Input.Number!.Value,
            cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {viewModel.Input.Number} đã tồn tại trong môn {viewModel.SubjectCode}.");
            return View(viewModel);
        }

        var chapter = new Chapter
        {
            Id = Guid.NewGuid(),
            SubjectId = viewModel.SubjectId,
            Number = viewModel.Input.Number!.Value,
            Title = viewModel.Input.Title.Trim()
        };

        _dbContext.Chapters.Add(chapter);
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã tạo chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToAction(nameof(Index), new { subjectId = chapter.SubjectId });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var viewModel = new ChapterEditViewModel
        {
            Id = chapter.Id,
            SubjectId = chapter.SubjectId,
            OriginalNumber = chapter.Number,
            OriginalTitle = chapter.Title,
            Input = new ChapterInputModel
            {
                Number = chapter.Number,
                Title = chapter.Title
            }
        };

        return await PopulateSubjectMetadataAsync(viewModel, cancellationToken)
            ? View(viewModel)
            : NotFound();
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Edit(
        Guid id,
        ChapterEditViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        viewModel.Id = id;
        viewModel.SubjectId = chapter.SubjectId;
        viewModel.OriginalNumber = chapter.Number;
        viewModel.OriginalTitle = chapter.Title;
        if (!await PopulateSubjectMetadataAsync(viewModel, cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return View(viewModel);
        }

        var duplicateExists = await _dbContext.Chapters.AnyAsync(
            candidate => candidate.SubjectId == chapter.SubjectId
                         && candidate.Number == viewModel.Input.Number!.Value
                         && candidate.Id != id,
            cancellationToken);

        if (duplicateExists)
        {
            ModelState.AddModelError("Input.Number", $"Chương số {viewModel.Input.Number} đã tồn tại trong môn {viewModel.SubjectCode}.");
            return View(viewModel);
        }

        chapter.Number = viewModel.Input.Number!.Value;
        chapter.Title = viewModel.Input.Title.Trim();
        await _dbContext.SaveChangesAsync(cancellationToken);

        TempData["StatusMessage"] = $"Đã cập nhật chương {chapter.Number}: {chapter.Title} thành công.";
        return RedirectToAction(nameof(Index), new { subjectId = chapter.SubjectId });
    }

    [HttpGet]
    [Authorize(Policy = AppPolicies.ManageDocuments)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var chapter = await _dbContext.Chapters
            .AsNoTracking()
            .FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);

        if (chapter is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == chapter.SubjectId, cancellationToken);
        if (subject is null)
        {
            return NotFound();
        }

        var affectedDocumentCount = await _dbContext.Documents
            .AsNoTracking()
            .CountAsync(document => document.SubjectId == chapter.SubjectId && document.ChapterId == id, cancellationToken);

        return View(new ChapterDeleteViewModel
        {
            Id = chapter.Id,
            SubjectId = subject.Id,
            SubjectCode = subject.Code,
            SubjectName = subject.Name,
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
        var chapter = await _dbContext.Chapters.FirstOrDefaultAsync(candidate => candidate.Id == id, cancellationToken);
        if (chapter is null)
        {
            return NotFound();
        }

        if (!await _subjectAccessService.CanManageSubjectAsync(User, chapter.SubjectId, cancellationToken))
        {
            return Forbid();
        }

        var subjectId = chapter.SubjectId;
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var affectedDocuments = await _dbContext.Documents
            .Where(document => document.SubjectId == subjectId && document.ChapterId == id)
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

        return RedirectToAction(nameof(Index), new { subjectId });
    }

    private async Task<bool> PopulateSubjectMetadataAsync(
        ChapterCreateViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == viewModel.SubjectId, cancellationToken);
        if (subject is null)
        {
            return false;
        }

        viewModel.SubjectCode = subject.Code;
        viewModel.SubjectName = subject.Name;
        return true;
    }

    private async Task<bool> PopulateSubjectMetadataAsync(
        ChapterEditViewModel viewModel,
        CancellationToken cancellationToken)
    {
        var subject = await _dbContext.Subjects.AsNoTracking().FirstOrDefaultAsync(candidate => candidate.Id == viewModel.SubjectId, cancellationToken);
        if (subject is null)
        {
            return false;
        }

        viewModel.SubjectCode = subject.Code;
        viewModel.SubjectName = subject.Name;
        return true;
    }
}
