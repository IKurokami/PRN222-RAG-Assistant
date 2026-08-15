# AI provider routing, free fallback, and backup strategy

> Canonical provider/runtime guide. Owned by Member 1. Research snapshot: **2026-08-15**.

## Goal

The project supports local, direct-cloud, and routed-cloud AI without provider-specific branches in Flow 1/Flow 2.

Supported providers:

```text
Ollama | Gemini | OpenAI | OpenRouter
```

`RAG_PROVIDER` is backward compatible. Two optional overrides allow chat and embeddings to be configured independently:

```env
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
```

Blank overrides inherit `RAG_PROVIDER`.

## Why the split was added

Chat free-model availability can be volatile. It is safe for a chat request to fall back to another LLM because generated text is not part of the persistent vector space.

Embedding models are not interchangeable. Changing an embedding model changes the semantic coordinate system stored in pgvector. Therefore:

- **chat may rotate/fallback between models**;
- **embedding uses one fixed configured model**;
- changing embedding provider/model/dimension requires a complete corpus re-index.

## Cost/free matrix

| Provider | Chat | Embedding | Cost position |
|---|---|---|---|
| Ollama | `qwen3:4b` | `qwen3-embedding:0.6b` | local / $0 provider fee; own hardware |
| Gemini | `gemini-3.6-flash` | `gemini-embedding-2` | direct online Free Tier available; limits/data terms apply |
| OpenRouter | ordered free chain | `nvidia/llama-nemotron-embed-vl-1b-v2:free` | free-first routing; low limits/availability can vary |
| OpenAI | `gpt-5.6-luna` | `text-embedding-3-small` | optional paid API |

OpenRouter documents `:free` variants and the `openrouter/free` router as zero-cost inference options. Its Free plan/free-model API limits are low and are not a production SLA. As of this snapshot, the documented baseline is 50 free-model API requests/day unless the account has purchased at least 10 credits, in which case the free-model limit is 1000 requests/day.

The default OpenRouter free embedding model is explicitly a trial/free endpoint whose provider page says prompts/output are logged to improve the provider's model/products and warns against personal/confidential/sensitive data. Treat this as a development/demo option, not a privacy-equivalent replacement for local Ollama.

## OpenRouter chat fallback

Default:

```env
OPENROUTER_CHAT_MODELS=google/gemma-4-26b-a4b-it:free,nvidia/nemotron-3-ultra-550b-a55b:free,openrouter/free
```

The adapter sends this ordered array as OpenRouter's `models` parameter. OpenRouter automatically tries the next model when the current one returns an error such as rate limiting or downtime. Provider routing also keeps `allow_fallbacks=true`.

`openrouter/free` remains last because it randomly selects from the currently available free pool and free availability changes frequently.

Operators may replace the model list in `.env` without code changes.

## OpenRouter embeddings

Default:

```env
OPENROUTER_EMBEDDING_MODEL=nvidia/llama-nemotron-embed-vl-1b-v2:free
RAG_EMBEDDING_DIMENSIONS=1024
```

The adapter sends **one `model`**, never a `models` array. This is intentional.

`nvidia/nemotron-3-embed-1b:free` was researched but not selected as this project's default because NVIDIA's hosted API documentation specifies native 2048-dimensional output and rejects `dimensions=1024`. The current database/corpus contract is 1024 dimensions.

If the OpenRouter embedding model changes, re-index all searchable documents before retrieval.

## Recommended configurations

### 1. Fully local

```env
RAG_PROVIDER=Ollama
```

Run:

```bash
docker compose --profile local-ai up -d --build
```

### 2. Recommended free-first hybrid

Use OpenRouter for resilient free chat and Gemini for a stable direct embedding model:

```env
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
OPENROUTER_API_KEY=<server-side key>
GEMINI_API_KEY=<server-side key>
RAG_EMBEDDING_DIMENSIONS=1024
```

Run:

```bash
docker compose up -d --build
```

### 3. OpenRouter for chat and embedding

```env
RAG_PROVIDER=OpenRouter
OPENROUTER_API_KEY=<server-side key>
RAG_EMBEDDING_DIMENSIONS=1024
```

If documents were previously indexed with Ollama/Gemini/OpenAI, perform a complete re-index before using RAG retrieval.

### 4. Direct Gemini

```env
RAG_PROVIDER=Gemini
GEMINI_API_KEY=<server-side key>
```

### 5. Optional OpenAI paid

```env
RAG_PROVIDER=OpenAI
OPENAI_API_KEY=<server-side key>
```

## Environment variables

Shared:

```text
RAG_PROVIDER
RAG_CHAT_PROVIDER
RAG_EMBEDDING_PROVIDER
RAG_EMBEDDING_DIMENSIONS
```

OpenRouter:

```text
OPENROUTER_API_KEY
OPENROUTER_BASE_URL
OPENROUTER_CHAT_MODELS
OPENROUTER_EMBEDDING_MODEL
OPENROUTER_HTTP_REFERER
OPENROUTER_APP_TITLE
```

`HTTP-Referer` and `X-Title` attribution are optional. API keys remain server-side only.

## Startup validation

- unsupported chat/embedding provider values fail fast;
- only providers actually selected by chat or embedding are configured/validated;
- selected cloud providers require their API key;
- unselected provider keys are not required;
- selected base URLs must be absolute;
- legacy `RAG_PROVIDER` continues to work for both contracts.

## Embedding compatibility invariant

Default:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

Any change to embedding provider/model/dimension invalidates the previously stored semantic vector space:

```text
change embedding provider/model/dimension
 -> treat stored DocumentChunk.Embedding values as stale
 -> re-index the complete corpus
 -> then enable similarity retrieval
```

Do not partially mix vectors from two embedding models.

## Official references

- OpenRouter model fallback: <https://openrouter.ai/docs/guides/routing/model-fallbacks>
- OpenRouter Free Models Router: <https://openrouter.ai/docs/guides/routing/routers/free-router>
- OpenRouter FAQ/rate limits: <https://openrouter.ai/docs/faq>
- OpenRouter embeddings API: <https://openrouter.ai/docs/api/reference/embeddings>
- OpenRouter free embedding model: <https://openrouter.ai/nvidia/llama-nemotron-embed-vl-1b-v2:free>
- NVIDIA embedding dimensions reference: <https://docs.nvidia.com/nim/nemo-retriever/text-embedding/latest/reference.html>
- Google Gemini pricing: <https://ai.google.dev/gemini-api/docs/pricing>
- Google Gemini rate limits: <https://ai.google.dev/gemini-api/docs/rate-limits>
- OpenAI API pricing: <https://openai.com/api/pricing/>
- Ollama pricing: <https://ollama.com/pricing>

Cloud models, free availability, rate limits, and data policies can change. Re-check official pages before production use.

## Ownership

This provider routing/fallback foundation is **Member 1** work. Member 1 owns provider configuration/adapters/tests/docs and re-index coordination.

Member 3 still owns indexing/chunking/worker behavior. Member 4 owns future Flow 2 retrieval/grounding/persistence. Member 5 owns future Flow 2 MVC/evaluation.
