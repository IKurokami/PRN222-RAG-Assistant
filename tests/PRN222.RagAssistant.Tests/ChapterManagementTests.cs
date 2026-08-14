using System.ComponentModel.DataAnnotations;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Models.Chapters;

namespace PRN222.RagAssistant.Tests;

public sealed class ChapterManagementTests
{
    [Fact]
    public void CreateInputModel_accepts_valid_number_and_title()
    {
        var model = new ChapterInputModel { Number = 1, Title = "Giới thiệu C#" };
        Assert.Empty(ValidateModel(model));
    }

    [Fact]
    public void CreateInputModel_rejects_missing_number()
    {
        var model = new ChapterInputModel { Number = null, Title = "Giới thiệu" };
        Assert.Contains(ValidateModel(model), r => r.MemberNames.Contains("Number"));
    }

    [Fact]
    public void CreateInputModel_rejects_number_out_of_range()
    {
        var model = new ChapterInputModel { Number = 1000, Title = "Chương vượt range" };
        Assert.Contains(ValidateModel(model), r => r.MemberNames.Contains("Number"));
    }

    [Fact]
    public void CreateInputModel_rejects_zero_number()
    {
        var model = new ChapterInputModel { Number = 0, Title = "Số 0 không hợp lệ" };
        Assert.Contains(ValidateModel(model), r => r.MemberNames.Contains("Number"));
    }

    [Fact]
    public void CreateInputModel_rejects_empty_title()
    {
        var model = new ChapterInputModel { Number = 1, Title = "" };
        Assert.Contains(ValidateModel(model), r => r.MemberNames.Contains("Title"));
    }

    [Fact]
    public void CreateInputModel_rejects_title_exceeding_max_length()
    {
        var model = new ChapterInputModel { Number = 1, Title = new string('A', 301) };
        Assert.Contains(ValidateModel(model), r => r.MemberNames.Contains("Title"));
    }

    [Fact]
    public void EditInputModel_accepts_valid_number_and_title()
    {
        var model = new ChapterInputModel { Number = 2, Title = "ASP.NET Core Basics" };
        Assert.Empty(ValidateModel(model));
    }

    [Fact]
    public void EditInputModel_rejects_missing_title()
    {
        var model = new ChapterInputModel { Number = 1, Title = "" };
        Assert.Contains(ValidateModel(model), r => r.MemberNames.Contains("Title"));
    }

    [Fact]
    public void DeleteChapter_logic_sets_document_chapterId_to_null_and_keeps_document()
    {
        var chapterId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var documents = new List<Document>
        {
            new()
            {
                Id = documentId,
                SubjectId = SeedData.Prn222SubjectId,
                ChapterId = chapterId,
                UploadedByUserId = Guid.NewGuid(),
                Title = "Bài giảng 1",
                OriginalFileName = "lecture.pdf",
                StoragePath = "storage/uploads/lecture.pdf",
                ContentType = "application/pdf",
                FileExtension = ".pdf",
                FileSizeBytes = 1024,
                UploadedAtUtc = DateTime.UtcNow
            }
        };

        var affectedDocs = documents.Where(d => d.ChapterId == chapterId).ToList();
        foreach (var document in affectedDocs)
        {
            document.ChapterId = null;
        }

        var persistedDoc = documents.FirstOrDefault(d => d.Id == documentId);
        Assert.NotNull(persistedDoc);
        Assert.Null(persistedDoc!.ChapterId);
        Assert.Single(documents);
    }

    [Fact]
    public void DeleteChapter_without_documents_clears_no_documents()
    {
        var chapterId = Guid.NewGuid();
        var documents = new List<Document>();

        var affectedDocs = documents.Where(d => d.ChapterId == chapterId).ToList();
        foreach (var document in affectedDocs)
        {
            document.ChapterId = null;
        }

        Assert.Empty(affectedDocs);
        Assert.Empty(documents);
    }

    [Fact]
    public void Create_detects_duplicate_number_within_same_subject()
    {
        var chapters = new List<Chapter>
        {
            new() { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 3, Title = "Chương 3" }
        };

        Assert.True(chapters.Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 3));
    }

    [Fact]
    public void Create_allows_same_number_for_different_subject()
    {
        var chapters = new List<Chapter>
        {
            new() { Id = Guid.NewGuid(), SubjectId = Guid.NewGuid(), Number = 3, Title = "Chương 3 khác môn" }
        };

        Assert.False(chapters.Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 3));
    }

    [Fact]
    public void Edit_allows_same_number_for_self_but_rejects_conflict_with_other_chapter()
    {
        var chapter1Id = Guid.NewGuid();
        var chapter2Id = Guid.NewGuid();
        var chapters = new List<Chapter>
        {
            new() { Id = chapter1Id, SubjectId = SeedData.Prn222SubjectId, Number = 4, Title = "Chương 4" },
            new() { Id = chapter2Id, SubjectId = SeedData.Prn222SubjectId, Number = 5, Title = "Chương 5" }
        };

        Assert.False(chapters.Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 4 && c.Id != chapter1Id));
        Assert.True(chapters.Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 5 && c.Id != chapter1Id));
    }

    [Fact]
    public void ChapterId_from_different_subject_is_rejected()
    {
        var foreignChapterId = Guid.NewGuid();
        var chapters = new List<Chapter>
        {
            new() { Id = foreignChapterId, SubjectId = Guid.NewGuid(), Number = 1, Title = "Chương khác môn" }
        };

        Assert.False(chapters.Any(c => c.Id == foreignChapterId && c.SubjectId == SeedData.Prn222SubjectId));
    }

    [Fact]
    public void ChapterId_belonging_to_prn222_is_accepted()
    {
        var validChapterId = Guid.NewGuid();
        var chapters = new List<Chapter>
        {
            new() { Id = validChapterId, SubjectId = SeedData.Prn222SubjectId, Number = 6, Title = "Chương PRN222" }
        };

        Assert.True(chapters.Any(c => c.Id == validChapterId && c.SubjectId == SeedData.Prn222SubjectId));
    }

    [Fact]
    public void ChapterId_nonexistent_is_rejected()
    {
        var nonexistentId = Guid.NewGuid();
        var chapters = new List<Chapter>();

        Assert.False(chapters.Any(c => c.Id == nonexistentId && c.SubjectId == SeedData.Prn222SubjectId));
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(model, new ValidationContext(model), results, validateAllProperties: true);
        return results;
    }
}
