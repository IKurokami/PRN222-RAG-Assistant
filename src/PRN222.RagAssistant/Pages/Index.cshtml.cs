using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;

    public IndexModel(ApplicationDbContext db)
    {
        _db = db;
    }

    public int TotalChapters { get; set; }
    public int TotalDocuments { get; set; }
    public int IndexedDocuments { get; set; }
    public string SubjectCode { get; set; } = "PRN222";
    public string SubjectName { get; set; } = "Lập trình C# & Phát triển Ứng dụng AI (.NET 10)";

    public async Task OnGetAsync()
    {
        TotalChapters = await _db.Set<Chapter>().CountAsync();
        TotalDocuments = await _db.Set<Document>().CountAsync();
        IndexedDocuments = await _db.Set<Document>().CountAsync(d => d.IndexStatus == DocumentIndexStatus.Indexed);

        var subject = await _db.Set<Subject>().FirstOrDefaultAsync();
        if (subject != null)
        {
            SubjectCode = subject.Code;
            SubjectName = subject.Name;
        }
    }
}
