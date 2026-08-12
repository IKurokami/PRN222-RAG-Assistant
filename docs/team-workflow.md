# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. It complements the code-level rules in `AGENTS.md`.

## Product workflows

The project uses three functional workflows:

1. **Document Management & Indexing** - Subject Leader manages PRN222 chapters, uploads course material, the system stores it, indexes it, and exposes indexing state.
2. **RAG Question & Answer** - Student asks a question, the system retrieves relevant indexed chunks, generates a grounded answer, and returns citations.
3. **Conversation History** - Student creates/reopens chat sessions and reviews persisted messages and citations.

## Member responsibilities

### Member 1 - Core/Data Lead

Primary responsibility: keep the shared model, security, persistence, and integration boundaries stable.

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- shared `Application/` abstractions/models
- migration conventions
- core architecture tests

Member 1 does not own the later workflow implementations. The baseline should expose contracts without prematurely implementing Member 2-4 business logic.

### Member 2 - Document Management

Primary responsibility: Flow 1 request/presentation side.

Owns:

- document list/upload/details/delete/re-index UI
- chapter list/create/edit/delete UI for PRN222
- PDF/DOCX/PPTX upload validation
- source-file persistence under configured upload storage
- creation/update of `Document` metadata
- creation/update/deletion of PRN222 `Chapter` records through the existing domain model
- server-side validation that any selected `ChapterId` belongs to PRN222
- server-side enforcement of `AppPolicies.ManageDocuments`
- enqueueing persisted document IDs through `IDocumentIndexingQueue`

Chapter Management is part of Flow 1. The Subject Leader must be able to create or reorganize chapters at runtime without editing seed data or requiring a migration when the course outline changes.

When deleting a Chapter:

1. Keep the existing `Document -> Chapter` relationship protected by `DeleteBehavior.Restrict`.
2. If no documents reference the Chapter, delete it normally.
3. If documents reference the Chapter, require an explicit user confirmation.
4. In one coherent application transaction, set those documents' nullable `ChapterId` values to `null`, then delete the Chapter.
5. Never cascade-delete documents as a side effect of deleting a Chapter.

Must not:

- parse documents in controllers/PageModels
- chunk or embed files in request handlers
- call Ollama directly
- write directly to `DocumentChunk`

### Member 3 - Document Indexing / Ingestion

Primary responsibility: Flow 1 background processing side.

Owns:

- PDF/DOCX/PPTX text extraction
- chunking strategy
- `IDocumentIndexingQueue` implementation
- hosted/background indexing worker
- `IDocumentIndexingService` implementation
- `ITextEmbeddingService` Ollama implementation
- replacement/persistence of `DocumentChunk` rows
- `DocumentIndexStatus`, `IndexError`, and `IndexedAtUtc` transitions

Expected state flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-indexing must replace stale chunks coherently rather than append duplicate chunk indexes.

### Member 4 - RAG / Chat Backend

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

Must validate that the supplied chat session belongs to the supplied authenticated user before reading/writing the conversation.

### Member 5 - Chat UI / History / Evaluation

Primary responsibility: Flow 2 presentation + Flow 3 + evaluation deliverable.

Owns:

- chat UI
- chat-session/history UI
- source/citation rendering
- session creation/opening/navigation
- `evaluation/` 50-question human-authored ground-truth set
- evaluation-facing tooling/tests

Must call `IRagQueryService` rather than Ollama/pgvector directly.

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

### Flow 1 request-side organization

```text
Member 2
Manage Chapters
    |
    +--> Create/Edit PRN222 Chapter
    |
    +--> Delete Chapter
            |
            +--> if referenced: Document.ChapterId = null
            +--> delete Chapter

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
Member 3 worker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
```

### Flow 2 handoff

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

The baseline already includes persistence for the planned workflows. `Chapter` already contains `Id`, `SubjectId`, `Number`, and `Title`; `Document.ChapterId` is nullable; and `(SubjectId, Number)` is already unique. Runtime Chapter CRUD therefore does not require a schema change by itself.

Do not change delete behavior merely to implement Chapter Management. The current restrictive FK is intentional; explicit unlinking of documents belongs in the Member 2 application workflow.

If a later workflow genuinely requires a schema change:

1. Explain the missing persistence requirement.
2. Update the entity and its dedicated EF configuration together.
3. Keep entities free of navigation properties and EF mapping attributes.
4. Synchronize with the latest integration branch before generating a migration.
5. Generate one EF Core migration and commit it with the model change.
6. Run the pending-model check and tests.

Avoid parallel migrations from separate branches when possible. Member 1 is the default coordinator for schema changes.

## Branch and PR guidance

Recommended feature branches:

```text
feature/document-management
feature/document-indexing
feature/rag-chat
feature/chat-ui-history
```

Chapter Management belongs in the document-management workflow/branch unless the team intentionally splits it into a dedicated follow-up PR owned by Member 2.

Each workflow should be merged through a pull request. Keep PRs scoped to the member's responsibility and avoid unrelated refactors.

Before coding, each member should read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/team-workflow.md
docs/member-1-core-data-handoff.md
```

Before handing off, run the relevant build/tests and report any remaining warnings or blockers.
