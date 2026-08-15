# Infrastructure baseline

> Updated for OpenRouter free-model routing/fallback on top of the merged provider-backup foundation.

## Runtime stack

- ASP.NET Core .NET 10 host with MVC + Razor Pages.
- ASP.NET Core Identity.
- EF Core + PostgreSQL.
- pgvector for embeddings.
- provider-neutral AI interfaces with:
  - Ollama local/default runtime;
  - Google Gemini Developer API direct online Free Tier path;
  - OpenRouter free-first routed provider;
  - optional paid OpenAI API provider.
- runtime source storage under `storage/uploads/`.
- Bootstrap + Bootstrap Icons for presentation dependencies.
- shared UI design system through `wwwroot/css/design-tokens.css` and `wwwroot/css/components.css`.

PRN222 is the seeded demo subject; the runtime application is multi-subject.

## AI provider selection

`Rag:Provider` remains the backward-compatible default for both contracts:

```text
Rag:Provider = Ollama | Gemini | OpenAI | OpenRouter
```

Purpose-specific overrides may be configured independently:

```text
Rag:ChatProvider      = Ollama | Gemini | OpenAI | OpenRouter
Rag:EmbeddingProvider = Ollama | Gemini | OpenAI | OpenRouter
```

Docker `.env` mapping:

```text
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
RAG_EMBEDDING_DIMENSIONS=1024
```

Blank overrides inherit `RAG_PROVIDER`.

Infrastructure still registers exactly one implementation of each:

```text
ITextEmbeddingService
IChatCompletionService
```

The two implementations may come from different providers. Flow 1 indexing and future Flow 2 RAG behavior must not branch on provider names.

There is intentionally no hidden application-level local-to-cloud failover. An operator explicitly chooses cloud mode. OpenRouter model/provider fallback is allowed only after OpenRouter itself has been selected for the relevant contract.

## Provider matrix

Research snapshot: 2026-08-15.

| Runtime | Chat | Embedding | Cost position |
|---|---|---|---|
| Ollama | `qwen3:4b` | `qwen3-embedding:0.6b` | local provider fee $0; own compute |
| Gemini | `gemini-3.6-flash` | `gemini-embedding-2` | direct online Standard Free Tier available; rate limited |
| OpenRouter | ordered free fallback chain | `nvidia/llama-nemotron-embed-vl-1b-v2:free` | free-first routing; low limits/availability can vary |
| OpenAI | `gpt-5.6-luna` | `text-embedding-3-small` | paid API / optional |

Canonical official-source notes and setup: `docs/ai-provider-backup.md`.

## OpenRouter chat routing

Default ordered chat list:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

The Infrastructure adapter sends the list through OpenRouter's `models` field and explicitly allows provider fallback. An error/rate-limit/downtime on an earlier model can move the request to the next configured model.

`openrouter/free` is last because it is a catch-all router whose free model selection/availability can change.

## Embedding dimension/vector-space invariant

```text
Rag:EmbeddingDimensions = 1024
```

All embedding adapters validate the configured output dimension. OpenAI/Gemini/OpenRouter requests include the configured output dimension.

Matching dimensions do **not** make two embedding models compatible. Changing embedding provider/model/dimensions invalidates the semantic vector space already stored in `DocumentChunk.Embedding`.

Operational rule:

```text
change embedding provider/model/dimension
        -> mark corpus embeddings stale
        -> re-index every searchable document
        -> only then enable similarity retrieval
```

Do not rotate embedding models or mix old/new embedding models in one searchable corpus. Chat model/provider changes alone do not require re-indexing.

## Docker modes

Ollama is isolated behind the `local-ai` Compose profile.

Local:

```bash
docker compose --profile local-ai up -d --build
```

Online/hybrid Gemini/OpenAI/OpenRouter:

```bash
docker compose up -d --build
```

This prevents a cloud-only deployment from starting/downloading Ollama unnecessarily. If one purpose-specific provider is Ollama, use the `local-ai` profile.

