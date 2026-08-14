# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. It complements the code-level rules in `AGENTS.md` and the current snapshot in `docs/project-status.md`.

## Current milestone

As of `master` after PR #12 was merged:

- Member 1 Core/Data baseline: **complete**.
- Member 2 Flow 1 Document/Chapter Management request side: **complete / merged**.
- Member 3 Flow 1 Document Indexing/Ingestion: **complete / merged through PR #9**.
- Member 2 Flow 3 Report & Statistics: **complete / merged through PR #12**.
- Member 4 Flow 2 RAG backend: **pending**.
- Member 5 Flow 2 **MVC** chat/conversation-history presentation + evaluation: **pending**.

Flow 1 is end-to-end complete. Flow 3 is complete. The remaining product implementation is Flow 2, and Flow 2 presentation is fixed to ASP.NET Core MVC Controllers + Views.

## Product workflows

The project defines three independent functional workflows for the course requirement:

1. **Flow 1 - Document Management & Indexing** - COMPLETE, Razor Pages presentation. Subject Leader manages PRN222 chapters/documents; the system stores, parses, chunks, embeds, indexes, and exposes indexing state.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING, **MVC presentation required**. Student creates/opens a chat session, asks grounded questions, receives citations, and can reopen persisted Conversation History.
3. **Flow 3 - Report & Statistics** - COMPLETE, Razor Pages presentation. Subject Leader reviews read-only aggregate document/indexing and chat-usage statistics.

**Conversation History is part of Flow 2, not the independent third workflow.**

## Presentation model decision

The application intentionally uses both ASP.NET Core presentation models:

```text
Flow 1 -> Razor Pages   [COMPLETE]
Flow 2 -> MVC           [PENDING]
Flow 3 -> Razor Pages   [COMPLETE]
```

Flow 2 is the selected MVC workflow. New Flow 2 presentation code belongs under `Controllers/` and `Views/`, not under `Pages/`.

Do not migrate completed Flow 1 or Flow 3 merely to make every workflow use the same presentation model.

## Member responsibilities and status

### Member 1 - Core/Data Lead - COMPLETE BASELINE

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- shared `Application/` abstractions/models
- migration conventions
- core architecture/convention tests
- coordination for genuine EF Core schema changes

The current persistence supports all three workflows. Flow 1 and Flow 3 both completed without speculative schema additions. Do not add duplicate analytics records or parallel migrations merely because Flow 2 remains pending.

### Member 2 - Document Management + Report & Statistics - COMPLETE CURRENT ASSIGNMENT

Member 2 owns two separate completed responsibilities.

#### Flow 1 request/presentation side - COMPLETE / MERGED

Merged Razor Pages scope:

- chapter list/create/edit/delete for PRN222
- document list/filter/upload/details/edit/delete/re-index request
- PDF/DOCX/PPTX validation and 50 MB limit
- configured upload storage
- `Document` metadata persistence
- optional PRN222 `ChapterId` validation
- server-side `AppPolicies.ManageDocuments` enforcement
- enqueueing persisted document IDs through `IDocumentIndexingQueue`
- safe chapter removal that unassigns referenced documents before deleting the chapter

Member 2 request handlers must not parse, chunk, embed, run pgvector retrieval, or call Ollama.

#### Flow 3 Report & Statistics - COMPLETE / MERGED THROUGH PR #12

PR #12 added the Subject-Leader Razor Pages reporting dashboard using read-only EF Core aggregate queries.

Implemented scope:

- Subject-Leader Reports/Statistics page
- total PRN222 chapters/documents
- documents grouped by indexing status
- documents grouped by chapter, including unassigned documents
- total PRN222 chunk count
- indexing completion percentage
- recent indexing failures with `IndexError`
- recently indexed documents with chunk counts/timestamps
- total chat sessions/messages/citations
- graceful zero/empty states before Flow 2 data exists

Flow 3 must remain read-only. Future reporting changes must not:

- mutate documents/chapters/indexing/chat data
- alter the indexing worker or parsers
- run pgvector similarity retrieval
- call Ollama
- duplicate Member 5 conversation pages
- create speculative analytics persistence or migrations
- force shared-contract changes merely for dashboard convenience

See `docs/flow-3-report-statistics-handoff.md`.

### Member 3 - Document Indexing / Ingestion - COMPLETE / MERGED

PR #9 completed the background side of Flow 1.

Merged scope:

- `DocumentParserFactory`
- PDF extraction through PdfPig
- DOCX/PPTX extraction through OpenXml
- `TextChunker`
- ordered/bounded embedding batching
- `OllamaTextEmbeddingService` using Ollama `/api/embed`
- `IDocumentIndexingService` implementation
- `DocumentIndexingWorker`
- replacement/persistence of `DocumentChunk` rows
- index-state/error/timestamp transitions
- startup rehydration of persisted `Uploaded`/`Processing` documents into the in-process queue

Implemented state flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-indexing replaces stale chunks coherently rather than appending duplicates.

The active `InMemoryDocumentIndexingQueue` is consumed by `DocumentIndexingWorker`. It is an in-process transport, not a durable broker. Recovery is driven by persisted document state during worker startup.

Member 3 does not own Flow 2 retrieval, Flow 2 MVC presentation, or Flow 3 reporting.

See `docs/member-3-document-indexing-handoff.md`.

### Member 4 - RAG / Chat Backend - PENDING

Member 4 is now the primary remaining backend owner.

Owns Flow 2 backend:

