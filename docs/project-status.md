# Project status

> Member 1 owns synchronization of this file.

## Workflows

| Workflow | Presentation | Status | Owner |
|---|---|---|---|
| Flow 1 - Document Management & Indexing | MVC | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC/provider integration |
| Flow 2 - RAG Q&A + Conversation Management | MVC | Pending | Member 4 backend + Member 5 UI/evaluation |
| Flow 3 - Report & Statistics | Razor Pages | Complete | Member 2 behavior; Member 1 subject/RBAC integration |

## Platform/provider state

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Multi-subject management + authorization | Complete | Member 1 |
| Ollama local provider | Complete | Member 1 provider foundation |
| Gemini direct online Free Tier provider | Complete | Member 1 |
| OpenAI optional paid provider | Complete | Member 1 |
| OpenRouter provider | Implemented in current fallback PR | **Member 1** |
| Independent chat/embedding provider selection | Implemented in current fallback PR | **Member 1** |
| Ordered free chat-model fallback | Implemented in current fallback PR | **Member 1** |
| Embedding single-model/re-index guardrail | Implemented/documented | **Member 1** |
| Cross-app UI/UX redesign | Complete / PR #19 | Member 3 |

## Free-first decision

Recommended hybrid for development/demo:

```env
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
OPENROUTER_API_KEY=...
GEMINI_API_KEY=...
```

Default OpenRouter chat order:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

OpenRouter automatically falls back between these chat choices when a model errors. Free model availability/rate limits remain external constraints.

OpenAI is retained only as optional paid infrastructure.

## Provider selection

```text
RAG_PROVIDER            # backward-compatible default for both contracts
RAG_CHAT_PROVIDER       # optional override
RAG_EMBEDDING_PROVIDER  # optional override
```

Supported values: `Ollama`, `Gemini`, `OpenAI`, `OpenRouter`.

## Embedding compatibility

Default dimension: `1024`.

Do not rotate embedding models. If embedding provider/model/dimension changes, re-index every document before retrieval. Chat-provider/model fallback does not require re-indexing.

## Flow 2 remaining requirement

```text
selected subject
 -> ITextEmbeddingService
 -> same-subject pgvector retrieval
 -> IChatCompletionService
 -> same-subject citations/history
```

The provider layer is prepared so Member 4 can focus on RAG behavior without concrete provider coupling.

## Next priority

The major unfinished product workflow remains **Flow 2**.
