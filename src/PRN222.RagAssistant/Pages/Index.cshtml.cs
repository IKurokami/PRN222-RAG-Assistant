using Microsoft.AspNetCore.Mvc.RazorPages;
using PRN222.RagAssistant.Application.Abstractions;

namespace PRN222.RagAssistant.Pages;

public class IndexModel(IHomePageService homePageService) : PageModel
{
    public int TotalChapters { get; set; }
    public int TotalDocuments { get; set; }
    public int IndexedDocuments { get; set; }
    public string SubjectCode { get; set; } = "PRN222";
    public string SubjectName { get; set; } = "Lập trình C# & Phát triển Ứng dụng AI (.NET 10)";

    public async Task OnGetAsync(CancellationToken cancellationToken)
    {
        var snapshot = await homePageService.GetSnapshotAsync(cancellationToken);

        TotalChapters = snapshot.TotalChapters;
        TotalDocuments = snapshot.TotalDocuments;
        IndexedDocuments = snapshot.IndexedDocuments;

        if (!string.IsNullOrWhiteSpace(snapshot.SubjectCode))
        {
            SubjectCode = snapshot.SubjectCode;
        }

        if (!string.IsNullOrWhiteSpace(snapshot.SubjectName))
        {
            SubjectName = snapshot.SubjectName;
        }
    }
}
