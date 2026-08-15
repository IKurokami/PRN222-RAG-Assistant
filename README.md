# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, and a provider-neutral AI runtime.

PRN222 remains the seeded demo subject, but the application supports multiple subjects.

## Current status

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Multi-subject management + Subject Leader assignment | Complete | Member 1 |
| AI provider foundation / routing | Ollama, Gemini, OpenAI + OpenRouter free fallback | **Member 1** |
| Flow 1 - Document Management & Indexing | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC/provider integration |
| Flow 2 - RAG Q&A + Conversation Management | Pending | Member 4 backend + Member 5 MVC/evaluation |
| Flow 3 - Report & Statistics | Complete | Member 2 behavior; Member 1 subject/RBAC integration |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | Member 3 |
| Repository documentation | Synchronized with provider routing | Member 1 |

Product workflows:

1. **Flow 1 - Document Management & Indexing** - MVC - complete.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - MVC - pending.
3. **Flow 3 - Report & Statistics** - Razor Pages - complete.

Conversation History belongs to Flow 2 and is not counted as a separate flow.

## AI runtime

Workflow code consumes only:

```text
ITextEmbeddingService
IChatCompletionService
```

Supported providers:

```text
Ollama
Gemini
OpenAI
OpenRouter
```

`RAG_PROVIDER` remains backward compatible and supplies both contracts when no per-purpose override is set:

```env
RAG_PROVIDER=Ollama
```

Chat and embeddings can now be selected independently:

```env
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
```

Resolution order is:

```text
RAG_CHAT_PROVIDER      -> RAG_PROVIDER -> Ollama
RAG_EMBEDDING_PROVIDER -> RAG_PROVIDER -> Ollama
```

### Recommended free-first hybrid

```env
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
RAG_EMBEDDING_DIMENSIONS=1024
OPENROUTER_API_KEY=<server-side key>
GEMINI_API_KEY=<server-side key>
```

OpenRouter chat uses an ordered fallback chain by default:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

OpenRouter automatically tries the next configured chat model when an earlier model fails. `openrouter/free` is the final catch-all free router and chooses from the free models currently available to OpenRouter.

### Provider matrix

| Provider | Chat | Embedding | Position |
|---|---|---|---|
| Ollama | `qwen3:4b` | `qwen3-embedding:0.6b` | local / $0 provider fee |
| Gemini | `gemini-3.6-flash` | `gemini-embedding-2` | direct online Free Tier path |
| OpenRouter | ordered free-model chain | `nvidia/llama-nemotron-embed-vl-1b-v2:free` | free-first router; low limits/availability can vary |
| OpenAI | `gpt-5.6-luna` | `text-embedding-3-small` | optional paid API |

OpenRouter's free tier is intended for development/demo/low-volume usage. Free model availability and rate limits can change. The default OpenRouter free embedding endpoint also has provider data-logging terms; do not use sensitive material there without reviewing current provider policy.

Canonical research/setup notes: `docs/ai-provider-backup.md`.

## Why chat rotates but embeddings do not

Chat completions are stateless with respect to the stored pgvector corpus, so the application may use an ordered fallback chain without re-indexing documents.

Embeddings are different. A searchable corpus must stay in one embedding vector space. The OpenRouter embedding adapter therefore uses exactly one configured model:

```env
OPENROUTER_EMBEDDING_MODEL=nvidia/llama-nemotron-embed-vl-1b-v2:free
RAG_EMBEDDING_DIMENSIONS=1024
```

**Never rotate between different embedding models for already indexed documents.** Changing embedding provider, embedding model, or dimensions requires a complete corpus re-index before similarity retrieval. Equal vector length does not make different embedding models compatible.

## Multi-subject model

`Subject` is the application boundary for chapters, documents, reports, and future RAG retrieval.

```text
Admin
  +--> manage subjects/users/roles
  +--> assign Subject Leaders
  \--> operational override for any subject

Subject Leader
  \--> assigned subjects only
       +--> Chapters
       +--> Documents
       +--> Re-index requests
       \--> Reports

Student
  \--> active subjects/document catalogue
```

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

Subject-specific writes/reports also use `ISubjectAccessService`.

## Flow 1

```text
DocumentsController / ChaptersController
 -> subject authorization
 -> persist Document
 -> IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> DocumentIndexingService
 -> parse/chunk
 -> ITextEmbeddingService [selected embedding provider]
 -> DocumentChunk / status
```

Member 3 retains indexing ownership. Provider routing does not create provider-specific workers.

## Flow 2 requirement

Flow 2 must remain subject-scoped and provider-neutral:

```text
selected subject
 -> ITextEmbeddingService
 -> same-subject pgvector retrieval
 -> grounded prompt
 -> IChatCompletionService
 -> same-subject messages/citations/history
```

Member 4 must not call Ollama, Gemini, OpenAI, or OpenRouter directly.

## Technology

- .NET 10 / ASP.NET Core
- MVC + Razor Pages
- ASP.NET Core Identity
- EF Core
- PostgreSQL + pgvector
- Ollama local runtime
- Google Gemini Developer API
- OpenRouter free-first routing
- optional OpenAI API
- PdfPig + OpenXml document parsing
- Bootstrap + Bootstrap Icons

## Environment configuration

Copy `.env.example` to `.env`. `.env` is ignored by Git.

Local Ollama for both chat/embedding:

```env
RAG_PROVIDER=Ollama
```

```bash
docker compose --profile local-ai up -d --build
```

OpenRouter chat fallback + Gemini embedding:

```env
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
OPENROUTER_API_KEY=<key>
GEMINI_API_KEY=<key>
```

```bash
docker compose up -d --build
```

OpenRouter for both contracts:

```env
RAG_PROVIDER=OpenRouter
OPENROUTER_API_KEY=<key>
RAG_EMBEDDING_DIMENSIONS=1024
```

If the corpus was indexed with another embedding model, re-index all documents before retrieval.

Never commit real API keys.

## Validation

```bash
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant

docker compose config
docker compose --profile local-ai config
```

Do not run `docker compose down -v` unless data deletion is explicitly intended.

## Team coordination

**Member 1 is the sole repository documentation editor** for `README.md`, all `AGENTS.md` files, and `docs/*`.

Required reading:

- `AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/infrastructure.md`
- `docs/ai-provider-backup.md`
