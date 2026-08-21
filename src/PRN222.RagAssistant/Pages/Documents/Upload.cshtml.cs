using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using PRN222.RagAssistant.Application.Abstractions;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Documents;
using PRN222.RagAssistant.Security;

namespace PRN222.RagAssistant.Pages.Documents;

[Authorize(Policy = AppPolicies.ManageDocuments)]
public class UploadModel(
    ISubjectCatalogService subjectCatalogService,
    IChapterManagementService chapterManagementService,
    IDocumentManagementService documentManagementService,
    ISubjectAccessService subjectAccessService,
    UserManager<ApplicationUser> userManager,
    ILogger<UploadModel> logger) : PageModel
{
    [BindProperty]
    public Guid SubjectId { get; set; }

    public string SubjectCode { get; set; } = string.Empty;
    public string SubjectName { get; set; } = string.Empty;
    public List<SelectListItem> ChapterOptions { get; set; } = new();

    [BindProperty]
    public DocumentUploadInputModel Input { get; set; } = new();

    public async Task<IActionResult> OnGetAsync(Guid subjectId, CancellationToken cancellationToken)
    {
        if (!await subjectAccessService.CanManageSubjectAsync(User, subjectId, cancellationToken))
        {
            return Forbid();
        }

        SubjectId = subjectId;
        if (!await PopulateMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        return Page();
    }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        if (!await subjectAccessService.CanManageSubjectAsync(User, SubjectId, cancellationToken))
        {
            return Forbid();
        }

        if (!await PopulateMetadataAsync(cancellationToken))
        {
            return NotFound();
        }

        if (!ModelState.IsValid)
        {
            return Page();
        }

        if (Input.ChapterId.HasValue
            && ChapterOptions.All(option => option.Value != Input.ChapterId.Value.ToString()))
        {
            ModelState.AddModelError(
                "Input.ChapterId",
                "Chương được chọn không hợp lệ hoặc không thuộc môn học này.");
            return Page();
        }

        var user = await userManager.GetUserAsync(User);
        if (user is null)
        {
            return Challenge();
        }

        var file = Input.File;
        if (file is null || file.Length == 0)
        {
            ModelState.AddModelError("Input.File", "File tải lên không hợp lệ.");
            return Page();
        }

        var request = new DocumentCreateRequest(
            SubjectId,
            Input.ChapterId,
            user.Id,
            Input.Title.Trim(),
            file.FileName,
            file.ContentType ?? "application/octet-stream",
            Path.GetExtension(file.FileName),
            file.Length);

        try
        {
            await using var stream = file.OpenReadStream();
            var document = await documentManagementService.CreateDocumentAsync(
                request,
                stream,
                cancellationToken);

            TempData["StatusMessage"] =
                $"Đã tải thành công tài liệu '{document.Title}'. File đã được thêm vào hàng chờ index.";
            return RedirectToPage("/Documents/Index", new { subjectId = document.SubjectId });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to upload document for subject {SubjectId}.", SubjectId);
            ModelState.AddModelError(
                string.Empty,
                "Lưu thông tin tài liệu thất bại. Vui lòng thử lại.");
            return Page();
        }
    }

    private async Task<bool> PopulateMetadataAsync(CancellationToken cancellationToken)
    {
        var subject = await subjectCatalogService.GetSubjectAsync(
            SubjectId,
            cancellationToken: cancellationToken);

        if (subject is null)
        {
            return false;
        }

        SubjectCode = subject.Code;
        SubjectName = subject.Name;

        var chapters = await chapterManagementService.GetChaptersAsync(SubjectId, cancellationToken);
        ChapterOptions = chapters.Select(chapter => new SelectListItem
        {
            Value = chapter.Id.ToString(),
            Text = $"Chương {chapter.Number}: {chapter.Title}"
        }).ToList();

        return true;
    }
}
