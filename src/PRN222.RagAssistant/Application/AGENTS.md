# Application-layer instructions

This subtree contains stable cross-workflow contracts/models. Keep it independent from MVC/Razor/provider-specific HTTP/PostgreSQL-specific presentation details.

## Current workflow state

1. Flow 1 - Document Management & Indexing - complete - MVC.
2. Flow 2 - RAG Q&A + Conversation Management - pending - MVC.
3. Flow 3 - Report & Statistics - complete - Razor Pages.
4. Cross-app UI/UX redesign - complete - Member 3.
5. Provider-neutral AI runtime/routing foundation - Member 1.

## Provider-neutral boundary

Application contracts are intentionally provider-agnostic:

```text
ITextEmbeddingService
IChatCompletionService
```

Infrastructure selects implementations from `Ollama`, `OpenAI`, `Gemini`, or `OpenRouter`. Chat and embedding providers may differ. OpenRouter may perform an ordered chat-model fallback inside Infrastructure.

Do not:

- add provider-specific DTOs to Application;
- expose API keys through Application contracts;
- branch on provider names inside workflow services;
- implement provider/model fallback in Application;
- assume embeddings from two models are interchangeable;
- add embedding-model rotation.

If the configured embedding provider/model/dimension changes, Infrastructure/operations must treat existing vectors as stale and re-index the full corpus. Chat-model fallback alone does not require re-indexing.

## Subject boundary

The application is multi-subject. PRN222 is only the seeded demo subject.

`Document` and `Chapter` persist `SubjectId`. Flow 1 and Flow 3 carry a concrete subject context and authorize via `ISubjectAccessService`.

Flow 2 must not omit subject context from retrieval, persistence, history, or citations.

## Integration boundaries

### Flow 1

```text
subject-aware MVC action
 -> Document/Chapter persistence
 -> IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> ITextEmbeddingService [selected embedding provider]
```

### Flow 2

```text
MVC subject/session context
 -> subject-scoped RAG boundary
 -> ITextEmbeddingService
 -> same-subject pgvector retrieval
 -> IChatCompletionService [selected chat provider; provider-internal fallback allowed]
 -> same-subject messages/citations
```

Member 4 owns backend workflow behavior. Member 1 owns provider selection/adapters/schema coordination. Member 5 owns MVC presentation/evaluation.

### Flow 3

Provider-independent and never calls AI providers.

## Shared contracts

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer` / `RagCitation`

Provider-specific payloads stay under Infrastructure.
