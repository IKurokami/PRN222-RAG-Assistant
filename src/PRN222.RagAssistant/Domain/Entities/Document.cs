using PRN222.RagAssistant.Domain.Enums;

namespace PRN222.RagAssistant.Domain.Entities;

public sealed class Document
{
    public Guid Id { get; set; }

    public Guid SubjectId { get; set; }

    public Guid? ChapterId { get; set; }

    public Guid UploadedByUserId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string StoragePath { get; set; } = string.Empty;

    public string ContentType { get; set; } = string.Empty;

    public string FileExtension { get; set; } = string.Empty;

    public long FileSizeBytes { get; set; }

    public DocumentIndexStatus IndexStatus { get; set; } = DocumentIndexStatus.Uploaded;

    public string? IndexError { get; set; }

    public DateTime UploadedAtUtc { get; set; }

    public DateTime? IndexedAtUtc { get; set; }
}
