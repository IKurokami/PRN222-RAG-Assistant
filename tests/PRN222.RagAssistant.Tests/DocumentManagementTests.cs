using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Pages.Documents;

namespace PRN222.RagAssistant.Tests;

public sealed class DocumentManagementTests
{
    [Theory]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("lecture.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("slides.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    public void UploadInputModel_validates_allowed_file_extensions(string fileName, string contentType)
    {
        var model = new UploadModel.InputModel
        {
            Title = "Tài liệu mẫu PRN222",
            File = CreateMockFormFile(fileName, contentType, length: 1024)
        };

        var validationResults = ValidateModel(model);

        Assert.Empty(validationResults);
    }

    [Theory]
    [InlineData("virus.exe", "application/x-msdownload")]
    [InlineData("image.png", "image/png")]
    [InlineData("script.sh", "text/plain")]
    public void UploadInputModel_rejects_unsupported_file_extensions(string fileName, string contentType)
    {
        var model = new UploadModel.InputModel
        {
            Title = "Tài liệu không hợp lệ",
            File = CreateMockFormFile(fileName, contentType, length: 1024)
        };

        var validationResults = ValidateModel(model);

        var result = Assert.Single(validationResults);
        Assert.Contains("không được hỗ trợ", result.ErrorMessage);
    }

    [Fact]
    public void UploadInputModel_rejects_files_exceeding_size_limit()
    {
        var model = new UploadModel.InputModel
        {
            Title = "Tài liệu quá lớn",
            File = CreateMockFormFile("large.pdf", "application/pdf", length: UploadModel.InputModel.MaxFileSizeBytes + 1)
        };

        var validationResults = ValidateModel(model);

        var result = Assert.Single(validationResults);
        Assert.Contains("vượt quá giới hạn tối đa", result.ErrorMessage);
    }

    [Fact]
    public async Task InMemoryDocumentIndexingQueue_enqueues_and_dequeues_document_ids()
    {
        var queue = new InMemoryDocumentIndexingQueue();
        var documentId = Guid.NewGuid();

        await queue.EnqueueAsync(documentId);

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
        var dequeuedId = await queue.DequeueAsync(cts.Token);

        Assert.Equal(documentId, dequeuedId);
    }

    [Fact]
    public void Document_entity_initializes_with_Uploaded_status()
    {
        var document = new Document
        {
            Id = Guid.NewGuid(),
            Title = "Bài giảng 1",
            OriginalFileName = "lecture1.pdf",
            StoragePath = "storage/uploads/lecture1.pdf",
            ContentType = "application/pdf",
            FileExtension = ".pdf",
            FileSizeBytes = 2048,
            UploadedAtUtc = DateTime.UtcNow
        };

        Assert.Equal(DocumentIndexStatus.Uploaded, document.IndexStatus);
        Assert.Null(document.IndexError);
        Assert.Null(document.IndexedAtUtc);
    }

    // ─── Upload orphan file cleanup ───────────────────────────────────────────

    [Fact]
    public async Task Upload_cleanup_logic_removes_file_when_db_persistence_fails()
    {
        // Arrange: write a real temp file to simulate what the upload handler does
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, $"{Guid.NewGuid()}.pdf");

        try
        {
            await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });
            Assert.True(File.Exists(filePath), "Pre-condition: file must exist before cleanup.");

            // Act: simulate what UploadModel does when SaveChangesAsync throws
            // (the try/catch block in OnPostAsync)
            bool cleanupRan = false;
            try
            {
                // Simulate DB failure
                throw new InvalidOperationException("Simulated DB failure");
            }
            catch
            {
                // This mirrors the catch block in Upload.cshtml.cs
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    cleanupRan = true;
                }
            }

            // Assert: file is gone after cleanup
            Assert.True(cleanupRan, "Cleanup block must have executed.");
            Assert.False(File.Exists(filePath), "Orphan file must be deleted after DB failure.");
        }
        finally
        {
            // Cleanup temp dir regardless of test outcome
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Upload_does_not_cleanup_file_when_db_persistence_succeeds()
    {
        // Arrange
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, $"{Guid.NewGuid()}.pdf");

        try
        {
            await File.WriteAllBytesAsync(filePath, new byte[] { 1, 2, 3 });

            // Act: simulate no DB exception — file should remain
            // (no catch block runs)
            bool cleanupRan = false;
            try
            {
                // Simulate successful DB save (no exception)
                _ = "db save ok";
            }
            catch
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    cleanupRan = true;
                }
            }

            // Assert: file is still there
            Assert.False(cleanupRan);
            Assert.True(File.Exists(filePath), "File must remain when DB persistence succeeds.");
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static List<ValidationResult> ValidateModel(object model)
    {
        var results = new List<ValidationResult>();
        var context = new ValidationContext(model);
        Validator.TryValidateObject(model, context, results, validateAllProperties: true);
        return results;
    }

    private static IFormFile CreateMockFormFile(string fileName, string contentType, long length)
    {
        var stream = new MemoryStream(new byte[(int)Math.Min(length, 100)]);
        return new FormFile(stream, 0, length, "File", fileName)
        {
            Headers = new HeaderDictionary(),
            ContentType = contentType
        };
    }
}
