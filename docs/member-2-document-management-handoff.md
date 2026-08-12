# Member 2 - Document Management handoff

## Status

Member 2's work is merged into `master` through PR #5 at merge commit `4a038bbf428cf96eafb97846f5a00904f9d78b63`. The PR head commit `b5681e026de0dac94de0f04d8644305cff047a3b` passed CI before merge.

Member 2 is now the completed request/presentation-side baseline for Flow 1. Later members should integrate with this work instead of creating a second document-management flow.

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

Member 4 should build retrieval and grounded generation on indexed chunks behind the existing shared application contracts. Member 5 should consume `IRagQueryService` for chat/history presentation and citation rendering.

Neither later workflow should move pgvector or Ollama calls into the Member 2 Razor Page handlers.

## Relevant tests merged with Member 2

- `ChapterAuthorizationTests.cs`
- `ChapterManagementTests.cs`
- `DocumentManagementTests.cs`

They cover request-side authorization, chapter validation and safety rules, upload validation and temporary queue behavior.

## Read before continuing

- `AGENTS.md`
- `src/PRN222.RagAssistant/Application/AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/member-1-core-data-handoff.md`
