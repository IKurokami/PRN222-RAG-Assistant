# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. It complements the code-level rules in `AGENTS.md` and the snapshot in `docs/project-status.md`.

## Current milestone

As of the merge of PR #5 into `master`:

- Member 1 core/data baseline is complete.
- Member 2 Document Management and Chapter Management request/presentation work is merged.
- Member 3 indexing/ingestion implementation is the next major Flow 1 integration step.
- Member 4 owns the pending Flow 2 RAG backend.
- Member 5 owns the pending Flow 2 chat/history presentation and evaluation deliverable.
- Member 2 additionally owns the new **Flow 3 - Report & Statistics** implementation in a separate focused branch after synchronizing with the latest `master`.

Member 2's merged Flow 1 work already calls `IDocumentIndexingQueue`. The currently registered `InMemoryDocumentIndexingQueue` is a temporary integration stub and is not the final Member 3 worker/indexing implementation.

The Flow 3 ownership change is intentionally documentation/coordination-first. It must not alter the active Member 3 indexing work or the Member 4/5 Flow 2 boundaries.

## Product workflows

The project defines three independent functional workflows for the course requirement:

1. **Flow 1 - Document Management & Indexing** - Subject Leader manages PRN222 chapters, uploads course material, the system stores it, indexes it, and exposes indexing state.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - Student creates/opens a chat session, asks questions, receives grounded answers with citations, and can reopen persisted conversation history.
3. **Flow 3 - Report & Statistics** - Subject Leader opens a read-only reporting area and reviews aggregate document/indexing and chat-usage statistics derived from persisted PRN222 data.

**Conversation History is part of Flow 2, not the independent third workflow.** This keeps the third workflow clearly distinct from the chat lifecycle and avoids relying on a chat sub-feature to satisfy the minimum-three-workflows requirement.

See `docs/flow-3-report-statistics-handoff.md` for the Flow 3 scope and non-interference rules.

## Member responsibilities and status

### Member 1 - Core/Data Lead - COMPLETE BASELINE

Primary responsibility: keep the shared model, security, persistence, and integration boundaries stable.

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- shared `Application/` abstractions/models
- migration conventions
- core architecture tests

The merged baseline already provides the persistence needed for the planned workflows. Member 1 remains the default coordinator for genuine EF Core schema/migration changes but should not absorb later members' business logic.

Flow 3 must not create speculative analytics entities or competing migrations. If reporting exposes a genuine persistence gap, Member 2 documents the requirement and coordinates the schema change through Member 1.

### Member 2 - Document Management + Report & Statistics

Primary responsibilities:

- merged Flow 1 request/presentation side
- pending Flow 3 Report & Statistics

#### Flow 1 request/presentation side - MERGED

Merged scope includes:

- document list and chapter filtering
- document upload/details/edit/removal/re-index request
- chapter list/create/edit/removal for PRN222
- PDF/DOCX/PPTX upload validation
- 50 MB upload size limit
- source-file persistence under configured upload storage
- creation/update of `Document` metadata
- runtime creation/update/removal of PRN222 `Chapter` rows through the existing model
- server-side validation that a selected `ChapterId` belongs to PRN222
- server-side enforcement of `AppPolicies.ManageDocuments`
- enqueueing persisted document IDs through `IDocumentIndexingQueue`
- cleanup of a newly written source file when database persistence fails

Chapter Management is part of Flow 1. The Subject Leader can change the PRN222 chapter structure at runtime without editing seed data or generating a migration simply because the course outline changed.

When removing a Chapter, the application must preserve documents. Referenced documents are explicitly unassigned from the chapter before the chapter record is removed. The restrictive `Document -> Chapter` relationship remains intentional; do not convert this into cascade deletion.

Member 2 must not parse, chunk, embed or call Ollama inside Razor Page handlers.

See `docs/member-2-document-management-handoff.md` for the exact Flow 1 handoff to Member 3.

#### Flow 3 Report & Statistics - NEW / PENDING

Member 2 owns the independent reporting workflow after the Flow 1 request-side work has merged.

Initial Flow 3 scope is intentionally read-only:

- Subject-Leader Reports/Statistics page
- total PRN222 chapters/documents
- document counts grouped by indexing status
- document counts grouped by chapter, including unassigned documents
- total chat sessions/messages/citations from persisted Flow 2 data
- empty/zero-data states while later workflows are still pending

Prefer aggregate/no-tracking EF Core queries over the existing model. The first version must not require custom analytics storage, event tracking, scheduled aggregation, or a new infrastructure service.

Member 2 must not implement Flow 3 by changing Member 3 indexing behavior, Member 4 RAG behavior, or Member 5 chat/history UI. Flow 3 pages are read-only and must not mutate documents, chapters, indexing state, chat sessions, messages, or citations.

Use a separate focused branch such as:

```text
feature/report-statistics
```

See `docs/flow-3-report-statistics-handoff.md` for detailed acceptance criteria and integration rules.

### Member 3 - Document Indexing / Ingestion - NEXT / PENDING

Primary responsibility: Flow 1 background processing side.

Owns:

- PDF/DOCX/PPTX text extraction
- chunking strategy
- final `IDocumentIndexingQueue` integration
- hosted/background indexing worker
- `IDocumentIndexingService` implementation
- `ITextEmbeddingService` Ollama implementation
- coherent replacement/persistence of `DocumentChunk` rows
- `DocumentIndexStatus`, `IndexError`, and `IndexedAtUtc` transitions

