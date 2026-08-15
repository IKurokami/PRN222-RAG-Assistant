# AI provider backup - Ollama, OpenAI, Gemini

> Added by Member 1 on 2026-08-15. This document is the canonical runtime/provider configuration guide.

## Goal

The application can run with one explicitly selected AI provider:

```text
RAG_PROVIDER=Ollama
RAG_PROVIDER=OpenAI
RAG_PROVIDER=Gemini
```

The provider supplies both existing provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

There is deliberately **no silent automatic failover** between local and cloud providers. Sending academic documents or prompts to an external API must be an explicit operator decision because it changes data egress, availability, and cost.

## Selected provider stacks

Research snapshot: 2026-08-15.

| Provider | Chat model | Embedding model | Why it is included |
|---|---|---|---|
| Ollama (local default) | `qwen3:4b` | `qwen3-embedding:0.6b` | Existing local/offline baseline |
| OpenAI (online backup) | `gpt-5.6-luna` | `text-embedding-3-small` | Current cost-sensitive GPT-5.6 option plus a dedicated embedding model |
| Google Gemini (online backup) | `gemini-3.6-flash` | `gemini-embedding-2` | Stable/GA Flash model plus current stable Gemini embedding model |

Official references used for this decision:

- OpenAI GPT-5.6 Luna: <https://developers.openai.com/api/docs/models/gpt-5.6-luna>
- OpenAI `text-embedding-3-small`: <https://developers.openai.com/api/docs/models/text-embedding-3-small>
- OpenAI Embeddings API: <https://developers.openai.com/api/reference/resources/embeddings/methods/create>
- Google Gemini models/release notes: <https://ai.google.dev/gemini-api/docs/models>
- Google Gemini Embeddings API: <https://ai.google.dev/api/embeddings>
- Ollama `qwen3-embedding:0.6b`: <https://ollama.com/library/qwen3-embedding:0.6b>

Provider/model IDs remain environment-configurable so a later model upgrade does not require application code changes.

## Embedding compatibility invariant

The default corpus dimension is:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

This matches the current Ollama `qwen3-embedding:0.6b` output. OpenAI `text-embedding-3-small` accepts a requested output dimension, and Gemini Embedding 2 supports flexible output dimensions, so both online adapters request the same configured dimension.

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

Copy `.env.example` to `.env`. Real API keys belong only in the untracked `.env`/deployment secret environment.

Shared:

```text
RAG_PROVIDER=Ollama
RAG_EMBEDDING_DIMENSIONS=1024
```

OpenAI:

```text
OPENAI_API_KEY=
OPENAI_BASE_URL=https://api.openai.com/v1/
OPENAI_CHAT_MODEL=gpt-5.6-luna
OPENAI_EMBEDDING_MODEL=text-embedding-3-small
```

Gemini:

```text
GEMINI_API_KEY=
GEMINI_BASE_URL=https://generativelanguage.googleapis.com/
GEMINI_CHAT_MODEL=gemini-3.6-flash
GEMINI_EMBEDDING_MODEL=gemini-embedding-2
```

Ollama:

```text
OLLAMA_CHAT_MODEL=qwen3:4b
OLLAMA_EMBEDDING_MODEL=qwen3-embedding:0.6b
```

ASP.NET Core receives these as `Rag__...` settings through Docker Compose.

For `dotnet run` outside Docker Compose, export the corresponding ASP.NET Core environment variables directly (for example `Rag__Provider`, `Rag__OpenAI__ApiKey`, or `Rag__Gemini__ApiKey`). The application does not load `.env` files by itself.

## Docker usage

### Local Ollama

Ollama is under the `local-ai` Compose profile so cloud-only runs do not consume local model resources.

```bash
# .env
RAG_PROVIDER=Ollama

docker compose --profile local-ai up -d --build
```

Pull the configured Ollama models if they are not already present.

### OpenAI

```bash
# .env
RAG_PROVIDER=OpenAI
OPENAI_API_KEY=<server-side key>

docker compose up -d --build
```

The `local-ai` profile is not enabled, so the Ollama container is not started.

### Gemini

```bash
# .env
RAG_PROVIDER=Gemini
GEMINI_API_KEY=<server-side key>

docker compose up -d --build
```

Again, Ollama is not required.

## Startup validation

`ServiceCollectionExtensions` validates only the selected provider:

- unsupported `Rag:Provider` values fail fast;
- the selected provider must have an absolute base URL;
- OpenAI/Gemini require their API key;
- API keys for unselected providers are not required.

Concrete adapters are registered behind the same Application interfaces, so Flow 1 indexing and future Flow 2 RAG code do not need provider-specific branches.

## Provider HTTP behavior

OpenAI:

```text
POST /v1/embeddings
POST /v1/chat/completions
Authorization: Bearer <OPENAI_API_KEY>
```

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

No API key value is logged or committed.

## Ownership

This backup/provider foundation is assigned to **Member 1**.

Member 1 owns:

- provider selection/configuration;
- online API-key/env wiring;
- concrete provider adapters and provider-registration tests;
- embedding-dimension invariant and migration/re-index coordination;
- provider documentation.

Existing ownership remains:

- Member 3 owns the indexing pipeline/chunking/worker behavior that consumes `ITextEmbeddingService`;
- Member 4 owns future subject-scoped Flow 2 retrieval/grounding/persistence behavior that consumes the provider-neutral contracts;
- Member 5 owns future Flow 2 MVC/evaluation presentation.

## Cloud-data boundary

`Ollama` keeps model inference local to the configured Ollama runtime.

`OpenAI` and `Gemini` are external APIs. When either online provider is selected, text submitted for embeddings and later chat prompts/context leaves the application runtime and is processed by that provider. Operators must choose cloud mode intentionally and follow the provider/account data-handling requirements that apply to their deployment.
