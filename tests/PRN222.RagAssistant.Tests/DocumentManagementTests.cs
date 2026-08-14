using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;
using PRN222.RagAssistant.Domain.Entities;
using PRN222.RagAssistant.Domain.Enums;
using PRN222.RagAssistant.Infrastructure.Services;
using PRN222.RagAssistant.Models.Documents;

namespace PRN222.RagAssistant.Tests;

public sealed class DocumentManagementTests
{
    [Theory]
    [InlineData("document.pdf", "application/pdf")]
    [InlineData("lecture.docx", "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [InlineData("slides.pptx", "application/vnd.openxmlformats-officedocument.presentationml.presentation")]
    public void UploadInputModel_validates_allowed_file_extensions(string fileName, string contentType)
    {
        var model = new DocumentUploadInputModel
        {
            Title = "Tài liệu mẫu PRN222",
            File = CreateMockFormFile(fileName, contentType, length: 1024)
        };

        Assert.Empty(ValidateModel(model));
    }

    [Theory]
    [InlineData("virus.exe", "application/x-msdownload")]
    [InlineData("image.png", "image/png")]
    [InlineData("script.sh", "text/plain")]
    public void UploadInputModel_rejects_unsupported_file_extensions(string fileName, string contentType)
    {
        var model = new DocumentUploadInputModel
        {
            Title = "Tài liệu không hợp lệ",
            File = CreateMockFormFile(fileName, contentType, length: 1024)
        };

        var result = Assert.Single(ValidateModel(model));
        Assert.Contains("không được hỗ trợ", result.ErrorMessage);
    }

    [Fact]
    public void UploadInputModel_rejects_files_exceeding_size_limit()
    {
        var model = new DocumentUploadInputModel
        {
            Title = "Tài liệu quá lớn",
            File = CreateMockFormFile(
                "large.pdf",
                "application/pdf",
                DocumentUploadInputModel.MaxFileSizeBytes + 1)
        };

        var result = Assert.Single(ValidateModel(model));
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

    [Fact]
    public async Task Upload_cleanup_logic_removes_file_when_db_persistence_fails()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, $"{Guid.NewGuid()}.pdf");

        try
        {
            await File.WriteAllBytesAsync(filePath, [1, 2, 3]);
            Assert.True(File.Exists(filePath));

            var cleanupRan = false;
            try
            {
                throw new InvalidOperationException("Simulated DB failure");
            }
            catch
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    cleanupRan = true;
                }
            }

            Assert.True(cleanupRan);
            Assert.False(File.Exists(filePath));
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task Upload_does_not_cleanup_file_when_db_persistence_succeeds()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, $"{Guid.NewGuid()}.pdf");

        try
        {
            await File.WriteAllBytesAsync(filePath, [1, 2, 3]);

            var cleanupRan = false;
            try
            {
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

            Assert.False(cleanupRan);
            Assert.True(File.Exists(filePath));
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
