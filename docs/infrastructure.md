# Infrastructure baseline

## Runtime stack

- ASP.NET Core .NET 10 MVC + Razor Pages
- ASP.NET Core Identity
- EF Core + PostgreSQL + pgvector
- provider-neutral AI interfaces
- Ollama local runtime
- Gemini direct cloud provider
- OpenRouter free-first routed provider
- optional OpenAI paid provider
- source storage under `storage/uploads/`

## AI selection

Legacy/default:

```text
Rag:Provider / RAG_PROVIDER
```

Independent overrides:

```text
Rag:ChatProvider / RAG_CHAT_PROVIDER
Rag:EmbeddingProvider / RAG_EMBEDDING_PROVIDER
```

Supported values: `Ollama`, `Gemini`, `OpenAI`, `OpenRouter`.

Blank purpose-specific overrides inherit `RAG_PROVIDER`, preserving existing deployments.

Infrastructure registers exactly one `ITextEmbeddingService` and one `IChatCompletionService`. They may come from different providers. Workflow/Application code does not branch on provider names.

## OpenRouter routing

OpenRouter chat sends an ordered `models` list. Default:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

OpenRouter performs model fallback when earlier models fail and provider routing is allowed to fall back as well.

OpenRouter embedding sends one fixed model only:

```text
nvidia/llama-nemotron-embed-vl-1b-v2:free
```

There is no embedding-model rotation.

## Vector-space invariant

```text
Rag:EmbeddingDimensions = 1024
```

Matching dimensions do not make different embedding models compatible. Changing embedding provider/model/dimensions requires a complete corpus re-index before similarity retrieval.

Chat provider/model changes alone do not require re-indexing.

## Docker modes

Local Ollama:

```bash
docker compose --profile local-ai up -d --build
```

Cloud/hybrid:

```bash
docker compose up -d --build
```

Ollama remains behind the `local-ai` profile so cloud-only deployments do not start it unnecessarily.

## API key handling

Server-side environment secrets only:

```text
GEMINI_API_KEY
OPENAI_API_KEY
OPENROUTER_API_KEY
```

Never commit/log/render real keys. Only selected providers are validated, so unused cloud keys are not required.

## Cloud-data boundary

Ollama local mode keeps inference in the configured Ollama runtime. Gemini/OpenAI/OpenRouter submit text to external services. OpenRouter free endpoints can have provider-specific logging/data-use rules; review current routing/provider policies before sending sensitive material.

## Flow 1 pipeline

```text
subject-aware request
 -> Document
 -> IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parse/chunk
 -> ITextEmbeddingService [selected embedding provider]
 -> DocumentChunk/status
```

Member 3 owns indexing behavior; Member 1 owns provider wiring.

## Flow 2 requirement

```text
subject/session boundary
 -> ITextEmbeddingService
 -> same-subject pgvector retrieval
 -> grounded prompt
 -> IChatCompletionService [selected chat provider]
 -> same-subject messages/citations
```

Member 4 must not call provider APIs directly.

## Schema impact

Provider routing/fallback adds no EF model change and requires no migration. `DocumentChunk.Embedding` remains provider-neutral vector storage.

## Validation

Before merge:

```text
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes ...
docker compose config
docker compose --profile local-ai config
PostgreSQL migration/schema validation through CI
```
