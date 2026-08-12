# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. It complements the code-level rules in `AGENTS.md` and the snapshot in `docs/project-status.md`.

## Current milestone

As of the merge of PR #5 into `master`:

- Member 1 core/data baseline is complete.
- Member 2 Document Management and Chapter Management request/presentation work is merged.
- Member 3 indexing/ingestion implementation is the next major integration step.
- Members 4 and 5 remain dependent on indexed document data and the existing shared contracts.

Member 2's merged flow already calls `IDocumentIndexingQueue`. The currently registered `InMemoryDocumentIndexingQueue` is a temporary integration stub and is not the final Member 3 worker/indexing implementation.

## Product workflows

The project uses three functional workflows:

1. **Document Management & Indexing** - Subject Leader manages PRN222 chapters, uploads course material, the system stores it, indexes it, and exposes indexing state.
2. **RAG Question & Answer** - Student asks a question, the system retrieves relevant indexed chunks, generates a grounded answer, and returns citations.
3. **Conversation History** - Student creates/reopens chat sessions and reviews persisted messages and citations.

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

The merged baseline already provides the persistence needed for later workflows. Member 1 remains the default coordinator for genuine EF Core schema/migration changes but should not absorb later members' business logic.

### Member 2 - Document Management - MERGED

Primary responsibility: Flow 1 request/presentation side.

Merged scope now includes:

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

Member 2 must not be extended to parse, chunk, embed or call Ollama inside Razor Page handlers.

See `docs/member-2-document-management-handoff.md` for the exact handoff to Member 3.

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

Member 3 must not move parsing/chunking/embedding work into MVC/Razor request handlers.

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

### Member 5 - Chat UI / History / Evaluation - PENDING

Primary responsibility: Flow 2 presentation + Flow 3 + evaluation deliverable.

Owns:

- chat UI
- chat-session/history UI
- source/citation rendering
- session creation/opening/navigation
- `evaluation/` 50-question human-authored ground-truth set
- evaluation-facing tooling/tests

Member 5 must call `IRagQueryService` rather than Ollama/pgvector directly.

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

## Current Flow 1 integration

The request-side portion below is now merged:

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
Member 5 UI
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

## Database coordination

The baseline already includes persistence for the planned workflows. `Chapter` contains `Id`, `SubjectId`, `Number`, and `Title`; `Document.ChapterId` is nullable; and `(SubjectId, Number)` is unique. Runtime Chapter CRUD therefore does not require a schema change by itself.

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
```

Member 2's document-management branch has already been merged. Do not create a competing upload/chapter implementation branch unless the team explicitly decides to replace the merged flow.

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
```

Before handing off, run the relevant build/tests and report any remaining warnings or blockers. After a major member workflow is merged, update `docs/project-status.md` and this workflow document so the next member works against the actual merged baseline.
