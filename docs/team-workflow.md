# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. It complements the code-level rules in `AGENTS.md` and the current snapshot in `docs/project-status.md`.

## Current milestone

As of the latest `master` after PR #10 and PR #9 were merged:

- Member 1 Core/Data baseline: **complete**.
- Member 2 Flow 1 Document/Chapter Management request side: **complete / merged**.
- Member 3 Flow 1 Document Indexing/Ingestion: **complete / merged**.
- Member 4 Flow 2 RAG backend: **pending**.
- Member 5 Flow 2 chat/conversation-history presentation + evaluation: **pending**.
- Member 2 Flow 3 Report & Statistics: **defined / pending implementation**.

PR #9 completed the Member 2 -> Member 3 handoff. Flow 1 is now end-to-end implemented in the merged baseline.

## Product workflows

The project defines three independent functional workflows for the course requirement:

1. **Flow 1 - Document Management & Indexing** - Subject Leader manages PRN222 chapters/documents; the system stores, parses, chunks, embeds, indexes, and exposes indexing state.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - Student creates/opens a chat session, asks grounded questions, receives citations, and can reopen persisted Conversation History.
3. **Flow 3 - Report & Statistics** - Subject Leader reviews read-only aggregate document/indexing and chat-usage statistics.

**Conversation History is part of Flow 2, not the independent third workflow.**

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

The current persistence already supports all three initial workflows. Do not add speculative schema fields, duplicate analytics records, or parallel migrations.

### Member 2 - Document Management + Report & Statistics

Member 2 owns two separate responsibilities.

#### Flow 1 request/presentation side - COMPLETE / MERGED

Merged scope:

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

#### Flow 3 Report & Statistics - PENDING

Member 2 owns the independent read-only reporting workflow.

Initial scope:

- Subject-Leader Reports/Statistics page
- total PRN222 chapters/documents
- documents grouped by indexing status
- documents grouped by chapter, including unassigned documents
- total chat sessions/messages/citations
- graceful zero/empty states

Because Member 3 is now merged, indexing statistics can query real persisted `Uploaded`/`Processing`/`Indexed`/`Failed` state immediately.

Flow 3 must not:

- mutate documents/chapters/indexing/chat data
- alter the indexing worker or parsers
- run pgvector similarity retrieval
- call Ollama
- duplicate Member 5 conversation pages
- create speculative analytics persistence or migrations
- force shared-contract changes merely for dashboard convenience

Use a focused branch such as `feature/report-statistics`.

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

The active `InMemoryDocumentIndexingQueue` is now consumed by `DocumentIndexingWorker`. It is an in-process transport, not a durable broker. Recovery is driven by persisted document state during worker startup.

Member 3 does not own Flow 2 retrieval, Flow 2 chat UI, or Flow 3 reporting.

See `docs/member-3-document-indexing-handoff.md` for the handoff to Member 4.

### Member 4 - RAG / Chat Backend - PENDING

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

Member 4 must not parse raw uploaded files or duplicate the indexing pipeline.

### Member 5 - Chat UI / Conversation Management / Evaluation - PENDING

Owns Flow 2 presentation and evaluation:

- chat UI
- chat-session creation/opening/navigation
- Conversation History
- citation/source rendering
- consumption of `IRagQueryService`
- `evaluation/` 50-question human-authored ground-truth set
- evaluation-facing tooling/tests

Browser/UI code must not call Ollama or query pgvector directly.

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

Flow 3 should not introduce a cross-member contract unless a concrete implementation need justifies it.

## Flow 1 - complete integration

```text
Member 2 - MERGED
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

Flow 1 is now end-to-end complete in the merged baseline.

## Flow 2 handoff

```text
Member 5 UI / Conversation History
        |
        v
IRagQueryService.AskAsync(userId, sessionId, question)
        |
        v
Member 4 RAG backend
        |
        +--> ITextEmbeddingService.EmbedAsync(question)
        +--> pgvector retrieval over indexed chunks
        +--> IChatCompletionService
        +--> persist messages/citations
        |
        v
RagAnswer + RagCitation[]
```

Member 4 may assume indexed chunks already exist through the completed Flow 1 pipeline.

## Flow 3 boundary

```text
Member 2 Reports/Statistics UI
        |
        v
Read-only aggregate queries
        |
        +--> Chapter / Document
        +--> Document indexing state
        +--> ChatSession / ChatMessage
        \--> MessageCitation
        |
        v
Subject Leader dashboard / tables
```

Document/indexing metrics are available now. Chat metrics must handle zero rows until Flow 2 persists chat data.

## Database coordination

The baseline already includes persistence for the planned workflows. Runtime Chapter CRUD and initial reporting do not require schema changes.

If a later workflow genuinely requires a schema change:

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
feature/chat-ui-history
feature/report-statistics
```

`feature/document-indexing` has already been merged through PR #9 and must not be recreated as a competing implementation.

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