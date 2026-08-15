# AI provider backup - Ollama, Gemini, OpenAI

> Added by Member 1 on 2026-08-15. This document is the canonical runtime/provider configuration guide.

## Goal

The application can run with one explicitly selected AI provider:

```text
RAG_PROVIDER=Ollama
RAG_PROVIDER=Gemini
RAG_PROVIDER=OpenAI
```

The provider supplies both existing provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

There is deliberately **no silent automatic failover** between local and cloud providers. Sending academic documents or prompts to an external API must be an explicit operator decision because it changes data egress, availability, and potentially cost.

## Cost decision

Research snapshot: **2026-08-15**.

| Provider | Chat model | Embedding model | Cost position |
|---|---|---|---|
| Ollama | `qwen3:4b` | `qwen3-embedding:0.6b` | **Local / $0 provider fee**; uses your own hardware |
| Google Gemini | `gemini-3.6-flash` | `gemini-embedding-2` | **Recommended online FREE TIER backup**; rate limits apply |
| OpenAI | `gpt-5.6-luna` | `text-embedding-3-small` | **Optional PAID API**, not the free fallback |

Google's official pricing currently lists Standard Free Tier input/output for `gemini-3.6-flash` and free-of-charge Standard Free Tier inputs for `gemini-embedding-2`. Google's Free Tier is rate-limited and its pricing page states Free Tier content may be used to improve Google products.

OpenAI API usage is not treated as free for this project. The current GPT-5.6 Luna API rate table has no general Free tier; `text-embedding-3-small` is also usage-priced. OpenAI remains available only as an optional paid provider for future deployment flexibility.

Official references used for this decision:

- Google Gemini API pricing: <https://ai.google.dev/gemini-api/docs/pricing>
- Google Gemini API rate limits: <https://ai.google.dev/gemini-api/docs/rate-limits>
- Google Gemini Embeddings API: <https://ai.google.dev/api/embeddings>
- OpenAI GPT-5.6 Luna: <https://developers.openai.com/api/docs/models/gpt-5.6-luna>
- OpenAI `text-embedding-3-small`: <https://developers.openai.com/api/docs/models/text-embedding-3-small>
- OpenAI API pricing: <https://openai.com/api/pricing/>
- Ollama pricing: <https://ollama.com/pricing>
- Ollama `qwen3-embedding`: <https://ollama.com/library/qwen3-embedding>

Cloud pricing/models can change. Re-check these official pages before production deployment.

## Embedding compatibility invariant

The default corpus dimension is:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

This matches the default Ollama `qwen3-embedding:0.6b` output. OpenAI `text-embedding-3-small` accepts a requested output dimension, and Gemini Embedding 2 supports configurable output dimensions, so both online adapters request the same configured dimension.

**Dimension equality alone does not make embeddings interchangeable.** Different embedding models use different vector spaces.

Whenever any of the following changes:

```text
RAG_PROVIDER
*_EMBEDDING_MODEL
RAG_EMBEDDING_DIMENSIONS
```

treat all existing `DocumentChunk.Embedding` values as stale and re-index the entire document corpus before using similarity retrieval.

Do not partially re-index a corpus with one embedding model and leave older chunks from another model.

## Environment variables

Copy `.env.example` to `.env` for Docker Compose. Real API keys belong only in the untracked `.env`/deployment secret environment.

Shared:

```text
RAG_PROVIDER=Ollama
RAG_EMBEDDING_DIMENSIONS=1024
```

### Gemini - online Free Tier backup

```text
RAG_PROVIDER=Gemini
GEMINI_API_KEY=<server-side key>
GEMINI_BASE_URL=https://generativelanguage.googleapis.com/
GEMINI_CHAT_MODEL=gemini-3.6-flash
GEMINI_EMBEDDING_MODEL=gemini-embedding-2
```

### Ollama - local/default

```text
RAG_PROVIDER=Ollama
OLLAMA_BASE_URL=http://localhost:11434
OLLAMA_CHAT_MODEL=qwen3:4b
OLLAMA_EMBEDDING_MODEL=qwen3-embedding:0.6b
```

### OpenAI - optional paid

```text
RAG_PROVIDER=OpenAI
OPENAI_API_KEY=<server-side key>
OPENAI_BASE_URL=https://api.openai.com/v1/
OPENAI_CHAT_MODEL=gpt-5.6-luna
OPENAI_EMBEDDING_MODEL=text-embedding-3-small
```

Docker Compose maps these flat `.env` values to ASP.NET Core `Rag__...` configuration.

For direct `dotnet run`, the infrastructure also recognizes standard flat environment variables for the provider choice/API keys/dimension (`RAG_PROVIDER`, `GEMINI_API_KEY`, `OPENAI_API_KEY`, `RAG_EMBEDDING_DIMENSIONS`) in addition to normal ASP.NET Core `Rag__...` environment names. The application does not automatically parse a `.env` file outside Docker Compose.

## Docker usage

### Local Ollama

Ollama is under the `local-ai` Compose profile so cloud-only runs do not consume local model resources.

```bash
# .env
RAG_PROVIDER=Ollama

docker compose --profile local-ai up -d --build
```

Pull the configured Ollama models if they are not already present.

### Gemini Free Tier

```bash
# .env
RAG_PROVIDER=Gemini
GEMINI_API_KEY=<server-side key>

docker compose up -d --build
```

The Ollama container is not required.

### OpenAI paid

```bash
# .env
RAG_PROVIDER=OpenAI
OPENAI_API_KEY=<server-side key>

docker compose up -d --build
```

## Startup validation

`ServiceCollectionExtensions` validates only the selected provider:

- unsupported providers fail fast;
- the selected provider must have an absolute base URL;
- Gemini/OpenAI require their API key;
- API keys for unselected providers are not required.

Concrete adapters are registered behind the same Application interfaces, so Flow 1 indexing and future Flow 2 RAG code do not need provider-specific branches.

## Provider HTTP behavior

Gemini:

```text
POST /v1beta/models/{model}:batchEmbedContents
POST /v1beta/models/{model}:generateContent
x-goog-api-key: <GEMINI_API_KEY>
```

Ollama:

```text
POST /api/embed
POST /api/chat
```

OpenAI:

```text
POST /v1/embeddings
POST /v1/chat/completions
Authorization: Bearer <OPENAI_API_KEY>
```

No API key value is logged or committed.

## Ownership

This backup/provider foundation is assigned to **Member 1**.

Member 1 owns:

- provider selection/configuration;
- online API-key/env wiring;
- concrete provider adapters and provider-registration tests;
- embedding-dimension invariant and full re-index coordination;
- provider documentation.

Existing ownership remains:

- Member 3 owns indexing/chunking/worker behavior consuming `ITextEmbeddingService`;
- Member 4 owns future subject-scoped Flow 2 retrieval/grounding/persistence behavior consuming the provider-neutral contracts;
- Member 5 owns future Flow 2 MVC/evaluation presentation.

## Cloud-data boundary

`Ollama` keeps inference local to the configured Ollama runtime.

`Gemini` and `OpenAI` are external APIs. When a cloud provider is selected, text submitted for embeddings and later chat prompts/context leaves the application runtime and is processed by that provider. Operators must choose cloud mode intentionally and follow the provider/account data-handling requirements that apply to their deployment.
