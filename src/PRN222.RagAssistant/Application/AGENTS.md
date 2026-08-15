# Application-layer instructions

<<<<<<< Updated upstream
> Provider-backup update based on `master` after merged PR #20.

This subtree contains stable cross-workflow contracts/models. Keep it independent from MVC/Razor/provider-specific HTTP/PostgreSQL-specific presentation details.

## Current workflow state

1. Flow 1 - Document Management & Indexing - complete - MVC.
2. Flow 2 - RAG Q&A + Conversation Management - pending - MVC.
3. Flow 3 - Report & Statistics - complete - Razor Pages.
4. Cross-app UI/UX redesign - complete in PR #19 - Member 3.
5. Provider-neutral AI runtime foundation - implemented by Member 1.
=======
This subtree contains stable contracts shared by the document-management, indexing, RAG, and chat presentation workflows.

The project currently defines three product workflows:

1. **Flow 1 - Document Management & Indexing**
2. **Flow 2 - RAG Question & Answer & Conversation Management**
3. **Flow 3 - Report & Statistics**

Conversation History is part of Flow 2. Flow 3 is a separate read-only reporting workflow and must not be implemented by reshaping the shared contracts unnecessarily.
>>>>>>> Stashed changes

## Provider-neutral boundary

<<<<<<< Updated upstream
Application contracts are intentionally provider-agnostic:

```text
ITextEmbeddingService
IChatCompletionService
```

Infrastructure selects exactly one implementation from `Ollama`, `OpenAI`, or `Gemini`.

Do not:

- add Ollama/OpenAI/Gemini DTOs to Application;
- expose API keys through Application contracts;
- branch on provider names inside workflow services;
- add silent cloud failover behavior to Application;
- assume embeddings from two models are interchangeable.

If the configured embedding provider/model/dimension changes, Infrastructure/operations must treat existing vectors as stale and re-index the corpus.
=======
Member 1 established these shared contracts. Member 2 is merged and actively consumes `IDocumentIndexingQueue` from the document upload/re-index flow. Member 3 is complete. Member 4 is in progress.

`ITextEmbeddingService` is shared between Member 3 (indexing) and Member 4 (retrieval). `IChatCompletionService` is used by Member 4. `IRagQueryService` is ready for Member 5.

The intended handoff is established:

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
>>>>>>> Stashed changes

## Subject boundary

<<<<<<< Updated upstream
The application is multi-subject. PRN222 is only the seeded demo subject.

`Document` and `Chapter` persist `SubjectId`. Flow 1 and Flow 3 carry a concrete subject context and authorize via `ISubjectAccessService`.

Do not add a contract that allows retrieval or persistence to silently omit subject context once Flow 2 implementation begins.

Current Flow 2 persistence limitation: `ChatSession` has no `SubjectId`. Coordinate any needed persistence change with Member 1.

## Current integration boundaries

### Flow 1

```text
subject-aware MVC action
   -> persist Document/Chapter
   -> IDocumentIndexingQueue.EnqueueAsync(documentId)
   -> DocumentIndexingWorker
   -> IDocumentIndexingService.IndexAsync(documentId)
   -> ITextEmbeddingService (selected provider)
```

The request layer never needs to know which provider is selected.

### Flow 2

Required direction:

```text
MVC subject/session context
   -> subject-scoped RAG application boundary
   -> ITextEmbeddingService
   -> pgvector retrieval restricted to selected Subject
   -> IChatCompletionService
   -> message/citation persistence bound to same Subject/session
```

Member 4 owns backend workflow behavior. Member 1 owns provider selection/adapters and shared schema/contract coordination. Member 5 owns MVC presentation/evaluation.
=======
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

### Flow 1 indexing - Member 3 - COMPLETE

Implement the background side behind the existing contracts:

- queue/worker integration
- `IDocumentIndexingService`
- parsing
- chunk replacement
- embeddings through `ITextEmbeddingService`
- index-state transitions (`Uploaded` -> `Processing` -> `Indexed`/`Failed`)
- `IndexedAtUtc` and `IndexError` updates

Member 3 replaced the temporary `InMemoryDocumentIndexingQueue` with the real `DocumentIndexingWorker` and implemented the full indexing pipeline.

### Flow 2 RAG backend - Member 4 - IN PROGRESS

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
>>>>>>> Stashed changes

### Flow 3

No reporting-specific shared contract is required. Flow 3 remains provider-independent and never calls AI providers.

## Shared contracts

<<<<<<< Updated upstream
- `IDocumentIndexingQueue`: request-to-background handoff.
- `IDocumentIndexingService`: one-document indexing pipeline.
- `ITextEmbeddingService`: provider-neutral single/batch embedding.
- `IChatCompletionService`: provider-neutral generation boundary.
- `IRagQueryService`: presentation-facing grounded Q&A boundary.
- `RagAnswer` / `RagCitation`: presentation-safe RAG result models.

Prefer additive changes. Keep concrete provider payloads under Infrastructure.

## Ownership

- Member 1: shared contracts, Core/Data/Identity/RBAC/multi-subject, provider configuration/adapters, schema coordination, all docs.
- Member 2: Flow 1 request/business behavior + Flow 3 reporting behavior.
- Member 3: indexing implementation + completed cross-app UI/UX redesign.
- Member 4: pending Flow 2 backend.
- Member 5: pending Flow 2 MVC/history/citations/evaluation.

## Dependency rules

- Application abstractions do not depend on MVC, Razor PageModel, HttpContext, provider-specific SDK/DTOs, Npgsql query types, CSS, or JS.
- Infrastructure implements provider adapters.
- Flow 1 controllers do not parse/chunk/embed/call providers.
- Flow 2 MVC does not call providers or pgvector directly.
- Flow 3 does not call provider/retrieval code.
- Do not create duplicate provider contracts inside feature folders.
=======
1. explain why the existing contract cannot represent the requirement;
2. coordinate with the current producer/consumer owners;
3. update all affected consumers and implementations together;
4. update `docs/project-status.md`, `docs/team-workflow.md`, and the relevant handoff document when the change affects member boundaries.

Do not change a shared contract merely to make one implementation more convenient, especially for the initial Flow 3 reporting dashboard.
>>>>>>> Stashed changes
