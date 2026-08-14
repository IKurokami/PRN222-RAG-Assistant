# Member 2 - Document Management and Report & Statistics handoff

## Status

Member 2 owns two completed responsibilities:

1. **Flow 1 request/presentation side - COMPLETE - MVC Controllers + Views**
2. **Flow 3 Report & Statistics - COMPLETE / merged through PR #12 - Razor Pages**

Member 3's PR #9 completed the downstream indexing side of Flow 1. The later Flow 1 presentation migration changes Razor Page handlers to MVC controllers/views without changing the indexing handoff or persistence model.

For the canonical whole-project snapshot, see `docs/project-status.md`.

## Completed Flow 1 request/presentation scope

### Current MVC structure

```text
Controllers/
├── DocumentsController.cs
└── ChaptersController.cs

Models/
├── Documents/DocumentViewModels.cs
└── Chapters/ChapterViewModels.cs

Views/
├── Documents/
└── Chapters/
```

The old `Pages/Documents/` and `Pages/Chapters/` implementations are removed. Flow 1 has one presentation implementation only.

### Chapter Management

`ChaptersController` + `Views/Chapters/` provide:

- list
- create
- edit
- delete confirmation and delete

Important behavior:

- authenticated list access
- create/edit/delete require `AppPolicies.ManageDocuments`
- POST actions use anti-forgery validation
- chapter number/title validation
- duplicate chapter-number checks within PRN222
- runtime-managed chapters rather than seed-only chapters
- documents are preserved when a chapter is removed
- referenced `Document.ChapterId` values are cleared in the delete transaction before the chapter is removed

### Document Management

`DocumentsController` + `Views/Documents/` provide:

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
- server-side validation that optional `ChapterId` belongs to PRN222
- enqueueing persisted `Document.Id` through `IDocumentIndexingQueue`
- cleanup of a newly written source file when database persistence fails

Document list/details remain available to authenticated users; management actions remain Subject-Leader-only.

Delete behavior remains DB-first: the metadata row is committed first, then physical file cleanup is best-effort and only logs a warning on cleanup failure.

See `docs/flow-1-mvc-migration.md` for route mapping.

## Member 2 -> Member 3 indexing handoff - fulfilled

The active Flow 1 path is:

```text
DocumentsController upload / re-index
        |
        v
Persist Document / update index state
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

The MVC migration does not move parsing/chunking/embedding into the controller. Those responsibilities remain behind the existing indexing services.

The queue remains process-local. Startup recovery re-enqueues persisted documents marked `Uploaded` or `Processing`.

## Flow 3 - Report & Statistics - complete

Flow 3 remains a separate Razor Pages workflow under:

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
```

It is protected server-side by:

```text
AppPolicies.ManageDocuments
```

Current metrics include:

- total PRN222 chapters/documents
- documents grouped by `DocumentIndexStatus`
- documents grouped by chapter and unassigned documents
- total persisted `DocumentChunk` count
- indexing completion percentage
- recent indexing failures with `IndexError`
- recently indexed documents with chunk counts/timestamps
- total chat sessions/messages/citations
- graceful zero/empty states while Flow 2 has no data

Flow 3 remains read-only and must not mutate workflow state, enqueue/re-index documents, perform pgvector retrieval, call Ollama, or create speculative analytics persistence.

## Handoff to Members 4 and 5

The remaining product implementation is Flow 2.

### Member 4

Builds presentation-agnostic RAG retrieval/generation on successfully indexed chunks and the shared application contracts.

### Member 5

Owns MVC Flow 2 presentation:

- chat/session controller actions
- MVC views
- session navigation
- Conversation History
- citation rendering
- evaluation deliverable

Because Flow 1 now also uses MVC, Member 5 must add focused Flow 2 controller/views without reusing or overloading `DocumentsController`/`ChaptersController` for chat responsibilities.

Expected Flow 2 presentation area:

```text
src/PRN222.RagAssistant/Controllers/ChatController.cs
src/PRN222.RagAssistant/Views/Chat/
```

Do not create `Pages/Chat` or `Pages/Conversation`.

## Tests

Flow 1 tests now validate MVC input models/controllers instead of Razor `PageModel` classes. Regression coverage includes validation rules, queue behavior, chapter-delete safety, authorization attributes on write actions, and anti-forgery attributes on POST actions.

No EF Core migration is required for the MVC conversion.

## Read before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/flow-1-mvc-migration.md
docs/member-1-core-data-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

Member 2 should be treated as complete for Flow 1 request-side and Flow 3 reporting work unless a new requirement explicitly reopens those scopes.
