# Flow 1 MVC architecture

> Synchronized with the post-PR #40 baseline on 2026-08-21.

## Status

Flow 1 presentation is MVC and functional behavior is complete.

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Do not recreate the removed Flow 1 Razor Pages implementation.

## Multi-subject behavior

Flow 1 is not hard-coded to the seeded PRN222 subject. Routes/actions preserve a concrete subject context and server-side authorization uses `ISubjectAccessService` for management operations.

## Request/indexing boundary

```text
MVC action
 -> validate subject/chapter/file
 -> persist Document/Chapter
 -> enqueue Document.Id
 -> redirect preserving subject context

background indexing
 -> parse PDF/DOCX/PPTX
 -> TextChunker
 -> ITextEmbeddingService
 -> replace DocumentChunk rows / update status
```

The MVC layer does not parse/chunk/embed/call AI providers/query pgvector.

## Re-index after embedding changes

Changing embedding provider/model/dimension requires a complete corpus re-index through the existing Flow 1 re-index/indexing pipeline.

PR #37 makes a dimension-changing migration safer for retrieval: old vectors whose dimensions do not match the current query embedding are excluded before cosine distance. This allows gradual document processing without dimension exceptions, but the full corpus still needs to be re-indexed to finish the migration.

Same-dimension embeddings from different models remain semantically incompatible even though `vector_dims` cannot distinguish them.

## Chapter deletion

Deleting a Chapter preserves its Documents by setting affected `ChapterId` values to null within the same Subject.

## Ownership

- Member 2 owns established Flow 1 request/business behavior.
- Member 3 owns ongoing indexing/ingestion maintenance.
- Member 1 owns cross-cutting RBAC/provider/schema/documentation coordination.

See `member-contributions.md` for merged implementation credit.
