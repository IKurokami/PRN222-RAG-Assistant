# Member 1 - Core/Data handoff

## Current status

Member 1's shared Core/Data baseline is **complete** and is consumed by completed Flow 1, completed Flow 3, and pending Flow 2 work.

Current workflow presentation allocation:

1. **Flow 1 - Document Management & Indexing** - complete - **MVC Controllers + Views**
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - **MVC Controllers + Views**
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

The Flow 1 Razor Pages -> MVC conversion is presentation-only and does not alter the Core/Data model.

For the canonical snapshot, read `docs/project-status.md`.

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
- document chunks with source metadata and embeddings
- chat sessions/messages
- message citations

Flow 1 MVC and Flow 3 reporting both use this existing persistence without adding presentation-specific schema.

## Core domain invariants

- entities use scalar foreign keys and no navigation properties
- EF mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` classes
- `ApplicationDbContext` stays thin
- application schema changes use EF Core migrations
- `(SubjectId, Number)` is unique for chapters
- `Document.ChapterId` is nullable
- removing a chapter must not cascade-delete documents
- timestamps are UTC
- persisted enum conventions remain consistent

The Flow 1 MVC migration preserves all of these rules.

## Shared contracts

### Document Management -> Indexing

`IDocumentIndexingQueue`

`DocumentsController` persists the document first and then enqueues only the persisted `Document.Id`.

### Indexing pipeline

`IDocumentIndexingService`

Member 3 provides the merged indexing implementation.

`ITextEmbeddingService`

Supports single-text embedding for retrieval and ordered batch embedding for indexing. Indexing and retrieval must use the same configured embedding model.

### RAG backend

`IChatCompletionService`

Provider-neutral generation boundary owned for implementation by Member 4.

`IRagQueryService`

Presentation-facing grounded Q&A boundary to be implemented by Member 4 and consumed by Member 5 from MVC.

Result models:

- `RagAnswer`
- `RagCitation`

MVC controllers must not replace these service boundaries with direct provider/pgvector calls.

### Reporting

Flow 3 remains read-only Razor Pages and does not require a reporting-specific shared contract or schema.

## Flow 1 validates the baseline

### Request/presentation side

Current implementation:

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Behavior includes runtime Chapter CRUD, document management, authorization/validation, source storage, `Uploaded` persistence, and queue handoff.

### Indexing side

PR #9 completed:

- PDF/DOCX/PPTX parsing
- chunking
- bounded/ordered embeddings
- `DocumentIndexingWorker`
- `DocumentIndexingService`
- `DocumentChunk` replacement/persistence
- indexing state transitions
- startup recovery

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

No schema change was required for either the indexing implementation or the later MVC presentation migration.

## Flow 3 validates the baseline

Flow 3 aggregates existing `Chapter`, `Document`, `DocumentChunk`, `ChatSession`, `ChatMessage`, and `MessageCitation` rows. It does not introduce analytics tables, denormalized counters, or a reporting migration.

## Handoff to Member 4

Member 4 should build retrieval on the existing model and indexed chunks:

1. validate session ownership;
2. persist the user message;
3. embed the question;
4. retrieve indexed PRN222 chunks with pgvector;
5. construct grounded context;
6. call `IChatCompletionService`;
7. persist assistant message/citations;
8. return `RagAnswer` / `RagCitation`;
9. handle insufficient evidence explicitly.

Member 4 remains presentation-agnostic.

## Handoff to Member 5

Member 5 owns Flow 2 MVC presentation/evaluation:

- `ChatController` or equivalent focused MVC actions
- `Views/Chat/`
- Conversation History
- citations
- evaluation set/tooling

Flow 1 already occupies `DocumentsController`, `ChaptersController`, `Views/Documents`, and `Views/Chapters`; do not mix Flow 2 responsibilities into those controllers.

Do not create a Razor Pages duplicate of Flow 2.

## Core tests

`CoreDataArchitectureTests` and `EntityModelConventionsTests` remain regression protection for the shared baseline. Flow 1 presentation tests now target MVC controllers/models but must not weaken Core/Data invariants.

## Files to read before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/flow-1-mvc-migration.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

The no-speculative-migration decision remains valid. A presentation-layer MVC conversion is not a reason to generate an EF Core migration.
