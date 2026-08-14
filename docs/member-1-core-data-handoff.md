# Member 1 - Core/Data handoff

## Current status

Member 1's shared Core/Data baseline is **complete** and is now consumed by completed Flow 1, completed Flow 3, and pending Flow 2 work.

Latest merged milestone:

- Member 1 Core/Data: complete
- Member 2 Flow 1 request/presentation: complete
- Member 3 Flow 1 indexing: complete through PR #9
- Member 2 Flow 3 Report & Statistics: complete through PR #12
- Member 4 Flow 2 backend: pending
- Member 5 Flow 2 **MVC presentation/evaluation**: pending

For the canonical snapshot, read `docs/project-status.md`.

## Relationship to the three workflows

The shared model supports:

1. **Flow 1 - Document Management & Indexing** - end-to-end complete with Razor Pages presentation
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending Members 4/5 with **ASP.NET Core MVC Controllers + Views presentation**
3. **Flow 3 - Report & Statistics** - complete through PR #12 with Razor Pages presentation

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

The existing persistence was sufficient for the completed Flow 3 dashboard. PR #12 aggregated existing rows and did not introduce a reporting entity or migration.

The same persistence is intended to support Flow 2 regardless of presentation model; using MVC for Flow 2 does not itself justify a schema change.

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

Member 3 provides the merged indexing implementation.

`ITextEmbeddingService`

The provider-neutral embedding boundary supports:

- single-text embedding for retrieval
- ordered batch embedding for indexing

The same configured embedding model must be used for indexing and retrieval.

### RAG backend

`IChatCompletionService`

Provider-neutral chat-generation boundary owned for implementation by Member 4.

`IRagQueryService`

Presentation-facing grounded-question boundary to be implemented by Member 4 and consumed by Member 5 from the **MVC presentation layer**.

Result models:

- `RagAnswer`
- `RagCitation`

MVC controllers must not replace these boundaries with direct pgvector/Ollama calls.

### Reporting

Flow 3 completed without a new shared reporting contract. The merged page uses focused read-only EF Core aggregate queries over existing persistence.

Do not add a reporting-specific abstraction/schema unless a concrete new requirement justifies it.

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

## Flow 3 completion validates the baseline

PR #12 completed the Subject-Leader-only Reports/Statistics page using existing tables:

- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Implemented aggregates include chapter/document totals, chapter grouping/unassigned counts, index-state counts, chunk totals, recent indexed/failed documents, and chat usage totals.

No analytics entity, denormalized counter, warehouse, scheduled aggregation, or migration was required.

This confirms the original Core/Data model also supports the initial Flow 3 requirement as designed.

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

Member 4 should remain presentation-agnostic and should not request schema changes unless the current persistence genuinely cannot represent the requirement.

## Current handoff to Member 5 - MVC presentation

Member 5 owns **ASP.NET Core MVC** Flow 2 presentation and evaluation:

- MVC Controllers/actions for chat/session workflows
- MVC Views for chat/session UI
- Conversation History
- citation rendering
- evaluation set/tooling

Expected presentation areas:

```text
src/PRN222.RagAssistant/Controllers/
src/PRN222.RagAssistant/Views/Chat/
```

Presentation should consume `IRagQueryService` and not leak provider or pgvector details.

Do not create a Razor Pages duplicate of Flow 2 under `Pages/Chat` or `Pages/Conversation`.

## Flow 3 maintenance boundary

Member 2's current Flow 3 assignment is complete.

Future report changes must remain read-only and must not:

- mutate workflow data
- enqueue indexing work
- perform RAG retrieval
- call Ollama
- create speculative analytics persistence

If a genuine persistence gap appears later, document it and coordinate the change with Member 1.

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

The original no-speculative-migration decision remains valid after Member 2 Flow 1, Member 3 indexing, and Member 2 Flow 3 merges. The Flow 2 MVC presentation choice does not by itself require any EF Core model change. Continue using the existing migration chain and coordinate genuine model changes through Member 1.
