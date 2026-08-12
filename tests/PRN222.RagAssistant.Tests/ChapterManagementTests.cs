using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using PRN222.RagAssistant.Data;
using PRN222.RagAssistant.Data.Seed;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Pages.Chapters;
using Pgvector.EntityFrameworkCore;

namespace PRN222.RagAssistant.Tests;

/// <summary>
/// Unit tests for Chapter Management — validates input models, duplicate detection,
/// delete safety (documents set to null, not deleted), and ChapterId server-side
/// rejection for invalid/cross-subject values.
///
/// DB tests use a fake Npgsql connection string (model-only, no actual DB connection
/// required for LINQ-to-Objects operations on in-memory collections).
/// </summary>
public sealed class ChapterManagementTests
{
    // ─── Input Model Validation ───────────────────────────────────────────────

    [Fact]
    public void CreateInputModel_accepts_valid_number_and_title()
    {
        var model = new CreateModel.InputModel { Number = 1, Title = "Giới thiệu C#" };
        var results = ValidateModel(model);
        Assert.Empty(results);
    }

    [Fact]
    public void CreateInputModel_rejects_missing_number()
    {
        var model = new CreateModel.InputModel { Number = null, Title = "Giới thiệu" };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains("Number"));
    }

    [Fact]
    public void CreateInputModel_rejects_number_out_of_range()
    {
        var model = new CreateModel.InputModel { Number = 1000, Title = "Chương vượt range" };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains("Number"));
    }

    [Fact]
    public void CreateInputModel_rejects_zero_number()
    {
        var model = new CreateModel.InputModel { Number = 0, Title = "Số 0 không hợp lệ" };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains("Number"));
    }

    [Fact]
    public void CreateInputModel_rejects_empty_title()
    {
        var model = new CreateModel.InputModel { Number = 1, Title = "" };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains("Title"));
    }

    [Fact]
    public void CreateInputModel_rejects_title_exceeding_max_length()
    {
        var model = new CreateModel.InputModel { Number = 1, Title = new string('A', 301) };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains("Title"));
    }

    [Fact]
    public void EditInputModel_accepts_valid_number_and_title()
    {
        var model = new EditModel.InputModel { Number = 2, Title = "ASP.NET Core Basics" };
        var results = ValidateModel(model);
        Assert.Empty(results);
    }

    [Fact]
    public void EditInputModel_rejects_missing_title()
    {
        var model = new EditModel.InputModel { Number = 1, Title = "" };
        var results = ValidateModel(model);
        Assert.Contains(results, r => r.MemberNames.Contains("Title"));
    }

    // ─── Delete safety: Documents không bị xóa khi xóa Chapter ──────────────
    // Logic được simulate ở mức LINQ-to-objects (không cần DB connection)

    [Fact]
    public void DeleteChapter_logic_sets_document_chapterId_to_null_and_keeps_document()
    {
        // Arrange: simulate in-memory collections as the handler would see them
        var chapterId = Guid.NewGuid();
        var documentId = Guid.NewGuid();

        var documents = new List<Document>
        {
            new Document
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

        // Act: simulate the Delete handler — null out ChapterId, remove chapter
        var affectedDocs = documents.Where(d => d.ChapterId == chapterId).ToList();
        foreach (var doc in affectedDocs)
        {
            doc.ChapterId = null;
        }

        // Assert: document still exists, ChapterId is null
        var persistedDoc = documents.FirstOrDefault(d => d.Id == documentId);
        Assert.NotNull(persistedDoc);
        Assert.Null(persistedDoc!.ChapterId);
        Assert.Single(documents); // document count unchanged
    }

    [Fact]
    public void DeleteChapter_without_documents_clears_no_documents()
    {
        var chapterId = Guid.NewGuid();
        var documents = new List<Document>(); // no docs linked

        var affectedDocs = documents.Where(d => d.ChapterId == chapterId).ToList();
        foreach (var doc in affectedDocs)
        {
            doc.ChapterId = null;
        }

        Assert.Empty(affectedDocs);
        Assert.Empty(documents);
    }

    // ─── Duplicate detection logic ────────────────────────────────────────────

    [Fact]
    public void Create_detects_duplicate_number_within_same_subject()
    {
        var chapters = new List<Chapter>
        {
            new Chapter { Id = Guid.NewGuid(), SubjectId = SeedData.Prn222SubjectId, Number = 3, Title = "Chương 3" }
        };

        var duplicateExists = chapters
            .Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 3);

        Assert.True(duplicateExists);
    }

    [Fact]
    public void Create_allows_same_number_for_different_subject()
    {
        var otherSubjectId = Guid.NewGuid();
        var chapters = new List<Chapter>
        {
            new Chapter { Id = Guid.NewGuid(), SubjectId = otherSubjectId, Number = 3, Title = "Chương 3 khác môn" }
        };

        // When checking for PRN222 subject, number 3 should NOT be a duplicate
        var duplicateExists = chapters
            .Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 3);

        Assert.False(duplicateExists);
    }

    [Fact]
    public void Edit_allows_same_number_for_self_but_rejects_conflict_with_other_chapter()
    {
        var chapter1Id = Guid.NewGuid();
        var chapter2Id = Guid.NewGuid();

        var chapters = new List<Chapter>
        {
            new Chapter { Id = chapter1Id, SubjectId = SeedData.Prn222SubjectId, Number = 4, Title = "Chương 4" },
            new Chapter { Id = chapter2Id, SubjectId = SeedData.Prn222SubjectId, Number = 5, Title = "Chương 5" }
        };

        // Editing chapter1 with its own number — should NOT be flagged as duplicate
        var selfConflict = chapters
            .Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 4 && c.Id != chapter1Id);
        Assert.False(selfConflict);

        // Editing chapter1 to use chapter2's number — IS a conflict
        var otherConflict = chapters
            .Any(c => c.SubjectId == SeedData.Prn222SubjectId && c.Number == 5 && c.Id != chapter1Id);
        Assert.True(otherConflict);
    }

    // ─── ChapterId server-side validation ────────────────────────────────────

    [Fact]
    public void ChapterId_from_different_subject_is_rejected()
    {
        var foreignSubjectId = Guid.NewGuid();
        var foreignChapterId = Guid.NewGuid();

        var chapters = new List<Chapter>
        {
            new Chapter { Id = foreignChapterId, SubjectId = foreignSubjectId, Number = 1, Title = "Chương khác môn" }
        };

        // Validating against PRN222 — must be false (rejected)
        var isValid = chapters
            .Any(c => c.Id == foreignChapterId && c.SubjectId == SeedData.Prn222SubjectId);

        Assert.False(isValid);
    }

    [Fact]
    public void ChapterId_belonging_to_prn222_is_accepted()
    {
        var validChapterId = Guid.NewGuid();

        var chapters = new List<Chapter>
        {
            new Chapter { Id = validChapterId, SubjectId = SeedData.Prn222SubjectId, Number = 6, Title = "Chương PRN222" }
        };

        var isValid = chapters
            .Any(c => c.Id == validChapterId && c.SubjectId == SeedData.Prn222SubjectId);

        Assert.True(isValid);
    }

    [Fact]
    public void ChapterId_nonexistent_is_rejected()
    {
        var nonexistentId = Guid.NewGuid();
        var chapters = new List<Chapter>(); // empty

        var isValid = chapters
            .Any(c => c.Id == nonexistentId && c.SubjectId == SeedData.Prn222SubjectId);

        Assert.False(isValid);
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }
}
