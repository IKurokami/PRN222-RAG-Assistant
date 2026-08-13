# Application-layer instructions

This subtree contains stable contracts shared by document management, indexing, RAG, and presentation workflows.

The project defines:

1. **Flow 1 - Document Management & Indexing** - complete
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending
3. **Flow 3 - Report & Statistics** - pending

Conversation History belongs to Flow 2. Flow 3 is a separate read-only reporting workflow and should not reshape shared contracts unnecessarily.

## Current integration status

Member 1 established the shared contracts. Member 2 consumes `IDocumentIndexingQueue` from the merged document upload/re-index flow. Member 3 has now completed and merged the indexing implementation through PR #9.

The active Flow 1 handoff is:

```text
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
IDocumentIndexingService.IndexAsync(documentId)
```

`InMemoryDocumentIndexingQueue` is an in-process transport consumed by the worker. It is not a durable broker. The worker recovers persisted `Uploaded`/`Processing` documents at startup by re-enqueueing them.

Member 4 is now the next main consumer of the completed indexing boundary. Member 2 may implement Flow 3 in parallel using read-only aggregate queries.

See:

- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/member-3-document-indexing-handoff.md`
- `docs/flow-3-report-statistics-handoff.md`

## Dependency boundaries

- Presentation code may depend on `Application/Abstractions` and `Application/Models`.
- Application abstractions must not depend on Razor Pages, MVC controllers, `HttpContext`, Ollama-specific DTOs, or PostgreSQL-specific query types.
- Infrastructure implementations may depend on these abstractions.
- Do not duplicate a shared contract inside a feature folder when an abstraction already exists here.
- Member 2 request handlers must not parse/chunk/embed documents.
- Member 4 must not parse raw uploaded files or duplicate the indexing pipeline.
- Member 5 presentation must not call Ollama or query pgvector directly.
- Flow 3 must not call Ollama, run similarity retrieval, mutate indexing state, or change a shared contract solely for dashboard convenience.

## Current shared contracts

### `IDocumentIndexingQueue`

Handoff from document upload/re-index actions to the merged background indexing worker.

### `IDocumentIndexingService`

One-document indexing pipeline executed by `DocumentIndexingWorker`. Member 3 provides the merged implementation.

### `ITextEmbeddingService`

Provider-neutral text embedding boundary shared by indexing and retrieval.

It supports:

- single-text embedding for Member 4 question retrieval
- ordered batch embedding for Member 3 document indexing

The same configured embedding model must be used for indexing and retrieval.

### `IChatCompletionService`

Provider-neutral chat-generation boundary for Member 4's RAG implementation.

### `IRagQueryService`

Presentation-facing boundary for asking a grounded question in an existing chat session. Member 4 owns the implementation; Member 5 owns the presentation consumer.

### `RagAnswer` / `RagCitation`

Presentation-safe result models for Flow 2.

There is intentionally no reporting-specific shared contract yet. Initial Flow 3 aggregates should use focused read-only EF Core queries unless a concrete reusable application-layer need justifies an abstraction.

## Ownership expectations

### Flow 1 request/presentation - Member 2 - COMPLETE

Member 2:

- validates/stores uploads
- persists `Document`
- manages Chapters
- enqueues persisted document IDs

Do not move background indexing into request handlers.

### Flow 1 indexing - Member 3 - COMPLETE

Merged PR #9 provides:

- parser factory and PDF/DOCX/PPTX parsers
- chunking
- embedding batching
- `OllamaTextEmbeddingService`
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- chunk replacement/persistence
- index-state transitions
- startup rehydration

Member 3's output for downstream consumers is persisted successfully indexed `DocumentChunk` rows plus document index state.

### Flow 2 RAG backend - Member 4 - PENDING

Member 4 should use the existing boundaries to:

- embed questions with `ITextEmbeddingService.EmbedAsync`
- retrieve successfully indexed PRN222 chunks through pgvector
- build grounded context
- implement `IChatCompletionService`
- implement `IRagQueryService`
- validate chat-session ownership
- persist messages/citations
- provide explicit no-evidence/out-of-scope behavior

Do not parse raw source files or create a second embedding/indexing path.

### Flow 2 presentation - Member 5 - PENDING

Member 5 depends on `IRagQueryService` and owns:

- chat/session UI
- Conversation History
- citation rendering
- evaluation-facing presentation/tooling

Do not expose provider or pgvector details directly to browser/UI code.

### Flow 3 Report & Statistics - Member 2 - PENDING

The initial reporting workflow is read-only and may aggregate:

- chapters
- documents/indexing status
- document/chapter grouping
- chunks where useful
- chat sessions/messages/citations

Because Member 3 is complete, document/indexing aggregates can use real merged data immediately.

Flow 3 must not:

- enqueue or process indexing work
- mutate index status
- perform similarity retrieval
- call Ollama
- mutate chat/session/message/citation data
- duplicate Member 5 conversation pages
- create speculative analytics schema
- force shared-contract changes

## Contract changes

These interfaces are cross-member integration points. Prefer additive changes.

If a signature must change:

1. explain why the current contract cannot represent the requirement;
2. coordinate with current producers/consumers;
3. update all affected implementations/consumers together;
4. update `docs/project-status.md`, `docs/team-workflow.md`, and relevant handoff docs.

Do not change a shared contract merely to make one implementation more convenient.