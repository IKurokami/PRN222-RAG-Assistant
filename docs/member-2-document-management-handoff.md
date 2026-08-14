# Member 2 - Document Management and Report & Statistics handoff

## Status

Member 2 owns two separate responsibilities and **both are now complete/merged**:

1. **Flow 1 request/presentation side - COMPLETE / MERGED**
2. **Flow 3 Report & Statistics - COMPLETE / MERGED through PR #12**

Member 3's PR #9 completed the downstream indexing side of Flow 1, so the Member 2 -> Member 3 handoff is fulfilled and Flow 1 is end-to-end implemented.

PR #12 completed Member 2's independent Flow 3 reporting assignment. Conversation History remains part of Flow 2 and belongs to Member 5 on the presentation side. The remaining Flow 2 presentation has now been fixed to **ASP.NET Core MVC Controllers + Views**, not Razor Pages.

For the canonical whole-project snapshot, see `docs/project-status.md`.

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

Member 3 merged the background indexing implementation through PR #9.

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

The in-memory queue is the active process-local transport consumed by the worker. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

See `docs/member-3-document-indexing-handoff.md` for the completed indexing boundary.

## Flow 3 - Report & Statistics - COMPLETE / MERGED

PR #12 merged the independent third workflow at master commit:

```text
00903a38693956f59090f71649ca8a99e053e604
```

Primary actor: **Subject Leader**.

Goal: inspect read-only aggregate state/usage of the PRN222 RAG Assistant.

### Merged report pages

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
```

The shared layout now exposes a Reports navigation entry to Subject Leaders.

Server-side access uses:

```text
[Authorize(Policy = AppPolicies.ManageDocuments)]
```

The policy requires `SubjectLeader`; UI visibility is not relied on as the authorization boundary.

### Implemented metrics

The report now includes:

- total PRN222 chapters
- total PRN222 documents
- documents grouped by `DocumentIndexStatus`
- documents grouped by chapter
- unassigned document count
- total persisted `DocumentChunk` count
- recent indexing failures using existing `IndexError`
- recently indexed documents using existing `IndexedAtUtc` plus chunk count
- total chat sessions
- total chat messages
- total persisted citations
- graceful zero/empty states while Flow 2 has no chat data

The page uses read-only EF Core aggregate queries and `AsNoTracking()` where appropriate.

### Flow 3 boundary

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

Flow 3 remains a consumer of existing persisted state. It does not participate in indexing or RAG generation.

### Non-interference rules

Future Member 2/reporting changes must not:

- enqueue/re-index documents as part of reporting
- alter parsers/chunker/embedding/worker behavior
- mutate document/chapter/index status
- perform pgvector similarity retrieval
- call Ollama
- duplicate Member 5 chat/history pages
- mutate chat sessions/messages/citations
- introduce analytics entities, denormalized counters, event tracking, a reporting warehouse, or scheduled aggregation solely to show the dashboard
- change shared `Application/` contracts only for convenience

If a genuine persistence gap is found, document it and coordinate schema/migration changes through Member 1.

## Handoff to Members 4 and 5

With Member 2's assigned work complete, the remaining product implementation is Flow 2.

Member 4 builds presentation-agnostic RAG retrieval on successfully indexed chunks and the merged `ITextEmbeddingService`.

Member 5 owns **ASP.NET Core MVC** Flow 2 presentation:

- MVC controller/actions for chat/session workflows
- MVC Views for chat/session UI
- session creation/opening/navigation presentation
- Conversation History
- citation rendering
- evaluation deliverable

Expected presentation areas:

```text
src/PRN222.RagAssistant/Controllers/
src/PRN222.RagAssistant/Views/Chat/
```

Do not create `Pages/Chat`, `Pages/Conversation`, or another Razor Pages implementation for Flow 2.

Neither Member 4 nor Member 5 should put provider/retrieval logic into Member 2 pages, and Flow 2 should not recreate Flow 3 reporting pages. MVC controllers should consume `IRagQueryService` rather than call Ollama/pgvector directly.

## Validation

PR #12 reported `75/75` automated tests passing.

Post-merge local smoke testing also confirmed:

- anonymous access to `/Reports/Index` redirects to login
- Student access is denied
- Subject Leader access succeeds
- a Chapter can be created and a PDF uploaded/indexed through the completed Flow 1 pipeline
- the Flow 3 dashboard updates chapter/document/chunk/indexing metrics from the resulting persisted data

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

Member 2 should now be treated as complete for the currently assigned Flow 1 request-side and Flow 3 reporting work unless a new requirement explicitly reopens those scopes.
