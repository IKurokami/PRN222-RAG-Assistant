# Application-layer instructions

> Provider-backup update based on `master` after merged PR #20.

This subtree contains stable cross-workflow contracts/models. Keep it independent from MVC/Razor/provider-specific HTTP/PostgreSQL-specific presentation details.

## Current workflow state

1. Flow 1 - Document Management & Indexing - complete - MVC.
2. Flow 2 - RAG Q&A + Conversation Management - pending - MVC.
3. Flow 3 - Report & Statistics - complete - Razor Pages.
4. Cross-app UI/UX redesign - complete in PR #19 - Member 3.
5. Provider-neutral AI runtime foundation - implemented by Member 1.

## Provider-neutral boundary

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

## Subject boundary

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

### Flow 3

No reporting-specific shared contract is required. Flow 3 remains provider-independent and never calls AI providers.

## Shared contracts

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
