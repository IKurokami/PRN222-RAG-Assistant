# AI provider routing, fallback, and embedding compatibility

> Runtime/configuration guide synchronized with repository configuration after PR #39 on 2026-08-21. External provider pricing/free limits can change independently of this repository.

## Supported providers

```text
Ollama | Gemini | OpenAI | OpenRouter
```

Application workflows consume provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

`RAG_PROVIDER` is the backward-compatible base provider. Chat and embeddings may override it independently:

```env
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
RAG_EMBEDDING_DIMENSIONS=1024
```

Blank overrides inherit `RAG_PROVIDER`.

## Why chat and embedding selection are separate

Chat model changes do not alter persisted vector data, so changing a chat provider/model/fallback order does not require re-indexing.

Embeddings define the semantic vector space stored in pgvector. Changing embedding provider/model/dimension requires a complete corpus re-index.

## Repository defaults/examples

Local Ollama defaults:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Gemini configuration defaults:

```text
Chat:      gemini-3.6-flash
Embedding: gemini-embedding-2
```

OpenAI configuration defaults:

```text
Chat:      gpt-5.6-luna
Embedding: text-embedding-3-small
```

OpenRouter embedding default:

```text
nvidia/llama-nemotron-embed-vl-1b-v2:free
```

These are repository configuration values, not guarantees that a third-party model remains available/free forever.

## OpenRouter chat fallback chain

When OpenRouter is explicitly selected for Chat, the current default ordered chain in `.env.example` / Docker Compose is:

```text
nvidia/nemotron-3.5-lightning:free
nvidia/nemotron-3-ultra-550b-a55b:free
nvidia/nemotron-3-super-120b-a12b:free
google/gemma-4-26b-a4b-it:free
openai/gpt-oss-20b:free
openrouter/free
```

The adapter sends an ordered model list and allows OpenRouter provider fallback. This can improve resilience to a model/provider outage, but it does not bypass account-level rate limits/quotas.

## Current Render split

Render does **not** currently use the OpenRouter chat fallback chain because PR #39 overrides Chat to Gemini:

```text
Rag__Provider=OpenRouter
Rag__ChatProvider=Gemini
Rag__EmbeddingProvider=OpenRouter

Chat:      gemini-3.6-flash
Embedding: nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimensions: 1024
```

Render therefore needs both a Gemini API key and an OpenRouter API key.

## Embedding dimensionality and PR #37

Default project dimension remains 1024, but the data column/runtime is not conceptually limited to one hard-coded dimension forever.

PR #37:

- sends configured `outputDimensionality` correctly for Gemini batch embedding;
- keeps provider response-dimension validation;
- keeps `DocumentChunk.Embedding` usable for different vector dimensions during a migration;
- filters retrieval using `vector_dims(Embedding) = questionEmbedding.Length` before cosine distance.

### What this means during re-index

If switching from one dimension to another, documents may be re-indexed gradually. Old vectors with the previous dimension can temporarily remain in the table without crashing current retrieval; they are excluded until re-indexed.

### What this does not mean

Two models can emit vectors with the same length while using different semantic coordinate systems. `vector_dims` cannot detect that incompatibility.

Therefore the operational rule remains:

```text
change embedding provider/model/dimension
 -> plan a full corpus re-index
 -> do not intentionally treat old vectors as semantically compatible
 -> complete re-index before considering the migration finished
```

## Recommended runtime patterns

### Fully local

```env
RAG_PROVIDER=Ollama
```

Run:

```bash
docker compose --profile local-ai up -d --build
```

### Direct Gemini

```env
RAG_PROVIDER=Gemini
GEMINI_API_KEY=<server-side key>
```

### OpenRouter chat + fixed embedding provider

```env
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
OPENROUTER_API_KEY=<server-side key>
GEMINI_API_KEY=<server-side key>
```

### Current Render pattern

```text
ChatProvider=Gemini
EmbeddingProvider=OpenRouter
```

This avoids coupling Chat availability to the persisted embedding corpus.

## Startup validation

- unsupported provider values fail fast;
- only providers selected for chat/embedding require configuration;
- selected cloud providers require API keys;
- selected base URLs must be valid absolute URLs;
- legacy `RAG_PROVIDER` remains supported.

## Secrets and privacy

API keys remain server-side. Do not expose keys in browser JavaScript, logs, tracked appsettings, docs, screenshots, or committed `.env` files.

Selecting a cloud provider sends the relevant embedding text and/or chat context to that external service. Provider choice is therefore a privacy/data-egress decision in addition to a cost/availability decision.

## External-information note

Free-tier availability, quotas, model names, pricing and provider data policies can change without repository changes. Before a production deployment, verify those items against the provider's current official documentation rather than treating an old repository snapshot as a service guarantee.
