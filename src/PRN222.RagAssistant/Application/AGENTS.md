# Application-layer instructions

This subtree contains stable contracts shared by document management, indexing, RAG, and presentation workflows.

The project defines:

1. **Flow 1 - Document Management & Indexing** - complete - **MVC Controllers + Views presentation**
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - **MVC Controllers + Views presentation**
3. **Flow 3 - Report & Statistics** - complete - Razor Pages presentation

Conversation History belongs to Flow 2. Flow 3 is a separate read-only workflow and completed without requiring a reporting-specific shared contract.

## Current integration status

Member 1 established the shared contracts. Member 2 consumes `IDocumentIndexingQueue` from the Flow 1 MVC document upload/re-index actions. Member 3 completed the indexing implementation through PR #9. Member 2 completed Flow 3 reporting through PR #12.

The active Flow 1 handoff is:

```text
DocumentsController persists/updates Document
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
IDocumentIndexingService.IndexAsync(documentId)
```

`InMemoryDocumentIndexingQueue` is an in-process transport, not a durable broker. The worker recovers persisted `Uploaded`/`Processing` documents at startup by re-enqueueing them.

Member 4 is the next main consumer of the completed indexing boundary. Member 5 will consume `IRagQueryService` from MVC presentation. Flow 3 is a read-only downstream consumer of existing persistence.

See:

- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/flow-1-mvc-migration.md`
- `docs/member-3-document-indexing-handoff.md`
- `docs/flow-3-report-statistics-handoff.md`

## Dependency boundaries

- Presentation code may depend on `Application/Abstractions` and `Application/Models`.
- Application abstractions must not depend on Razor Pages, MVC controllers, `HttpContext`, Ollama-specific DTOs, or PostgreSQL-specific query types.
- Infrastructure implementations may depend on these abstractions.
- Do not duplicate a shared contract inside a feature folder when an abstraction already exists here.
- Flow 1 MVC controllers must not parse/chunk/embed documents or call Ollama/pgvector directly.
- Member 4 must not parse raw uploaded files or duplicate the indexing pipeline.
- Member 4 Flow 2 backend must remain presentation-agnostic.
- Member 5 Flow 2 presentation must use MVC Controllers + Views rather than Razor Pages.
- Member 5 MVC presentation must not call Ollama or query pgvector directly.
- Flow 3 must not call Ollama, run similarity retrieval, mutate indexing state, or change a shared contract solely for dashboard convenience.

## MVC presentation boundaries

### Flow 1

Flow 1 request/presentation code lives in `DocumentsController`, `ChaptersController`, Flow 1 MVC view models, and `Views/Documents` / `Views/Chapters`.

The MVC layer owns HTTP concerns plus the already-existing request-side persistence/orchestration. Background indexing remains behind `IDocumentIndexingQueue` / `IDocumentIndexingService`.

Do not recreate `Pages/Documents` or `Pages/Chapters` as a parallel implementation.

### Flow 2

Expected path:

```text
Student browser
    |
    v
ChatController / MVC action
    |
    v
IRagQueryService
    |
    v
Flow 2 application/backend services
    |
    v
RagAnswer + RagCitation[]
    |
    v
MVC View
```

The MVC layer may handle model binding, validation, authorization, redirects, and view selection. It must not implement question embeddings, pgvector retrieval, grounded prompt construction, Ollama generation, or indexing behavior.

Do not introduce `Pages/Chat`, `Pages/Conversation`, or another Razor Pages Flow 2 implementation.

## Current shared contracts

### `IDocumentIndexingQueue`

Handoff from Flow 1 document upload/re-index actions to the background indexing worker.

### `IDocumentIndexingService`

One-document indexing pipeline executed by `DocumentIndexingWorker`. Member 3 provides the merged implementation.

### `ITextEmbeddingService`

Provider-neutral embedding boundary shared by indexing and retrieval. It supports single-text embedding for question retrieval and ordered batch embedding for document indexing.

The same configured embedding model must be used for indexing and retrieval.

### `IChatCompletionService`

Provider-neutral chat-generation boundary for Member 4's RAG implementation.

### `IRagQueryService`

Presentation-facing boundary for grounded questions in an existing chat session. Member 4 owns the implementation; Member 5 owns the MVC consumer.

MVC chat controllers should call this boundary rather than implementing retrieval/generation themselves.

### `RagAnswer` / `RagCitation`

Presentation-safe Flow 2 result models.

There is intentionally no reporting-specific shared contract.

## Ownership expectations

### Flow 1 request/presentation - Member 2 - COMPLETE / MVC

Member 2:

- validates/stores uploads
- persists `Document`
- manages Chapters
- enqueues persisted document IDs
- exposes the behavior through MVC controllers/views

Do not move background indexing into request actions.

### Flow 1 indexing - Member 3 - COMPLETE

Provides parsers, chunking, embedding batching, `OllamaTextEmbeddingService`, `DocumentIndexingService`, `DocumentIndexingWorker`, chunk replacement/persistence, indexing state transitions, and startup recovery.

Member 3's downstream output is successfully indexed `DocumentChunk` rows plus document index state.

### Flow 3 Report & Statistics - Member 2 - COMPLETE

The read-only Razor Pages workflow aggregates chapters, documents/indexing state, chunks, recent indexed/failed state, and chat session/message/citation totals.

It must not enqueue/process indexing, mutate workflow data, perform similarity retrieval, call Ollama, duplicate Conversation History, create speculative analytics schema, or force shared-contract changes.

### Flow 2 RAG backend - Member 4 - PENDING

Use the existing boundaries to embed questions, retrieve indexed PRN222 chunks, construct grounded context, implement chat completion/RAG services, validate session ownership, persist messages/citations, and handle insufficient evidence explicitly.

### Flow 2 MVC presentation - Member 5 - PENDING

Owns focused chat/session controllers/views, Conversation History, citations, and evaluation-facing presentation/tooling.

Expected directories:

```text
src/PRN222.RagAssistant/Controllers/ChatController.cs
src/PRN222.RagAssistant/Views/Chat/
```

Do not leak provider/pgvector details to controller/browser code.

## Contract changes

These interfaces are cross-member integration points. Prefer additive changes.

If a signature must change:

1. explain why the current contract cannot represent the requirement;
2. coordinate with producers/consumers;
3. update all affected implementations/consumers together;
4. update project status, team workflow, and relevant handoff docs.

Do not change a shared contract merely to make one controller/action convenient.
