# Team workflow and ownership

> Member 1 is the sole documentation owner.

## Current milestone

- Member 1 Core/Data/Identity/RBAC: complete.
- Member 1 multi-subject management/subject scoping: complete.
- **Member 1 AI provider routing/fallback: implemented in the current PR.**
- Member 2 Flow 1 request/business behavior: complete.
- Member 3 Flow 1 indexing: complete.
- Member 2 Flow 3 reporting behavior: complete.
- Member 3 cross-app UI/UX redesign: complete / PR #19.
- Member 4 Flow 2 backend: pending.
- Member 5 Flow 2 MVC/history/citations/evaluation: pending.

## Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Owns shared infrastructure, Identity/RBAC, subject management/authorization, provider selection/configuration, Ollama/Gemini/OpenAI/OpenRouter adapters, OpenRouter free chat fallback, API-key/env wiring, embedding compatibility/re-index coordination, provider tests, and all README/AGENTS/docs edits.

Provider plumbing does not transfer indexing ownership from Member 3 or RAG business behavior from Member 4.

## Member 2

Retains established Chapter/Document request semantics and read-only reporting behavior. Controllers/Pages do not call concrete providers.

## Member 3

Owns parser/chunker/indexing service/worker/startup recovery and completed UI/UX redesign. Indexing consumes `ITextEmbeddingService`; no provider-specific worker exists.

## Member 4

Pending Flow 2 backend responsibilities: subject-scoped question embedding, pgvector retrieval, grounding, completion through `IChatCompletionService`, session validation, messages/citations persistence.

Do not call Ollama/Gemini/OpenAI/OpenRouter directly.

## Member 5

Owns future Chat MVC/session/history/citation UI and evaluation tooling. Do not call provider APIs in controllers/views.

## Provider integration map

```text
RAG_PROVIDER [legacy/default]
  |
  +--> RAG_CHAT_PROVIDER override ------> IChatCompletionService
  |       +--> OpenRouter ordered free-model fallback
  |
  \--> RAG_EMBEDDING_PROVIDER override -> ITextEmbeddingService
          \--> exactly one embedding model per corpus
```

## Provider change procedure

If only chat provider/model/order changes, no document re-index is required.

If embedding provider/model/dimension changes:

1. Member 1 records the configuration change;
2. treat existing vectors as stale;
3. re-index the entire searchable corpus;
4. do not mix old/new vectors;
5. validate retrieval before considering the switch complete.

## Secrets

Real API keys live only in local/deployment secret environments. Never put them in PR text, screenshots, tracked appsettings, browser JS, logs, or docs.
