# Application-layer instructions

This subtree contains stable contracts shared by the document-management, indexing, RAG, and chat presentation workflows.

## Current integration status

Member 1 established these shared contracts. Member 2 is now merged and actively consumes `IDocumentIndexingQueue` from the document upload/re-index flow.

The repository currently contains `Infrastructure/Services/InMemoryDocumentIndexingQueue.cs` as a **temporary Member 2 integration stub**. It is not the final indexing subsystem. Member 3 owns the hosted worker, real indexing implementation, parser/chunker/embedding pipeline, and any replacement/integration of that temporary queue implementation.

Do not redesign the Member 2 upload flow simply because indexing is still pending. The intended handoff is already established:

```text
Persist Document
    |
    v
IDocumentIndexingQueue.EnqueueAsync(documentId)
    |
    v
Member 3 background worker
    |
    v
IDocumentIndexingService.IndexAsync(documentId)
```

See `docs/project-status.md` and `docs/member-2-document-management-handoff.md` for the current project milestone.

## Dependency boundaries

- Presentation code (MVC/Razor Pages) may depend on `Application/Abstractions` and `Application/Models`.
- Application abstractions must not depend on Razor Pages, MVC controllers, `HttpContext`, Ollama-specific DTOs, or PostgreSQL-specific query types.
- Infrastructure implementations may depend on these abstractions.
- Do not duplicate a shared contract inside a feature folder when an abstraction already exists here.
- Do not move parser, chunker, pgvector, or Ollama implementation logic into Member 2 request handlers.

## Current shared contracts

- `IDocumentIndexingQueue`: handoff from the merged document upload/re-index actions to the Member 3 background indexing worker.
- `IDocumentIndexingService`: one-document indexing pipeline executed by the worker.
- `ITextEmbeddingService`: provider-neutral text embedding boundary shared by indexing and retrieval.
- `IChatCompletionService`: provider-neutral chat-generation boundary used by the RAG workflow.
- `IRagQueryService`: presentation-facing boundary for asking a grounded question in an existing chat session.
- `RagAnswer` / `RagCitation`: presentation-safe RAG result models used by the chat UI.

## Ownership expectations

### Document management workflow - Member 2 - MERGED

The merged flow persists the source file and `Document` record first, validates an optional PRN222 chapter assignment, then enqueues only the persisted `Document.Id` through `IDocumentIndexingQueue`.

Document/Chapter Management is now an existing consumer of this application layer. Later members should preserve that handoff rather than creating a second upload/indexing entry point.

Member 2 request handlers must not parse or embed documents.

### Indexing workflow - Member 3 - PENDING

Implement the background side behind the existing contracts:

- queue/worker integration
- `IDocumentIndexingService`
- parsing
- chunk replacement
- embeddings through `ITextEmbeddingService`
- index-state transitions (`Uploaded` -> `Processing` -> `Indexed`/`Failed`)
- `IndexedAtUtc` and `IndexError` updates

Member 3 may replace the temporary `InMemoryDocumentIndexingQueue` and its DI registration, but should keep `IDocumentIndexingQueue` stable unless all consumers are changed together.

### RAG workflow - Member 4 - PENDING

Use `ITextEmbeddingService` for question embedding and `IChatCompletionService` for generation.

Implement `IRagQueryService` so it:

- validates that the session belongs to the supplied authenticated user
- persists user/assistant messages
- retrieves successfully indexed PRN222 evidence
- persists source citations
- returns `RagAnswer`
- provides explicit no-evidence/out-of-scope behavior when grounding is insufficient

### Presentation/chat workflow - Member 5 - PENDING

Depend on `IRagQueryService`; do not call Ollama or pgvector directly from controllers, Razor Page models, or browser JavaScript.

Use `RagAnswer` and `RagCitation` rather than leaking persistence entities/provider DTOs into the UI.

## Contract changes

These interfaces are cross-member integration points. Prefer additive changes.

If a signature must change:

1. explain why the existing contract cannot represent the requirement;
2. coordinate with the current producer/consumer owners;
3. update all affected consumers and implementations together;
4. update `docs/project-status.md`, `docs/team-workflow.md`, and the relevant handoff document when the change affects member boundaries.

Do not change a shared contract merely to make one implementation more convenient.
