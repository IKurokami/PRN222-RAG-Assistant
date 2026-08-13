# Member 2 - Document Management and Report & Statistics handoff

## Status

Member 2 owns two separate responsibilities:

1. **Flow 1 request/presentation side - COMPLETE / MERGED**
2. **Flow 3 Report & Statistics - PENDING IMPLEMENTATION**

Member 3's PR #9 has now completed the downstream indexing side of Flow 1, so the Member 2 -> Member 3 handoff is fulfilled and Flow 1 is end-to-end implemented.

Conversation History remains part of Flow 2 and belongs to Member 5 on the presentation side.

## Completed Flow 1 request/presentation scope

### Chapter Management

Razor Pages under `Pages/Chapters/` provide:

- list
- create
- edit
- delete

Important behavior:

- write pages require `AppPolicies.ManageDocuments`
- chapter number/title validation
- duplicate chapter-number checks within PRN222
- runtime-managed chapters rather than fixed seed-only data
- documents are preserved when a chapter is removed
- referenced `Document.ChapterId` values are cleared before deleting the chapter
- the restrictive EF relationship remains intentional

### Document Management

Razor Pages under `Pages/Documents/` provide:

- list/filter
- upload
- details
- metadata/chapter edit
- delete
- re-index request

Upload behavior includes:

- Subject Leader authorization
- PDF/DOCX/PPTX validation
- 50 MB limit
- configured source-file storage
- `Document` metadata persistence with initial `Uploaded` status
- optional validation that selected `ChapterId` belongs to PRN222
- enqueueing persisted `Document.Id` through `IDocumentIndexingQueue`
- cleanup of a newly written source file when database persistence fails

Students may read document list/details without management actions.

## Member 2 -> Member 3 handoff - FULFILLED

Member 3 has now merged the background indexing implementation through PR #9.

The active Flow 1 path is:

```text
Member 2 upload / re-index
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
DocumentIndexingWorker
        |
        v
DocumentIndexingService
        |
        +--> parse PDF / DOCX / PPTX
        +--> chunk
        +--> batch embed
        +--> replace DocumentChunk rows
        \--> Indexed / Failed
```

Member 2 must continue to preserve this boundary. Do not move parsing/chunking/embedding into Razor Page handlers.

The in-memory queue is now the active process-local transport consumed by the worker. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

See `docs/member-3-document-indexing-handoff.md` for the completed indexing boundary.

## Handoff to Members 4 and 5

Member 4 now builds Flow 2 retrieval on successfully indexed chunks and the merged `ITextEmbeddingService`.

Member 5 owns Flow 2 presentation:

- chat UI
- session creation/opening/navigation
- Conversation History
- citation rendering
- evaluation deliverable

Neither Member 4 nor Member 5 should put provider/retrieval logic into Member 2 pages.

## Flow 3 - Report & Statistics - PENDING

Member 2 owns the independent third workflow in a separate focused branch.

Primary actor: **Subject Leader**.

Goal: inspect read-only aggregate state/usage of the PRN222 RAG Assistant.

### Initial report scope

At minimum:

- total PRN222 chapters
- total PRN222 documents
- documents grouped by `DocumentIndexStatus`
- documents grouped by chapter
- unassigned document count
- total chat sessions
- total chat messages
- total persisted citations
- clear empty/zero states

Now that Member 3 is merged, indexing metrics are immediately meaningful and can use real persisted states:

- `Uploaded`
- `Processing`
- `Indexed`
- `Failed`

Optional presentation improvements that remain within scope include:

- recent indexing failures with `IndexError`
- recently indexed documents using existing timestamps
- total persisted `DocumentChunk` count

These should still be computed from existing persistence.

### Suggested flow

```text
Subject Leader
      |
      v
Reports / Statistics
      |
      +--> Document overview
      +--> Indexing overview
      +--> Chat usage overview
      |
      v
Read-only dashboard / tables
```

### Implementation guidance

Prefer:

- aggregate EF Core queries
- `AsNoTracking()` for read-only report queries where appropriate
- simple Razor Pages/MVC presentation
- empty-state handling
- focused authorization tests
- focused aggregate/query tests

Do not add complexity solely for reporting.

### Non-interference rules

Flow 3 must not:

- enqueue/re-index documents
- alter parsers/chunker/embedding/worker behavior
- mutate document/chapter/index status
- perform pgvector similarity retrieval
- call Ollama
- duplicate Member 5 chat/history pages
- mutate chat sessions/messages/citations
- introduce analytics entities, denormalized counters, event tracking, a reporting warehouse, or scheduled aggregation solely to show the dashboard
- change shared `Application/` contracts only for convenience

If a genuine persistence gap is found, document it and coordinate the schema/migration through Member 1.

## Suggested branch

```text
feature/report-statistics
```

The Flow 3 PR should remain focused. Do not mix Flow 1 fixes, indexing refactors, RAG work, or chat UI changes into it.

## Relevant existing tests

Member 2's merged tests cover Chapter/Document Management authorization, validation, safety, upload behavior, and queue handoff expectations.

The future Flow 3 PR should add tests for:

- Subject Leader access
- unauthorized/student behavior according to the intended policy
- aggregate correctness
- grouped index-status counts
- chapter/unassigned counts
- empty chat-data states

## Read before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```
