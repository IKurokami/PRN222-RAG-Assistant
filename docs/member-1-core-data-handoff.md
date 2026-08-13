# Member 1 - Core/Data handoff

## Current status

Member 1's shared baseline is complete and already consumed by the merged Member 2 Document Management work.

Since this handoff was originally written, PR #5 has merged Member 2's runtime Chapter Management and Document Management request/presentation flow into `master`. The project now also defines an independent **Flow 3 - Report & Statistics**, assigned to Member 2 as a separate read-only workflow.

For the latest project milestone, read `docs/project-status.md`. For workflow ownership, read `docs/team-workflow.md`. For reporting scope, read `docs/flow-3-report-statistics-handoff.md`.

## Product workflow relationship to the Core/Data baseline

The shared model supports three product workflows:

1. **Flow 1 - Document Management & Indexing**
2. **Flow 2 - RAG Question & Answer & Conversation Management**
3. **Flow 3 - Report & Statistics**

Conversation History belongs to Flow 2 and is not counted as the independent third workflow.

Member 1 remains the Core/Data and schema/migration coordinator across all three flows. Flow 3 must consume the existing persisted data read-only before requesting any new schema.

## Scope completed by Member 1

Member 1 owns the shared application/data baseline that the remaining workflows build on.

The repository already had the required persistent entities, EF Core configurations, migration baseline, PostgreSQL/pgvector integration, Identity roles, and the `ManageDocuments` policy. This handoff intentionally did **not** add speculative schema fields or an unnecessary migration.

Validated existing persistence covers:

- PRN222 subject and runtime-manageable chapters
- document metadata, source storage path, upload owner, indexing status/error/timestamps
- document chunks with page/slide metadata and pgvector embeddings
- chat sessions and messages
- citations linking assistant messages to source chunks
- `SubjectLeader` and `Student` Identity roles
- document-management authorization restricted to `SubjectLeader`

These same existing records are sufficient for the initial Flow 3 aggregate dashboard. Reporting should derive counts from `Chapter`, `Document`, `DocumentChunk`, `ChatSession`, `ChatMessage`, and `MessageCitation` rather than introducing duplicate analytics entities.

`Chapter` persistence is intentionally reusable at runtime. The existing model supports Subject Leader-managed Chapter CRUD without a schema change: `Chapter` has `Id`, `SubjectId`, `Number`, and `Title`; `(SubjectId, Number)` is unique; and `Document.ChapterId` is nullable.

Member 2 now uses this model directly in the merged Chapter Management flow.

## Shared contracts established

### Upload -> indexing

`IDocumentIndexingQueue`

The document-management workflow persists the source file and `Document` record first, then enqueues only the persisted `Document.Id`.

This handoff is now active in merged Member 2 code. The current `InMemoryDocumentIndexingQueue` is only a temporary integration stub until Member 3 supplies the real background integration.

`IDocumentIndexingService`

The Member 3 background worker should invoke this service for each dequeued document. The implementation owns parsing, chunk replacement, embedding, and index-state transitions.

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

### Reporting

The first Flow 3 implementation should **not require a new cross-workflow application contract** merely to calculate aggregate counts.

Prefer direct read-only EF Core aggregate queries from the reporting presentation/application code using the existing `ApplicationDbContext` conventions. If reporting later becomes complex enough to justify an abstraction, add it only when there is a concrete need and coordinate the public contract with the affected owners.

## Core invariants protected by tests

`CoreDataArchitectureTests` provides regression protection for:

- explicit delete behavior on core relationships
- presence/nullability of RAG persistence fields
- the `ManageDocuments` policy allowing only `SubjectLeader`

`EntityModelConventionsTests` enforces:

- no navigation properties in domain entities
- one dedicated `IEntityTypeConfiguration<TEntity>` per entity

Member 2 has since added request-side tests for Chapter and Document Management. Those later tests do not replace the core architecture tests; both sets of invariants should stay green.

Future Flow 3 tests should verify aggregate/query correctness and access restrictions without weakening these conventions.

## Why Member 1 added no new migration

No persistence gap was found that required a schema change before later members started.

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

Member 2's merged Chapter and Document Management features confirmed this decision: runtime Chapter CRUD and document metadata/upload flows were implemented without a new schema migration.

The initial Flow 3 scope also does not require a new migration because it only aggregates these existing records. A dashboard is not justification for adding duplicated counters, analytics tables, or event records.

Later schema changes must follow the coordination rules in `AGENTS.md` and `docs/team-workflow.md`.

## Member 2 Flow 1 handoff - NOW MERGED

The original Member 1 -> Member 2 Flow 1 expectations have been implemented.

Merged Chapter Management behavior includes:

- Subject Leader authorization
- runtime PRN222 Chapter list/create/edit/removal
- chapter number/title validation
- unique number validation within PRN222
- preserving documents when a chapter is removed by clearing referenced nullable `ChapterId` values before removing the chapter

Merged document flow includes:

```text
Authorize SubjectLeader
        |
Validate PDF/DOCX/PPTX and size
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

Member 2 correctly leaves parsing/chunking/embedding outside the request handler.

See `docs/member-2-document-management-handoff.md` for the current downstream integration contract.

## Member 2 Flow 3 assignment - NEW / PENDING

Member 2 additionally owns **Flow 3 - Report & Statistics** in a separate focused branch after synchronizing with the latest `master`.

The initial reporting workflow is deliberately read-only:

- chapter/document totals
- documents grouped by indexing status
- documents grouped by chapter, including unassigned documents
- total chat sessions/messages/citations
- clear zero/empty states before Flow 2 data exists

Member 2 must not create a migration simply to support these counts. If a genuine persistence gap appears, Member 2 must document it and coordinate with Member 1 before touching the schema.

## Handoff to Member 3 - Flow 1 background side

Member 3 should implement:

- final/integrated `IDocumentIndexingQueue` behavior
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

Member 3 should preserve the merged Member 2 request-side handoff. The temporary `InMemoryDocumentIndexingQueue` and its DI registration may be replaced as part of the real worker integration.

## Handoff to Member 4 - Flow 2 backend

Member 4 should implement:

- pgvector retrieval
- grounded context/prompt construction
- `IChatCompletionService`
- `IRagQueryService`

`IRagQueryService` must:

1. validate that `chatSessionId` belongs to `userId`;
2. persist the user message;
3. retrieve only successfully indexed PRN222 document chunks;
4. generate an answer using grounded document context;
5. provide explicit no-evidence/out-of-scope behavior when retrieval is insufficient;
6. persist the assistant message and source `MessageCitation` rows;
7. return `RagAnswer` with ordered `RagCitation` values.

## Handoff to Member 5 - Flow 2 presentation/evaluation

Member 5 should treat `IRagQueryService` as the chat backend boundary and render `RagAnswer`/`RagCitation` results. UI code should not depend on Ollama payloads or raw pgvector queries.

Member 5 owns chat-session creation/opening/navigation and **Conversation History as part of Flow 2**, plus the `evaluation/` human-authored evaluation deliverable.

## Files to read before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-2-document-management-handoff.md
docs/flow-3-report-statistics-handoff.md
```

## Validation note

Member 1's no-new-migration decision remains valid after Member 2's merge and after defining Flow 3. The restrictive `Document -> Chapter` relationship remains intentional; runtime document unassignment is handled in the application workflow rather than by changing the schema to cascade.