- question embedding through the merged `ITextEmbeddingService`
- pgvector similarity retrieval over successfully indexed PRN222 `DocumentChunk` rows
- top-K context selection
- grounded prompt construction
- explicit no-evidence/out-of-scope behavior
- `IChatCompletionService` implementation for Ollama generation
- `IRagQueryService` implementation
- chat-session ownership validation
- persistence of user/assistant `ChatMessage` rows
- persistence of ordered `MessageCitation` rows

Member 4's implementation must remain presentation-agnostic. It must not depend on MVC `Controller`, Razor Pages `PageModel`, `HttpContext`, or browser-specific types.

Member 4 must not parse raw uploaded files, duplicate the indexing pipeline, implement a second reporting workflow, or put pgvector/Ollama business logic directly in a controller.

### Member 5 - Flow 2 MVC Presentation / Conversation Management / Evaluation - PENDING

Member 5 owns **ASP.NET Core MVC Controllers + Views** for Flow 2 presentation and evaluation.

Owns:

- MVC chat controller/actions
- MVC chat/session views
- chat-session creation/opening/navigation presentation
- Conversation History presentation
- citation/source rendering
- consumption of `IRagQueryService`
- `evaluation/` 50-question human-authored ground-truth set
- evaluation-facing tooling/tests

Expected presentation structure:

```text
src/PRN222.RagAssistant/Controllers/
src/PRN222.RagAssistant/Views/Chat/
```

Supporting MVC files such as `Views/_ViewImports.cshtml`, `Views/_ViewStart.cshtml`, and `Views/Shared/` may be added as needed.

**Do not create `Pages/Chat`, `Pages/Conversation`, or another Razor Pages implementation of Flow 2.**

MVC controllers should remain thin HTTP adapters. They may handle model binding, authorization, redirects, and presentation orchestration, but grounded retrieval/generation must go through `IRagQueryService` and other application/service boundaries.

Browser/controller code must not call Ollama or query pgvector directly.

Member 5 should not duplicate the completed Flow 3 Reports page; chat/history presentation remains Flow 2.

## Shared integration contracts

Stable handoff points live under:

```text
src/PRN222.RagAssistant/Application/
```

Current contracts/models:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService` - single-text and ordered batch embedding
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Treat these signatures as cross-member integration points. Prefer additive changes. If a signature genuinely must change, update affected producers/consumers together and synchronize the docs.

Flow 3 completed without introducing a reporting-specific cross-member contract.

## Flow 1 - complete integration

```text
Member 2 - MERGED Razor Pages
Manage Chapters / Documents
        |
        v
Persist Document
        |
        v
IDocumentIndexingQueue.EnqueueAsync(documentId)
        |
        v
InMemoryDocumentIndexingQueue
        |
        v
Member 3 - MERGED
DocumentIndexingWorker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
        |
        +--> parse PDF / DOCX / PPTX
        +--> chunk
        +--> batch embeddings via Ollama
        +--> replace DocumentChunk rows
        \--> Indexed / Failed state
```

Flow 1 is end-to-end complete in the merged baseline.

## Flow 2 handoff - MVC remaining implementation

```text
Student browser
        |
        v
Member 5 MVC Controller + Views         [PENDING]
        |
        v
IRagQueryService.AskAsync(userId, sessionId, question)
        |
        v
Member 4 RAG backend                    [PENDING]
        |
        +--> ITextEmbeddingService.EmbedAsync(question)
        +--> pgvector retrieval over indexed chunks
        +--> IChatCompletionService
        +--> persist messages/citations
        |
        v
RagAnswer + RagCitation[]
        |
        v
Member 5 MVC Views -> answer / citations / history
```

Member 4 may assume indexed chunks already exist through the completed Flow 1 pipeline.

Member 5 must not bypass the service boundary by implementing retrieval/generation inside `ChatController`.

## Flow 3 - complete integration

```text
Subject Leader
        |
        v
Member 2 Reports/Statistics Razor Pages [MERGED PR #12]
        |
        v
Read-only aggregate EF Core queries
        |
        +--> Chapter / Document
        +--> Document indexing state
        +--> DocumentChunk
        +--> ChatSession / ChatMessage
        \--> MessageCitation
        |
        v
Dashboard / tables
```

Document/indexing metrics use real persisted Flow 1 data. Chat metrics currently handle zero rows and will automatically become meaningful as Flow 2 begins persisting chat data.

## Database coordination

The baseline already includes persistence for all planned workflows. Runtime Chapter CRUD and completed reporting did not require schema changes.

If Flow 2 genuinely requires a schema change:

1. Explain the missing persistence requirement.
2. Coordinate through Member 1.
3. Update the entity and dedicated EF configuration together.
4. Synchronize with latest `master` before generating a migration.
5. Generate one EF Core migration through the repository-local tool.
6. Run the pending-model check and tests.

Avoid parallel competing migrations.

## Remaining branch guidance

Recommended focused branches for unfinished work:

```text
feature/rag-chat
feature/mvc-chat-ui-history
```

Do not recreate already merged branches/implementations for:

```text
feature/document-indexing
feature/report-statistics
```

Do not create a competing `feature/razor-chat-*` branch; Flow 2 presentation is MVC.

## Validation snapshot

PR #12 reported `75/75` automated tests passing. Post-merge local smoke testing also reported:

- PostgreSQL + pgvector healthy
- Ollama embedding runtime healthy
- ASP.NET Core app healthy
- anonymous/Student Reports access blocked
- Subject Leader Reports access successful
- Chapter creation and PDF upload/indexing successful
- Flow 3 aggregate values updated from real persisted Flow 1 data

Treat these as validation of the current merged baseline; future Flow 2 MVC branches must rerun relevant checks for their own changes.

## Required reading before coding

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

Before handing off, run relevant build/tests and report remaining warnings/blockers. After each major merge, synchronize status/ownership documentation against the actual `master` baseline.
