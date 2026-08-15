# Infrastructure baseline

> Updated for AI-provider backup work based on `master` after PR #20.

## Runtime stack

- ASP.NET Core .NET 10 host with MVC + Razor Pages.
- ASP.NET Core Identity.
- EF Core + PostgreSQL.
- pgvector for embeddings.
- provider-neutral AI interfaces with:
  - Ollama local/default runtime;
  - Google Gemini Developer API online Free Tier backup;
  - optional paid OpenAI API provider.
- runtime source storage under `storage/uploads/`.
- Bootstrap + Bootstrap Icons for presentation dependencies.
- shared UI design system through `wwwroot/css/design-tokens.css` and `wwwroot/css/components.css`.

PRN222 is the seeded demo subject; the runtime application is multi-subject.

## AI provider selection

Exactly one provider is selected at application startup:

```text
Rag:Provider = Ollama | Gemini | OpenAI
```

Docker `.env` mapping:

```text
RAG_PROVIDER=Ollama
RAG_EMBEDDING_DIMENSIONS=1024
```

The provider supplies both:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete adapters live in Infrastructure. Flow 1 indexing and future Flow 2 RAG behavior must not branch on provider names.

There is intentionally no automatic local-to-cloud failover. An operator must explicitly choose cloud mode because external providers change data egress and may introduce billing.

## Provider matrix

Research snapshot: 2026-08-15.

| Runtime | Chat | Embedding | Cost position |
|---|---|---|---|
| Ollama | `qwen3:4b` | `qwen3-embedding:0.6b` | local provider fee $0; own compute |
| Gemini | `gemini-3.6-flash` | `gemini-embedding-2` | **online Standard Free Tier available**; rate limited |
| OpenAI | `gpt-5.6-luna` | `text-embedding-3-small` | **paid API / optional** |

Canonical official-source notes and setup: `docs/ai-provider-backup.md`.

## Embedding dimension/vector-space invariant

```text
Rag:EmbeddingDimensions = 1024
```

All adapters validate the configured output dimension. OpenAI/Gemini requests include the configured reduced output dimension.

Matching dimensions do **not** make two embedding models compatible. Changing provider/model/dimensions invalidates the semantic vector space already stored in `DocumentChunk.Embedding`.

Operational rule:

```text
change embedding provider/model/dimension
        -> mark corpus embeddings stale
        -> re-index every searchable document
        -> only then enable similarity retrieval
```

Do not mix old/new embedding models in one searchable corpus.

## Docker modes

Ollama is isolated behind the `local-ai` Compose profile.

Local:

```bash
docker compose --profile local-ai up -d --build
```

Online Gemini/OpenAI:

```bash
docker compose up -d --build
```

This prevents a cloud-only deployment from starting/downloading Ollama unnecessarily.

## API key handling

Online keys are server-side environment configuration only:

```text
GEMINI_API_KEY
OPENAI_API_KEY
```

Docker Compose maps them to:

```text
Rag__Gemini__ApiKey
Rag__OpenAI__ApiKey
```

The selected cloud provider fails fast when its key is missing. Unselected provider keys are not required.

Never commit keys to `.env.example`, appsettings, documentation examples, tests, or source code. Never log them or render them to HTML/JavaScript.

## Cloud-data boundary

Ollama local mode keeps inference in the configured local Ollama runtime.

Gemini/OpenAI modes submit embedding text and future chat prompt/context to the configured external provider. Operators must treat provider selection as a privacy/deployment choice, not merely a performance switch.

Google's Gemini Developer API Free Tier is useful for development/demo cost control, but it is rate-limited and Google's pricing page states Free Tier content may be used to improve products. Re-check current provider terms before real deployment.

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

Provider backup requires no schema migration. `DocumentChunk.Embedding` remains provider-neutral vector storage.

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
 -> ITextEmbeddingService [selected provider]
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
 -> IChatCompletionService
 -> same-subject message/citation persistence
```

Member 4 must not call a concrete provider API directly.

## Ownership

**Member 1** owns provider selection/configuration, online API-key wiring, concrete provider adapters, provider tests, dimension/re-index coordination, and provider docs.

Existing ownership is preserved:

- Member 3 owns indexing/chunking/worker behavior consuming `ITextEmbeddingService`;
- Member 4 owns future Flow 2 RAG behavior consuming both provider-neutral AI contracts;
- Member 5 owns Flow 2 MVC/evaluation presentation.

## Intentionally not added

- silent cloud failover;
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
