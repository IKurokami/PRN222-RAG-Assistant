# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. It complements `AGENTS.md` and `docs/project-status.md`.

## Current milestone

- Member 1 Core/Data baseline: **complete**.
- Member 2 Flow 1 Document/Chapter Management request side: **complete**, now using **MVC Controllers + Views**.
- Member 3 Flow 1 Document Indexing/Ingestion: **complete / merged through PR #9**.
- Member 2 Flow 3 Report & Statistics: **complete / merged through PR #12**.
- Member 4 Flow 2 RAG backend: **pending**.
- Member 5 Flow 2 MVC chat/history/citation presentation + evaluation: **pending**.

Flow 1 and Flow 3 are complete. The remaining product implementation is Flow 2.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - **MVC Controllers + Views**. Subject Leader manages PRN222 chapters/documents; the indexing worker parses, chunks, embeds, and persists searchable chunks.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - **MVC Controllers + Views**. Students ask grounded questions, receive citations, and reopen Conversation History.
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages. Subject Leader reviews read-only aggregate document/indexing/chat-usage statistics.

Conversation History is part of Flow 2, not a fourth or independent third workflow.

## Presentation model allocation

```text
Flow 1 -> MVC           [COMPLETE]
Flow 2 -> MVC           [PENDING]
Flow 3 -> Razor Pages   [COMPLETE]
Auth/shell -> Razor Pages
```

The mixed host is intentional. Do not recreate Flow 1 under `Pages/Documents` or `Pages/Chapters`, and do not create a Razor Pages version of Flow 2.

## Member responsibilities

### Member 1 - Core/Data Lead - COMPLETE BASELINE

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- shared `Application/` abstractions/models
- migration conventions
- architecture/convention tests
- coordination for genuine EF Core schema changes

Presentation migrations alone do not justify schema changes.

### Member 2 - Flow 1 + Flow 3 - COMPLETE CURRENT ASSIGNMENT

#### Flow 1 request/presentation

Current MVC locations:

```text
src/PRN222.RagAssistant/Controllers/DocumentsController.cs
src/PRN222.RagAssistant/Controllers/ChaptersController.cs
src/PRN222.RagAssistant/Models/Documents/
src/PRN222.RagAssistant/Models/Chapters/
src/PRN222.RagAssistant/Views/Documents/
src/PRN222.RagAssistant/Views/Chapters/
```

Owned behavior:

- chapter list/create/edit/delete
- document list/filter/upload/details/edit/delete/re-index
- PDF/DOCX/PPTX validation and 50 MB limit
- configured source-file storage
- document metadata persistence
- PRN222 `ChapterId` validation
- `AppPolicies.ManageDocuments` on write actions
- anti-forgery protection on POST actions
- enqueueing persisted document IDs through `IDocumentIndexingQueue`
- safe chapter deletion that unassigns referenced documents

Flow 1 controllers must not parse/chunk/embed, perform pgvector retrieval, or call Ollama.

See `docs/flow-1-mvc-migration.md` and `docs/member-2-document-management-handoff.md`.

#### Flow 3 Report & Statistics

Flow 3 remains under `Pages/Reports/` and remains Razor Pages.

It is read-only and Subject-Leader-only. It must not mutate workflow rows, enqueue indexing, perform RAG retrieval, call Ollama, duplicate Conversation History, or introduce speculative analytics persistence.

### Member 3 - Document Indexing / Ingestion - COMPLETE

Owns the already-merged background Flow 1 pipeline:

- PDF via PdfPig
- DOCX/PPTX via OpenXml
- `DocumentParserFactory`
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- coherent `DocumentChunk` replacement
- indexing state/error/timestamp transitions
- startup recovery for persisted `Uploaded`/`Processing` documents

State flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Do not build a second indexing pipeline.

### Member 4 - RAG / Chat Backend - PENDING

Owns Flow 2 backend:

- question embedding through `ITextEmbeddingService`
- pgvector retrieval over indexed PRN222 chunks
- top-K context selection
- grounded prompt construction
- no-evidence/out-of-scope behavior
- `IChatCompletionService`
- `IRagQueryService`
- session ownership validation
- `ChatMessage` and `MessageCitation` persistence

Member 4 remains presentation-agnostic and must not depend on MVC Controller/Razor Page types.

### Member 5 - Flow 2 MVC Presentation / Conversation Management / Evaluation - PENDING

Owns:

- chat/session MVC controller actions
- `Views/Chat/` presentation
- session creation/open/navigation
- Conversation History
- citation/source rendering
- consumption of `IRagQueryService`
- 50-question evaluation set and evaluation tooling

Flow 2 should coexist with the existing Flow 1 MVC controllers without modifying document/chapter behavior unnecessarily.

Do not create `Pages/Chat` or `Pages/Conversation`.

## Shared integration contracts

Stable handoff points under `Application/`:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Prefer additive changes. If a public signature must change, update affected producers/consumers together and synchronize docs.

## Flow 1 integration

```text
Subject Leader browser
        |
        v
DocumentsController / ChaptersController     [MVC]
        |
        +--> validate / persist / manage
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
DocumentIndexingService
        |
        +--> parse PDF / DOCX / PPTX
        +--> chunk
        +--> batch embed via Ollama
        +--> replace DocumentChunk rows
        \--> Indexed / Failed
```

The MVC migration changes only the first request/presentation stage.

## Flow 2 handoff

```text
Student browser
        |
        v
Member 5 ChatController + MVC Views          [PENDING]
        |
        v
IRagQueryService
        |
        v
Member 4 RAG backend                         [PENDING]
        |
        +--> question embedding
        +--> pgvector retrieval
        +--> grounded generation
        +--> messages/citations persistence
        |
        v
RagAnswer + RagCitation[]
```

Member 5 must not implement provider/retrieval logic in controllers.

## Flow 3 integration

```text
Subject Leader
        |
        v
Pages/Reports Razor Pages                    [COMPLETE]
        |
        v
Read-only aggregate EF Core queries
```

Flow 3 automatically benefits from real Flow 1 persisted data and later Flow 2 chat persistence.

## Database coordination

If later work genuinely requires a schema change:

1. document the missing persistence requirement;
2. coordinate through Member 1;
3. update entity + dedicated EF configuration together;
4. synchronize with latest `master`;
5. generate one EF Core migration;
6. run pending-model checks and tests.

Avoid competing migrations.

## Branch guidance

Recommended unfinished-work branches remain focused around Flow 2, for example:

```text
feature/rag-chat
feature/mvc-chat-ui-history
```

Do not recreate completed document indexing/reporting work or add a parallel Razor Flow 1/Flow 2 implementation.

## Required reading

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

Before handoff, run relevant build/tests and report remaining blockers. After major merges, synchronize ownership/status docs against the actual `master` baseline.
