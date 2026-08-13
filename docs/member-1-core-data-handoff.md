# Member 1 - Core/Data handoff

## Current status

Member 1's shared Core/Data baseline is **complete** and is now consumed by completed Flow 1 plus the pending Flow 2/Flow 3 work.

Latest merged milestone:

- Member 1 Core/Data: complete
- Member 2 Flow 1 request/presentation: complete
- Member 3 Flow 1 indexing: complete through PR #9
- Member 4 Flow 2 backend: pending
- Member 5 Flow 2 presentation/evaluation: pending
- Member 2 Flow 3 Report & Statistics: pending

For the canonical snapshot, read `docs/project-status.md`.

## Relationship to the three workflows

The shared model supports:

1. **Flow 1 - Document Management & Indexing** - now end-to-end complete
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending Members 4/5
3. **Flow 3 - Report & Statistics** - pending Member 2

Conversation History remains part of Flow 2.

Member 1 continues to own schema/migration coordination across all workflows but should not absorb later members' business logic.

## Scope completed by Member 1

Member 1 established:

- core domain entities/enums
- EF Core configurations
- migration baseline
- PostgreSQL/pgvector integration
- ASP.NET Core Identity integration
- `SubjectLeader` and `Student` roles
- `ManageDocuments` authorization policy
- shared `Application/` abstractions/models
- architecture/convention tests
- schema/migration coordination rules

Validated persistence covers:

- PRN222 subject and runtime-managed chapters
- document metadata/storage/indexing state
- document chunks with page/slide metadata and embeddings
- chat sessions/messages
- message citations

The existing persistence is sufficient for the initial Flow 3 dashboard; reporting should aggregate existing rows before proposing new storage.

## Core domain invariants

Important rules remain:

- entities use scalar foreign keys and no navigation properties
- EF mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` classes
- `ApplicationDbContext` stays thin
- application schema changes use EF Core migrations
- `(SubjectId, Number)` is unique for chapters
- `Document.ChapterId` is nullable
- removing a chapter must not cascade-delete documents
- timestamps are persisted as UTC
- domain enums are persisted according to established configuration conventions

## Shared contracts

### Document Management -> Indexing

`IDocumentIndexingQueue`

Member 2 persists the document first, then enqueues only the persisted `Document.Id`.

### Indexing pipeline

`IDocumentIndexingService`

Member 3 now provides the merged indexing implementation.

`ITextEmbeddingService`

Member 3 extended/implemented the provider-neutral embedding boundary with:

- single-text embedding for retrieval
- ordered batch embedding for indexing

The same configured embedding model must be used for indexing and retrieval.

### RAG backend

`IChatCompletionService`

Provider-neutral chat-generation boundary owned for implementation by Member 4.

`IRagQueryService`

Presentation-facing grounded-question boundary to be implemented by Member 4 and consumed by Member 5.

Result models:

- `RagAnswer`
- `RagCitation`

### Reporting

Initial Flow 3 does not need a new shared contract merely to count existing data. Prefer focused read-only aggregate queries unless a concrete reusable application-layer need emerges.

## Flow 1 completion validates the baseline

### Member 2 merged behavior

- runtime Chapter CRUD
- document upload/list/details/edit/delete/re-index request
- authorization/validation
- configured source storage
- `Uploaded` metadata persistence
- queue handoff

### Member 3 merged behavior

PR #9 completed:

- PDF/DOCX/PPTX parsing
- chunking
- bounded/ordered embeddings
- `DocumentIndexingWorker`
- `DocumentIndexingService`
- `DocumentChunk` replacement/persistence
- index-state transitions
- startup rehydration of `Uploaded`/`Processing` documents

Implemented state flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

No new domain entity or migration was required for this indexing implementation, confirming that the original persistence baseline was sufficient.

## Current handoff to Member 4

Member 4 should build RAG retrieval on the existing model and completed indexing output.

Expected behavior:

1. validate `chatSessionId` ownership for `userId`;
2. persist the user message;
3. embed the question with `ITextEmbeddingService.EmbedAsync`;
4. retrieve successfully indexed PRN222 chunks with pgvector;
5. construct grounded context;
6. call `IChatCompletionService`;
7. persist the assistant message and ordered `MessageCitation` rows;
8. return `RagAnswer` / `RagCitation`;
9. handle insufficient evidence explicitly.

Member 4 should not request schema changes unless the current persistence genuinely cannot represent the requirement.

## Current handoff to Member 5

Member 5 owns Flow 2 presentation and evaluation:

- chat/session UI
- Conversation History
- citation rendering
- evaluation set/tooling

Presentation should consume `IRagQueryService` and not leak provider or pgvector details.

## Member 2 Flow 3 assignment

Member 2 owns Flow 3 in a separate branch.

Initial read-only metrics:

- chapters/documents
- documents by index status
- documents by chapter/unassigned
- chat sessions/messages/citations

Because Member 3 is complete, indexing metrics can now show real persisted status immediately.

The initial reporting scope still does **not** justify:

- analytics entities
- denormalized counters
- event tracking
- scheduled aggregation
- a reporting warehouse
- a migration solely for dashboard counts

If a genuine persistence gap appears, Member 2 must document it and coordinate the change with Member 1.

## Core tests

`CoreDataArchitectureTests` and `EntityModelConventionsTests` remain regression protection for the shared baseline.

Later workflow tests must not weaken these invariants.

## Files to read before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

## Validation note

The original no-speculative-migration decision remains valid after both Member 2 and Member 3 merges. Continue using the existing migration chain and coordinate genuine model changes through Member 1.