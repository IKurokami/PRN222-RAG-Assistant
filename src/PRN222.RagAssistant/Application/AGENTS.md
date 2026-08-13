# Application-layer instructions

This subtree contains stable contracts shared by the document-management, indexing, RAG, and chat presentation workflows.

The project currently defines three product workflows:

1. **Flow 1 - Document Management & Indexing**
2. **Flow 2 - RAG Question & Answer & Conversation Management**
3. **Flow 3 - Report & Statistics**

Conversation History is part of Flow 2. Flow 3 is a separate read-only reporting workflow and must not be implemented by reshaping the shared contracts unnecessarily.

## Current integration status

Member 1 established these shared contracts. Member 2 is merged and actively consumes `IDocumentIndexingQueue` from the document upload/re-index flow.

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

Member 2 additionally owns Flow 3 in a separate focused reporting branch. The initial reporting implementation should aggregate existing persisted data and should not require a new `Application/` interface merely to count rows.

See `docs/project-status.md`, `docs/team-workflow.md`, `docs/member-2-document-management-handoff.md`, and `docs/flow-3-report-statistics-handoff.md` for the current project boundaries.

## Dependency boundaries

- Presentation code (MVC/Razor Pages) may depend on `Application/Abstractions` and `Application/Models`.
- Application abstractions must not depend on Razor Pages, MVC controllers, `HttpContext`, Ollama-specific DTOs, or PostgreSQL-specific query types.
- Infrastructure implementations may depend on these abstractions.
- Do not duplicate a shared contract inside a feature folder when an abstraction already exists here.
- Do not move parser, chunker, pgvector, or Ollama implementation logic into Member 2 request handlers.
- Flow 3 reporting code must not call Ollama or pgvector similarity retrieval.
- Flow 3 must not change a shared contract solely to make a dashboard/query page easier to implement.

## Current shared contracts

- `IDocumentIndexingQueue`: handoff from the merged document upload/re-index actions to the Member 3 background indexing worker.
- `IDocumentIndexingService`: one-document indexing pipeline executed by the worker.
- `ITextEmbeddingService`: provider-neutral text embedding boundary shared by indexing and retrieval. It supports both single-text embedding for retrieval and ordered batch embedding for indexing.
- `IChatCompletionService`: provider-neutral chat-generation boundary used by the RAG workflow.
- `IRagQueryService`: presentation-facing boundary for asking a grounded question in an existing chat session.
- `RagAnswer` / `RagCitation`: presentation-safe RAG result models used by the chat UI.

There is intentionally **no reporting-specific shared contract yet**. Initial Flow 3 aggregates can be implemented with focused read-only EF Core queries over the existing model. Introduce a reporting abstraction only if a concrete reuse/testability requirement justifies it.

## Ownership expectations

### Flow 1 request/presentation - Member 2 - MERGED

The merged flow persists the source file and `Document` record first, validates an optional PRN222 chapter assignment, then enqueues only the persisted `Document.Id` through `IDocumentIndexingQueue`.

Document/Chapter Management is now an existing consumer of this application layer. Later members should preserve that handoff rather than creating a second upload/indexing entry point.

Member 2 request handlers must not parse or embed documents.

### Flow 1 indexing - Member 3 - PENDING

Implement the background side behind the existing contracts:

- queue/worker integration
- `IDocumentIndexingService`
- parsing
- chunk replacement
- embeddings through `ITextEmbeddingService`
- index-state transitions (`Uploaded` -> `Processing` -> `Indexed`/`Failed`)
- `IndexedAtUtc` and `IndexError` updates

Member 3 may replace the temporary `InMemoryDocumentIndexingQueue` and its DI registration, but should keep `IDocumentIndexingQueue` stable unless all consumers are changed together.

### Flow 2 RAG backend - Member 4 - PENDING

Use `ITextEmbeddingService` for question embedding and `IChatCompletionService` for generation.

Implement `IRagQueryService` so it:

- validates that the session belongs to the supplied authenticated user
- persists user/assistant messages
- retrieves successfully indexed PRN222 evidence
- persists source citations
- returns `RagAnswer`
- provides explicit no-evidence/out-of-scope behavior when grounding is insufficient

### Flow 2 presentation/conversation management - Member 5 - PENDING

Depend on `IRagQueryService`; do not call Ollama or pgvector directly from controllers, Razor Page models, or browser JavaScript.

Use `RagAnswer` and `RagCitation` rather than leaking persistence entities/provider DTOs into the UI.

Member 5 owns chat-session creation/opening/navigation and **Conversation History as part of Flow 2**, plus citation rendering and the evaluation deliverable.

### Flow 3 Report & Statistics - Member 2 - NEW / PENDING

The initial reporting workflow should be read-only and derive aggregate information from existing persisted data, including where useful:

- chapters
- documents and indexing status
- document/chapter grouping
- chat sessions
- chat messages
- message citations

Flow 3 must not:

- enqueue or process indexing work
- mutate index status
- perform similarity retrieval
- call Ollama
- mutate chat/session/message/citation data
- duplicate Member 5 conversation pages
- create speculative analytics entities or migrations
- force changes to the existing shared contracts

If reporting later exposes a genuine reusable application-layer need, coordinate it additively and preserve existing Flow 1/Flow 2 consumers.

## Contract changes

These interfaces are cross-member integration points. Prefer additive changes.

If a signature must change:

1. explain why the existing contract cannot represent the requirement;
2. coordinate with the current producer/consumer owners;
3. update all affected consumers and implementations together;
4. update `docs/project-status.md`, `docs/team-workflow.md`, and the relevant handoff document when the change affects member boundaries.

Do not change a shared contract merely to make one implementation more convenient, especially for the initial Flow 3 reporting dashboard.