Expected state flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-indexing must replace stale chunks coherently rather than append duplicate chunks.

Integration requirement: keep the existing Member 2 handoff stable. Upload/re-index actions enqueue a persisted `Document.Id`; Member 3 consumes that ID in the background. The temporary `InMemoryDocumentIndexingQueue` and current DI registration may be replaced when the real worker is introduced.

Member 3 must not move parsing/chunking/embedding work into MVC/Razor request handlers and does not own reporting/dashboard work.

### Member 4 - RAG / Chat Backend - PENDING

Primary responsibility: Flow 2 backend.

Owns:

- question embedding through `ITextEmbeddingService`
- pgvector similarity retrieval
- top-K context selection
- grounded prompt construction
- explicit out-of-scope/no-evidence behavior
- `IChatCompletionService` Ollama implementation
- `IRagQueryService` implementation
- persistence of user/assistant messages and `MessageCitation` rows

Member 4 must validate that the supplied chat session belongs to the supplied authenticated user before reading/writing the conversation.

Retrieval evidence should come from successfully indexed PRN222 chunks, not raw uploaded files.

Member 4 does not own aggregate reporting queries or report presentation.

### Member 5 - Chat UI / Conversation Management / Evaluation - PENDING

Primary responsibility: Flow 2 presentation and evaluation deliverable.

Owns:

- chat UI
- chat-session/history UI
- source/citation rendering
- session creation/opening/navigation
- persisted conversation-history presentation
- `evaluation/` 50-question human-authored ground-truth set
- evaluation-facing tooling/tests

Member 5 must call `IRagQueryService` rather than Ollama/pgvector directly.

Conversation history is explicitly part of Flow 2. Member 5 does not own Flow 3 reporting pages.

## Shared integration contracts

The stable handoff points are under:

```text
src/PRN222.RagAssistant/Application/
```

Current contracts:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Treat these public signatures as cross-member integration points. Prefer additive changes. If a signature genuinely must change, update affected producers and consumers together.

Flow 3 should not introduce a cross-member contract unless a concrete implementation need justifies it. A report-specific read-only implementation can remain within its feature boundary.

## Current Flow 1 integration

The request-side portion below is merged:

```text
Member 2 - MERGED
Manage Chapters
    |
    +--> Create/Edit PRN222 Chapter
    |
    +--> Remove Chapter
            |
            +--> if referenced: Document.ChapterId = null
            +--> remove Chapter

Upload/validate/save source
        |
        +--> optional validated ChapterId
        |
        v
Persist Document
        |
        v
IDocumentIndexingQueue.EnqueueAsync(documentId)
        |
        v
Temporary InMemoryDocumentIndexingQueue
        |
        v
Member 3 - PENDING
Hosted worker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
```

The temporary queue is only an integration bridge. Flow 1 is not considered end-to-end complete until Member 3 processes queued documents into indexed chunks and persists correct index state.

## Flow 2 handoff

```text
Member 5 UI / conversation history
        |
        v
IRagQueryService.AskAsync(userId, sessionId, question)
        |
        v
Member 4 RAG backend
        |
        +--> ITextEmbeddingService
        +--> pgvector retrieval
        +--> IChatCompletionService
        +--> persist messages/citations
        |
        v
RagAnswer + RagCitation[]
```

Conversation-history navigation remains a presentation concern owned by Member 5 within Flow 2.

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

Flow 3 must tolerate data sources being empty while Members 3-5 are still pending. It must not create dependencies that block their work.

## Database coordination

The baseline already includes persistence for the planned workflows. `Chapter` contains `Id`, `SubjectId`, `Number`, and `Title`; `Document.ChapterId` is nullable; and `(SubjectId, Number)` is unique. Runtime Chapter CRUD therefore does not require a schema change by itself.

The existing `Document`, `DocumentChunk`, `ChatSession`, `ChatMessage`, and `MessageCitation` persistence is the default source for Flow 3 aggregates. Do not add an analytics schema merely to count existing records.

Do not change delete behavior merely to simplify Chapter Management. Explicitly preserving/unassigning documents is already part of the merged Member 2 application workflow.

If a later workflow genuinely requires a schema change:

1. Explain the missing persistence requirement.
2. Update the entity and its dedicated EF configuration together.
3. Keep entities free of navigation properties and EF mapping attributes.
4. Synchronize with the latest `master` before generating a migration.
5. Generate one EF Core migration and commit it with the model change.
6. Run the pending-model check and tests.

Avoid parallel migrations from separate branches when possible. Member 1 is the default coordinator for schema changes.

## Branch and PR guidance

Recommended branches for the remaining work:

```text
feature/document-indexing
feature/rag-chat
feature/chat-ui-history
feature/report-statistics
```

Member 2's document-management branch has already been merged. Flow 3 must use its own reporting branch rather than reopening or mixing unrelated changes into the old document-management work.

Each remaining workflow should be merged through a focused pull request. Avoid unrelated architecture refactors.

## Required reading before coding

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/flow-3-report-statistics-handoff.md
```

Before handing off, run the relevant build/tests and report any remaining warnings or blockers. After a major member workflow is merged, update `docs/project-status.md`, this workflow document, `README.md`, and relevant agent/handoff instructions so the next member works against the actual merged baseline.
