# Application-layer instructions

This subtree contains stable contracts shared by the document-management, indexing, RAG, and chat presentation workflows.

## Dependency boundaries

- Presentation code (MVC/Razor Pages) may depend on `Application/Abstractions` and `Application/Models`.
- Application abstractions must not depend on Razor Pages, MVC controllers, `HttpContext`, Ollama-specific DTOs, or PostgreSQL-specific query types.
- Infrastructure implementations may depend on these abstractions.
- Do not duplicate a shared contract inside a feature folder when an abstraction already exists here.

## Current shared contracts

- `IDocumentIndexingQueue`: handoff from document upload/re-index actions to the background indexing worker.
- `IDocumentIndexingService`: one-document indexing pipeline executed by the worker.
- `ITextEmbeddingService`: provider-neutral text embedding boundary shared by indexing and retrieval.
- `IChatCompletionService`: provider-neutral chat-generation boundary used by the RAG workflow.
- `IRagQueryService`: presentation-facing boundary for asking a grounded question in an existing chat session.

## Ownership expectations

### Document management workflow

Persist the `Document` record and source file first, then enqueue only the persisted `Document.Id` through `IDocumentIndexingQueue`. Do not parse or embed documents in the request handler.

### Indexing workflow

Implement the queue and `IDocumentIndexingService`. The indexing service owns status transitions (`Uploaded` -> `Processing` -> `Indexed`/`Failed`), parsing, chunk replacement, embeddings, and `IndexedAtUtc`/`IndexError` updates.

### RAG workflow

Use `ITextEmbeddingService` for the question embedding and `IChatCompletionService` for generation. Implement `IRagQueryService` so it validates that the session belongs to the supplied user, persists user/assistant messages and citations, and returns `RagAnswer`.

### Presentation/chat workflow

Depend on `IRagQueryService`; do not call Ollama or pgvector directly from controllers, Razor Page models, or browser JavaScript.

## Contract changes

These interfaces are cross-member integration points. Prefer additive changes. If a signature must change, update all consumers/implementations in the same pull request or coordinate the change before merging.
