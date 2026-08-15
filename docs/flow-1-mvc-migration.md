# Flow 1 MVC architecture

> Updated for provider-neutral embedding support.

## Status

Flow 1 presentation remains MVC and functional behavior is complete.

Current locations:

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Do not recreate removed Razor Pages implementations.

## Multi-subject update

Flow 1 is not scoped to `SeedData.Prn222SubjectId`. Routes/actions carry subject context and server-side authorization uses `ISubjectAccessService`.

## Request/indexing boundary

```text
MVC action
 -> validate subject/chapter/file
 -> persist Document/Chapter
 -> enqueue Document.Id
 -> redirect preserving subject context

background indexing
 -> parse/chunk
 -> ITextEmbeddingService
 -> persist DocumentChunk
```

The MVC layer does not parse/chunk/embed/call Ollama/Gemini/OpenAI/query pgvector.

## Provider impact

Flow 1 request semantics do not change when `RAG_PROVIDER` changes.

AI provider selection is Member 1-owned Infrastructure. Indexing remains Member 3-owned and consumes the same `ITextEmbeddingService` contract.

If the embedding provider/model/dimension changes, all indexed documents must be re-indexed before retrieval. Re-indexing uses the existing Flow 1 action/queue/indexing pipeline; no provider-specific controller action is added.

## Chapter deletion

Deleting a Chapter keeps Documents and sets affected `ChapterId` values to null. The operation is scoped to the same Subject.

## Ownership

- Member 2: established Flow 1 request/business behavior.
- Member 1: multi-subject/RBAC/provider configuration.
- Member 3: indexing and completed PR #19 visual redesign.
