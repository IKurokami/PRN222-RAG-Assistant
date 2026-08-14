# Project status

This snapshot reflects the Flow 1 presentation migration from Razor Pages to **ASP.NET Core MVC Controllers + Views**. The functional Flow 1 behavior and indexing pipeline remain complete; only the request/presentation implementation has changed.

When documentation disagrees with code, the latest merged `master` is the source of truth. While this migration PR is open, use this branch together with `docs/flow-1-mvc-migration.md` as the intended post-merge state.

## Product workflows

The project defines three independent workflows:

1. **Flow 1 - Document Management & Indexing** - COMPLETE - **ASP.NET Core MVC Controllers + Views**
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - **ASP.NET Core MVC Controllers + Views**
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages

Conversation History belongs to Flow 2 and is not counted as a separate workflow.

## Presentation allocation

```text
Flow 1 -> MVC           [COMPLETE]
Flow 2 -> MVC           [PENDING]
Flow 3 -> Razor Pages   [COMPLETE]
Auth/shell -> Razor Pages
```

The application intentionally remains a mixed MVC + Razor Pages host. `Program.cs` registers and maps both presentation models.

Flow 1 no longer has a parallel implementation under `Pages/Documents/` or `Pages/Chapters/`. Its HTTP entry points now live in `DocumentsController` and `ChaptersController`, with views under `Views/Documents/` and `Views/Chapters/`.

## Current project state

| Area | Owner | Status | Notes |
|---|---|---|---|
| Core domain/data/security | Member 1 | Complete baseline | Entities, EF Core configurations/migrations, Identity, roles/policies, pgvector wiring, shared application contracts, architecture tests. |
| Chapter Management | Member 2 | Complete | MVC list/create/edit/delete for runtime PRN222 chapters; write actions require `ManageDocuments`. |
| Document Management | Member 2 | Complete | MVC list/filter/upload/details/edit/delete/re-index, validation, storage, authorization, and queue handoff. |
| Document parsing/chunking/indexing | Member 3 | Complete / merged through PR #9 | PDF/DOCX/PPTX parsing, chunking, embeddings, indexing service/worker, chunk replacement, state transitions. |
| Flow 1 end-to-end | Members 2 + 3 | Complete | MVC request side -> queue -> worker -> parse -> chunk -> embed -> `DocumentChunk` -> `Indexed`/`Failed`. |
| Flow 3 Report & Statistics | Member 2 | Complete / merged through PR #12 | Subject-Leader-only read-only Razor Pages dashboard. |
| RAG retrieval / grounded backend | Member 4 | Pending | Question embedding, pgvector retrieval, grounded generation, `IRagQueryService`, chat/citation persistence. |
| Flow 2 MVC presentation / history / citations | Member 5 | Pending | MVC chat/session UI, Conversation History, citations, evaluation integration. |
| Evaluation set | Member 5 | Pending | Human-authored 50-question ground-truth set under `evaluation/`. |

## Flow 1 - complete with MVC presentation

### Request/presentation side

Primary files:

```text
src/PRN222.RagAssistant/Controllers/DocumentsController.cs
src/PRN222.RagAssistant/Controllers/ChaptersController.cs
src/PRN222.RagAssistant/Models/Documents/DocumentViewModels.cs
src/PRN222.RagAssistant/Models/Chapters/ChapterViewModels.cs
src/PRN222.RagAssistant/Views/Documents/
src/PRN222.RagAssistant/Views/Chapters/
```

Preserved behavior:

- runtime PRN222 chapter list/create/edit/delete
- document list/filter/upload/details/edit/delete/re-index
- PDF/DOCX/PPTX upload validation
- 50 MB limit
- configured source-file persistence
- optional PRN222 `ChapterId` validation
- `AppPolicies.ManageDocuments` on all write actions
- anti-forgery validation on POST actions
- `Document` persistence with initial `Uploaded` status
- queue handoff only after persistence
- orphan-file cleanup when database persistence fails
- DB-first document deletion with best-effort physical-file cleanup
- safe chapter deletion that preserves documents by clearing `Document.ChapterId`

See `docs/flow-1-mvc-migration.md` for route mapping and migration details.

### Background indexing side

The indexing pipeline remains unchanged:

```text
Document upload / re-index
        |
        v
IDocumentIndexingQueue.EnqueueAsync(documentId)
        |
        v
InMemoryDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
        |
        +--> DocumentParserFactory
        +--> PDF / DOCX / PPTX parser
        +--> TextChunker
        +--> TextEmbeddingBatcher
        +--> ITextEmbeddingService / Ollama
        +--> replace/persist DocumentChunk rows
        \--> update indexing state
```

State flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

The queue remains process-local. Startup recovery is based on persisted `Uploaded`/`Processing` document state.

## Flow 3 - complete Razor Pages workflow

Flow 3 remains unchanged by the Flow 1 MVC migration.

Primary files:

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
```

It reports read-only PRN222 aggregates including chapters/documents, indexing states, chunk counts, recent failures/indexes, and chat session/message/citation totals. It remains protected by `AppPolicies.ManageDocuments` and must not mutate workflow state, call Ollama, or perform pgvector retrieval.

## Flow 2 - remaining work

### Member 4 - RAG backend

Owns:

- question embedding with `ITextEmbeddingService.EmbedAsync`
- pgvector similarity retrieval over successfully indexed PRN222 chunks
- top-K context selection
- grounded/no-evidence behavior
- `IChatCompletionService`
- `IRagQueryService`
- chat-session ownership validation
- user/assistant message persistence
- ordered `MessageCitation` persistence

The backend must remain presentation-agnostic.

### Member 5 - MVC presentation/evaluation

Owns:

- MVC chat controller/actions
- MVC chat/session/history views
- session navigation
- citation/source rendering
- consumption of `IRagQueryService`
- evaluation set/tooling

Flow 2 must not be implemented as Razor Pages under `Pages/Chat` or `Pages/Conversation`.

Flow 1 and Flow 2 controllers share the normal `Controllers/` root, so new Flow 2 code must use focused controller/view names and must not modify Flow 1 behavior without a Flow 1 requirement.

## Shared persistence and contracts

Current domain model:

- `ApplicationUser`
- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Current shared contracts/models:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

The Flow 1 MVC migration does **not** require an EF Core migration because the persistence model is unchanged.

## Validation baseline

Before this presentation migration, PR #12 reported `75/75` automated tests passing and local smoke testing confirmed Flow 1 indexing plus Flow 3 reporting against PostgreSQL/pgvector and Ollama.

This migration updates Flow 1 tests so they target MVC input models/controllers and verify:

- upload/chapter validation remains intact
- Flow 1 uses MVC controllers
- write actions carry `AppPolicies.ManageDocuments`
- POST actions carry anti-forgery protection

CI for this PR is the source of truth for the migrated presentation layer.

## Required reading before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/flow-1-mvc-migration.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

After a major workflow PR is merged, synchronize these documents against the actual `master` baseline.
