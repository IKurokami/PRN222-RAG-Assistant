# Member 1 handoff - Core/Data/RBAC/Multi-subject/AI Providers/Documentation

## Ownership

Member 1 owns:

- Domain/Data/Security baseline;
- Identity/RBAC and shared schema/contracts;
- subject management/authorization integration;
- AI provider selection/configuration;
- Ollama/Gemini/OpenAI/OpenRouter adapters;
- OpenRouter free chat fallback routing;
- API-key/environment wiring and startup validation;
- embedding dimension/vector-space/re-index coordination;
- provider regression tests;
- all repository documentation.

## Provider assignment

Supported providers:

```text
Ollama -> local/default
Gemini -> direct online Free Tier path
OpenRouter -> free-first online router/fallback
OpenAI -> optional paid API
```

`RAG_PROVIDER` remains backward compatible. Member 1 added optional `RAG_CHAT_PROVIDER` and `RAG_EMBEDDING_PROVIDER` so free chat can fail over independently without changing the corpus embedding model.

## OpenRouter rule

Chat may use an ordered model list. Embeddings may not.

```text
Chat: Gemma free -> Nemotron free -> openrouter/free
Embedding: one fixed configured model only
```

Changing only chat model/provider does not require document re-indexing. Changing embedding provider/model/dimensions does.

## Secrets

```text
GEMINI_API_KEY
OPENAI_API_KEY
OPENROUTER_API_KEY
```

All keys are server-side environment secrets. No real key belongs in tracked files.

## Cross-workflow boundary

Member 3 retains parser/chunker/indexing worker behavior and consumes `ITextEmbeddingService`.

Member 4 owns future subject-scoped RAG retrieval/grounding/persistence and consumes both provider-neutral interfaces.

Member 5 owns future Flow 2 MVC/evaluation presentation.

Provider routing is cross-cutting Infrastructure work and does not transfer those responsibilities.

## Schema impact

No EF model/migration is required for provider routing. A full corpus re-index is an operational requirement whenever the embedding vector space changes.