## API key handling

Online keys are server-side environment configuration only:

```text
GEMINI_API_KEY
OPENAI_API_KEY
OPENROUTER_API_KEY
```

Docker Compose maps them to:

```text
Rag__Gemini__ApiKey
Rag__OpenAI__ApiKey
Rag__OpenRouter__ApiKey
```

Only providers selected by chat or embedding are validated. Unselected provider keys are not required.

Never commit keys to `.env.example`, appsettings, documentation examples, tests, or source code. Never log them or render them to HTML/JavaScript.

## Cloud-data boundary

Ollama local mode keeps inference in the configured local Ollama runtime.

Gemini/OpenAI/OpenRouter modes submit embedding text and/or future chat prompt/context to the configured external provider. Operators must treat provider selection as a privacy/deployment choice, not merely a performance switch.

Google's Gemini Developer API Free Tier is useful for development/demo cost control, but it is rate-limited and Google's pricing page states Free Tier content may be used to improve products.

OpenRouter free models also have low limits and provider-specific data policies. The default free OpenRouter embedding endpoint currently warns that prompts/output are logged for provider improvement and should not receive personal/confidential/sensitive data. Re-check current terms before real deployment.

## Authentication/authorization

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Subject-resource authorization is implemented by `ISubjectAccessService` and must accompany `ManageDocuments` for subject-specific writes/reports.

Public registration creates `Student` accounts only.

## PostgreSQL system of record

PostgreSQL persists:

- Subjects/Chapters;
- Documents/index state;
- DocumentChunks/embeddings;
- Identity users/roles/claims;
- ChatSessions/ChatMessages;
- MessageCitations.

Provider routing requires no schema migration. `DocumentChunk.Embedding` remains provider-neutral vector storage.

## MVC/Razor allocation

```text
MVC:
  Flow 1 Documents/Chapters
  pending Flow 2 Chat
  Admin Users
  Subjects/Admin Subjects

Razor Pages:
  Auth/shell
  Flow 3 Reports
```

## Flow 1 indexing pipeline

```text
subject-aware HTTP request
 -> persist Document with SubjectId
 -> IDocumentIndexingQueue
 -> InMemoryDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parse/chunk
 -> ITextEmbeddingService [selected embedding provider]
 -> persist chunks/status
```

The queue is process-local. Startup recovery re-enqueues persisted Uploaded/Processing documents.

Parsers:

- PDF: PdfPig;
- DOCX/PPTX: OpenXml.

The indexing pipeline is not duplicated per subject or provider.

## Flow 2 infrastructure requirement

Flow 2 remains pending. It must consume provider-neutral interfaces:

```text
subject/session boundary
 -> ITextEmbeddingService
 -> pgvector retrieval restricted by Document.SubjectId
 -> grounded prompt
 -> IChatCompletionService [selected chat provider]
 -> same-subject message/citation persistence
```

Member 4 must not call a concrete provider API directly.

## Ownership

**Member 1** owns provider selection/configuration, OpenRouter routing/fallback, online API-key wiring, concrete provider adapters, provider tests, dimension/re-index coordination, and provider docs.

Existing ownership is preserved:

- Member 3 owns indexing/chunking/worker behavior consuming `ITextEmbeddingService`;
- Member 4 owns future Flow 2 RAG behavior consuming both provider-neutral AI contracts;
- Member 5 owns Flow 2 MVC/evaluation presentation.

## Intentionally not added

- hidden application-level local-to-cloud failover;
- embedding-model rotation;
- Redis/RabbitMQ/external broker;
- another vector DB;
- RAGFlow/LangChain service;
- provider-specific logic in MVC/Razor pages;
- provider-specific contracts in Application;
- API keys in repository files.

## Validation

Before merge run:

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes ...
docker compose config
docker compose --profile local-ai config
PostgreSQL migration/schema/pgvector validation through CI
```
