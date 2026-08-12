# Member 1 - Core/Data handoff

## Scope completed

Member 1 owns the shared application/data baseline that the remaining workflows build on.

The repository already had the required persistent entities, EF Core configurations, migration baseline, PostgreSQL/pgvector integration, Identity roles, and the `ManageDocuments` policy. This handoff intentionally does **not** add speculative schema fields or an unnecessary migration.

Validated existing persistence covers:

- PRN222 subject and chapters
- document metadata, source storage path, upload owner, indexing status/error/timestamps
- document chunks with page/slide metadata and pgvector embeddings
- chat sessions and messages
- citations linking assistant messages to source chunks
- `SubjectLeader` and `Student` Identity roles
- document-management authorization restricted to `SubjectLeader`

`Chapter` persistence is intentionally reusable at runtime. The existing model already supports Subject Leader-managed Chapter CRUD without a schema change: `Chapter` has `Id`, `SubjectId`, `Number`, and `Title`; `(SubjectId, Number)` is unique; and `Document.ChapterId` is nullable.

## Shared contracts added

### Upload -> indexing

`IDocumentIndexingQueue`

The document-management workflow persists the source file and `Document` record first, then enqueues only the persisted `Document.Id`.

`IDocumentIndexingService`

The background worker invokes this service for each dequeued document. The implementation owns parsing, chunk replacement, embedding, and index-state transitions.

### Indexing/retrieval -> AI provider

`ITextEmbeddingService`

Both document indexing and question retrieval must use the same configured embedding implementation/model for a given index.

`IChatCompletionService`

The RAG layer uses this provider-neutral boundary instead of coupling business logic directly to Ollama request/response DTOs.

### Chat presentation -> RAG

`IRagQueryService`

The presentation layer supplies the authenticated `userId`, an existing `chatSessionId`, and the question. The implementation is responsible for session ownership validation, persistence, grounded generation, and citations.

The result is returned as:

- `RagAnswer`
- `RagCitation`

These models keep the chat UI independent from database entities and provider-specific DTOs.

## Core invariants protected by tests

`CoreDataArchitectureTests` adds regression protection for:

- explicit delete behavior on core relationships
- presence/nullability of RAG persistence fields
- the `ManageDocuments` policy allowing only `SubjectLeader`

The existing `EntityModelConventionsTests` continues to enforce:

- no navigation properties in domain entities
- one dedicated `IEntityTypeConfiguration<TEntity>` per entity

## Why there is no new migration

No persistence gap was found that requires a schema change before Member 2-4 start.

The existing model already has:

```text
Chapter
- SubjectId
- Number
- Title

Document
- SubjectId
- ChapterId (nullable)
- UploadedByUserId
- title/file metadata/storage path
- IndexStatus
- IndexError
- UploadedAtUtc
- IndexedAtUtc

DocumentChunk
- DocumentId
- ChunkIndex
- Content
- PageNumber
- SlideNumber
- Embedding

ChatSession
- UserId
- Title
- CreatedAtUtc
- UpdatedAtUtc

ChatMessage
- ChatSessionId
- Role
- Content
- CreatedAtUtc

MessageCitation
- ChatMessageId
- DocumentChunkId
- Rank
```

Adding fields without a concrete workflow requirement would create migration churn and increase merge conflicts. Later schema changes must follow the coordination rules in `AGENTS.md` and `docs/team-workflow.md`.

## Handoff to Member 2

Member 2 should implement document and chapter management against the existing model.

Chapter Management belongs to the Flow 1 request/presentation side. Subject Leaders should be able to create, edit, list, and delete PRN222 chapters at runtime instead of depending on fixed seed data.

Expected Chapter flow:

```text
Authorize SubjectLeader
        |
Create/Edit Chapter
        |
Validate SubjectId = PRN222
        |
Validate unique chapter Number within PRN222
        |
Persist Chapter
```

For Chapter deletion, keep the existing restrictive relationship behavior. Do not cascade-delete documents and do not change the FK merely to make deletion easier.

Expected delete sequence when documents reference a Chapter:

```text
Authorize SubjectLeader
        |
Confirm destructive organization change
        |
BEGIN TRANSACTION
        |
Set matching Document.ChapterId = null
        |
Delete Chapter
        |
COMMIT
```

Expected document upload sequence:

```text
Authorize SubjectLeader
        |
Validate PDF/DOCX/PPTX
        |
Validate optional ChapterId belongs to PRN222
        |
Persist source file
        |
Create Document with IndexStatus = Uploaded
        |
SaveChanges
        |
IDocumentIndexingQueue.EnqueueAsync(document.Id)
```

Do not parse or embed the file in the upload request.

## Handoff to Member 3

Member 3 should implement:

- `IDocumentIndexingQueue`
- hosted/background worker
- `IDocumentIndexingService`
- parsers/chunking
- `ITextEmbeddingService`

The indexing implementation owns:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

On success, clear `IndexError` and set `IndexedAtUtc`.
On failure, persist a useful bounded `IndexError` and set status to `Failed`.
Re-indexing should replace stale chunks rather than append duplicates.

## Handoff to Member 4

Member 4 should implement:

- pgvector retrieval
- grounded context/prompt construction
- `IChatCompletionService`
- `IRagQueryService`

`IRagQueryService` must:

1. validate that `chatSessionId` belongs to `userId`;
2. persist the user message;
3. retrieve only indexed PRN222 document chunks;
4. generate an answer using document context only;
5. provide an explicit no-evidence/out-of-scope answer when retrieval is insufficient;
6. persist the assistant message and source `MessageCitation` rows;
7. return `RagAnswer` with ordered `RagCitation` values.

## Handoff to Member 5

Member 5 should treat `IRagQueryService` as the chat backend boundary and render `RagAnswer`/`RagCitation` results. UI code should not depend on Ollama payloads or raw pgvector queries.

## Files to read before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/team-workflow.md
docs/infrastructure.md
```

## Validation status

This work was authored through the connected GitHub repository environment. The Chapter Management clarification does not change the EF model, so no new migration is expected. The existing restrictive `Document -> Chapter` relationship remains intentional; runtime unlinking before Chapter deletion belongs to the application workflow.
