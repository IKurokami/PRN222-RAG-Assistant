# Application-layer instructions

> Synchronized after PR #30 merged and issue #27 closed on 2026-08-18.

This subtree contains stable cross-workflow contracts/models. Keep it independent from MVC/Razor/provider-specific HTTP/PostgreSQL-specific presentation details.

## Current workflow state

1. Flow 1 - Document Management & Indexing - complete - MVC.
2. Flow 2 RAG backend - complete / merged through PR #30 - Member 4.
3. Flow 2 MVC product presentation/evaluation - pending - Member 5.
4. Flow 3 - Report & Statistics - complete - Razor Pages.
5. Cross-app UI/UX redesign - complete - Member 3.
6. Provider-neutral AI runtime foundation/routing - complete - Member 1.

## Documentation identity rule

Project documentation uses **Member numbers only**. Do not add GitHub usernames to README, AGENTS files or `docs/*`.

Contribution credit is separate from ownership. See `docs/member-contributions.md`.

## Provider-neutral boundary

Application contracts are intentionally provider-agnostic:

```text
ITextEmbeddingService
IChatCompletionService
```

Infrastructure selects the concrete implementations from Ollama, OpenAI, Gemini or OpenRouter. Chat and embedding providers may be configured independently. OpenRouter may perform ordered chat-model fallback inside Infrastructure.

Do not:

- add provider-specific DTOs to Application;
- expose API keys through Application contracts;
- branch on provider names inside workflow services;
- implement provider/model fallback in Application;
- add embedding-model rotation;
- assume embeddings from two models/providers are interchangeable.

If embedding provider/model/dimension changes, treat existing vectors as stale and re-index the corpus. Chat-provider/model changes alone do not require re-indexing.

## Subject boundary

The application is multi-subject. PRN222 is only the seeded demo subject.

Persisted subject context now includes:

```text
Chapter.SubjectId
Document.SubjectId
ChatSession.SubjectId
```

Flow 1 and Flow 3 carry a concrete subject context. Flow 2 backend now carries subject context through session/retrieval/persistence.

Do not add a product contract that silently drops subject context or intentionally falls back to global-corpus retrieval.

## Current integration boundaries

### Flow 1

```text
subject-aware MVC action
   -> persist Document/Chapter
   -> IDocumentIndexingQueue.EnqueueAsync(documentId)
   -> DocumentIndexingWorker
   -> IDocumentIndexingService.IndexAsync(documentId)
   -> ITextEmbeddingService
```

The request layer never needs to know which provider is selected.

### Flow 2 backend

```text
subject-aware session/query
   -> IRagQueryService
   -> ITextEmbeddingService
   -> pgvector retrieval restricted by subject context
   -> IChatCompletionService
   -> message/citation persistence
```

The merged backend validates session ownership, subject consistency, citation markers and failure-path persistence behavior.

Member 4 owns backend workflow behavior. Member 1 owns provider selection/adapters and shared schema/contract coordination. Member 5 owns final MVC presentation/evaluation.

The internal RAG demo Razor Page is a development surface, not the final Member 5 MVC product UI.

### Flow 3

No reporting-specific shared Application contract is required. Flow 3 remains provider-independent and never calls AI providers.

Because `ChatSession.SubjectId` now exists, report-side chat aggregates should be audited when Member 5 completes Flow 2 so subject scoping is explicit.

## Shared contracts

- `IDocumentIndexingQueue`: request-to-background handoff.
- `IDocumentIndexingService`: one-document indexing pipeline.
- `ITextEmbeddingService`: provider-neutral embedding.
- `IChatCompletionService`: provider-neutral generation.
- `IRagQueryService`: presentation-facing grounded Q&A/session boundary.
- `RagAnswer` / `RagCitation`: presentation-safe RAG result models.

Prefer additive changes. Keep concrete provider payloads under Infrastructure.

## Ownership

- Member 1: shared contracts, Core/Data/Identity/RBAC/multi-subject, provider configuration/adapters/routing, schema coordination, docs.
- Member 2: Flow 1 request/business behavior + Flow 3 reporting behavior.
- Member 3: indexing maintenance + completed cross-app UI/UX redesign.
- Member 4: completed Flow 2 backend baseline + merged issue #27 remediation contribution.
- Member 5: pending Flow 2 MVC/history/citations/evaluation.

Contribution accounting may differ from ownership; use `docs/member-contributions.md` for actual merged credit.

## Dependency rules

- Application abstractions do not depend on MVC, Razor PageModel, HttpContext, provider-specific SDK/DTOs, Npgsql query types, CSS or JS.
- Infrastructure implements provider adapters/routing and pgvector retrieval.
- Flow 1 controllers do not parse/chunk/embed/call providers.
- Flow 2 MVC must call `IRagQueryService`, not providers or pgvector directly.
- Flow 3 does not call provider/retrieval code.
- Do not create duplicate provider contracts inside feature folders.
