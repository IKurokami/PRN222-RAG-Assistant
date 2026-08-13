# Member 2 - Document Management and Report & Statistics handoff

## Status

Member 2's Flow 1 request/presentation work is merged into `master` through PR #5 at merge commit `4a038bbf428cf96eafb97846f5a00904f9d78b63`. The PR head commit `b5681e026de0dac94de0f04d8644305cff047a3b` passed CI before merge.

Member 2 now has two clearly separated responsibilities:

1. **Flow 1 request/presentation side - COMPLETE / MERGED**
2. **Flow 3 - Report & Statistics - NEW / PENDING**

The new Flow 3 assignment must not be used as a reason to reopen or redesign the already merged Flow 1 implementation. Later members should integrate with the existing Flow 1 handoff instead of creating a second document-management flow.

Conversation History is part of **Flow 2 - RAG Question & Answer & Conversation Management** and is owned on the presentation side by Member 5. It is not counted as the independent third workflow.

## Completed Chapter Management scope

Razor Pages under `Pages/Chapters/` now provide runtime PRN222 chapter listing, creation, editing and removal.

Important behavior:

- write pages require `AppPolicies.ManageDocuments`
- chapter number and title are validated
- duplicate chapter numbers are checked within PRN222
- chapters are runtime-managed data, not a fixed seed-only list
- documents remain in the system when a chapter is removed; referenced `Document.ChapterId` values are cleared as part of the application workflow
- the existing restrictive EF relationship remains intentional

## Completed Document Management scope

Razor Pages under `Pages/Documents/` now provide:

- document list and chapter filtering
- upload
- details
- metadata/chapter editing
- removal
- re-index request

Upload behavior includes:

- Subject Leader authorization
- PDF/DOCX/PPTX validation
- 50 MB size limit
- configured source-file storage
- `Document` metadata persistence with initial `Uploaded` status
- optional validation that the selected chapter belongs to PRN222
- enqueueing the persisted `Document.Id` through `IDocumentIndexingQueue`
- cleanup of a newly written source file if database persistence fails

Students may read document list/details without management actions.

## Temporary queue integration

`Infrastructure/Services/InMemoryDocumentIndexingQueue.cs` is intentionally temporary. It exists only to complete the Member 2 -> Member 3 handoff through `IDocumentIndexingQueue`.

It is not a real indexing subsystem and contains no document parsing, chunking, embedding or Ollama workflow.

## Handoff to Member 3

Member 3 owns the background side of Flow 1:

- final queue/worker integration
- hosted background worker
- `IDocumentIndexingService`
- PDF/DOCX/PPTX extraction
- chunking
- `ITextEmbeddingService`
- `DocumentChunk` replacement/persistence
- index status, error and timestamp transitions

Expected state flow remains `Uploaded -> Processing -> Indexed` with `Failed` as the error state.

The Member 2 request flow should continue to enqueue a persisted document ID. Member 3 may replace the temporary queue class and its DI registration, but should preserve the shared queue contract unless all consumers are migrated together.

## Handoff to Members 4 and 5

Member 4 owns the Flow 2 retrieval and grounded-generation backend on indexed chunks behind the existing shared application contracts.

Member 5 owns Flow 2 presentation, including:

- chat UI
- chat-session creation/opening/navigation
- conversation history
- citation rendering
- evaluation deliverable

Neither later workflow should move pgvector or Ollama calls into Member 2 Razor Page handlers.

## Flow 3 - Report & Statistics - NEW / PENDING

Member 2 additionally owns the independent third product workflow after the merged Flow 1 request-side work.

Primary actor: **Subject Leader**.

Goal: inspect read-only aggregate state and usage of the PRN222 RAG Assistant without modifying source workflow records.

Initial Flow 3 scope:

- total PRN222 chapters
- total PRN222 documents
- document counts grouped by indexing status
- document counts grouped by chapter, including unassigned documents
- total chat sessions
- total chat messages
- total persisted citations
- clear zero/empty states while later workflows are still pending

Expected first implementation path:

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

Prefer aggregate, no-tracking EF Core queries over the existing model. Flow 3 should not introduce custom analytics persistence, scheduled aggregation, event tracking, a reporting warehouse, or a separate infrastructure service for the first version.

### Flow 3 non-interference rules

Member 2 must not implement reporting by:

- changing Member 3 parsing/chunking/embedding/worker behavior
- changing Member 4 RAG retrieval or grounded-generation behavior
- duplicating Member 5 chat/history pages
- mutating documents, chapters, indexing state, chat sessions, messages, or citations from report pages
- changing shared `Application/` contracts solely for reporting convenience
- creating speculative analytics entities or EF migrations

If a genuine persistence gap is discovered, document the requirement first and coordinate it through Member 1, who remains the schema/migration coordinator.

Flow 3 should be implemented in a separate focused branch such as:

```text
feature/report-statistics
```

See `docs/flow-3-report-statistics-handoff.md` for detailed acceptance criteria.

## Relevant tests merged with Member 2

- `ChapterAuthorizationTests.cs`
- `ChapterManagementTests.cs`
- `DocumentManagementTests.cs`

They cover request-side authorization, chapter validation and safety rules, upload validation and temporary queue behavior.

The future Flow 3 PR should add focused tests for aggregate/query behavior and Subject Leader access without changing the existing Flow 1 tests unnecessarily.

## Read before continuing

- `AGENTS.md`
- `src/PRN222.RagAssistant/Application/AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/infrastructure.md`
- `docs/member-1-core-data-handoff.md`
- `docs/flow-3-report-statistics-handoff.md`
