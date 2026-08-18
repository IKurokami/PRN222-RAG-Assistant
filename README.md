# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, and a provider-neutral AI runtime.

> Documentation baseline: synchronized on 2026-08-18 after PR #30 merged and issue #27 closed.

PRN222 remains the seeded demo subject, but the application supports multiple subjects and must not treat PRN222 as the global hard-coded scope.

## Current status

| Area | Status | Ownership |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Multi-subject management + Subject Leader assignment | Complete | Member 1 |
| AI provider runtime - Ollama/Gemini/OpenAI/OpenRouter | Complete | Member 1 |
| Flow 1 - Document Management & Indexing | Complete | Member 2 request behavior + Member 3 indexing maintenance |
| Flow 2 - RAG backend | Complete / merged through PR #30 | Member 4 |
| Flow 2 - MVC Chat/history/citations/evaluation | Pending | Member 5 |
| Flow 3 - Report & Statistics | Complete | Member 2 |
| Cross-app UI/UX baseline | Complete | Member 3 |
| Repository documentation/coordination | Active | Member 1 |

Product workflows:

1. **Flow 1 - Document Management & Indexing** - MVC Controllers + Views - complete.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - backend complete; final MVC product UI/evaluation pending.
3. **Flow 3 - Report & Statistics** - Razor Pages - complete.

Conversation History belongs to Flow 2 and is not a separate workflow.

## Contribution accounting

Repository ownership and actual contribution credit are tracked separately.

Canonical ledger:

- `docs/member-contributions.md`

Important rules:

- project documentation uses **Member numbers only**;
- do not add GitHub usernames to README/AGENTS/docs;
- do not double-credit work to an owner when another member delivered the merged implementation;
- keep PR numbers as auditable evidence.

Examples from the current baseline:

- Member 1 receives implementation credit for the original indexing pipeline merged in PR #9 and chunk-preview/chunking/PDF work in PR #23;
- Member 3 still owns ongoing indexing/ingestion maintenance;
- Member 4 receives implementation credit for the issue #27 remediation and Flow 2 backend merged in PR #30.

## AI runtime

Workflow code consumes provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

Backward-compatible shared provider:

```text
RAG_PROVIDER=Ollama
```

Optional purpose-specific overrides:

```text
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
```

Supported providers:

```text
Ollama
Gemini
OpenAI
OpenRouter
```

### Default/local path

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

### Direct online path

Gemini is the preferred direct online Free Tier development/demo option when available under the provider's current terms.

### OpenRouter path

The configured OpenRouter chat adapter supports an ordered free-model fallback chain. Embeddings intentionally use one fixed configured embedding model per corpus.

### Embedding invariant

Default configured corpus dimension is 1024.

**Never mix embeddings from different models/providers in one searchable corpus.** Equal vector dimensions do not imply compatible vector spaces.

If embedding provider/model/dimension changes, re-index the complete document corpus. Changing only chat provider/model/fallback order does not require re-indexing.

Canonical provider notes: `docs/ai-provider-backup.md`.

## Multi-subject model

`Subject` is the application boundary for chapters, documents, reports, chat sessions and RAG retrieval.

```text
Admin
  +--> manage Subjects
  +--> assign Subject Leader(s)
  \--> manage any Subject as an operational override

Subject Leader
  \--> manage assigned Subject(s)
       +--> Chapters
       +--> Documents
       +--> Re-index
       \--> Reports

Student
  +--> view active Subject(s)
  \--> use subject-scoped chat when Flow 2 MVC UI is completed
```

`ChatSession.SubjectId` is now persisted and the Member 4 backend carries subject context through the RAG pipeline.

## Roles and authorization

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

Subject-specific write/report actions also check `ISubjectAccessService` against the concrete `SubjectId`.

Public self-registration is restricted to `Student`.

## Flow 1

Flow 1 is MVC:

```text
DocumentsController / ChaptersController
      |
      +--> subject-specific authorization
      +--> validate/persist
      v
IDocumentIndexingQueue
      |
      v
DocumentIndexingWorker
      |
      v
DocumentIndexingService
      +--> parse PDF/DOCX/PPTX
      +--> chunk
      +--> ITextEmbeddingService
      \--> persist DocumentChunk / status
```

### Issue #27 status

Issue #27 is closed after PR #30.

Merged hardening includes:

- deterministic bounded overlap;
- Unicode normalization and safer grapheme handling;
- configurable chunk size/overlap with startup validation;
- improved PDF two-column reading order;
- PDF regression coverage;
- DOCX fake-page correction;
- additional DOCX/PPTX parser/integration coverage.

PDF is the primary real-world ingestion format currently being tested most heavily.

Deferred follow-up debt:

- deeper DOCX complex layout/list/table fixtures;
- deeper PPTX grouped-shape/table/group-transform fixtures;
- more difficult PDF table/side-note/rotated-text cases.

## Flow 2

### Backend - complete

Member 4 backend now provides:

```text
subject-aware ChatSession
 -> ITextEmbeddingService
 -> subject-scoped pgvector retrieval
 -> grounded prompt/history
 -> IChatCompletionService
 -> citation marker parsing
 -> message/citation persistence
```

It validates session ownership and subject consistency, avoids duplicating the current question in history, and persists only citations actually referenced by the generated answer.

### Product MVC UI - pending

Member 5 still owns:

- MVC Chat/session/history/citation UI;
- subject-aware conversation navigation;
- user-facing citation rendering;
- evaluation tooling.

The internal RAG demo Razor Page is a development aid and is not the final Flow 2 product presentation.

## Flow 3

Reports remain Razor Pages and are read-only.

`ChatSession.SubjectId` now exists, so existing chat aggregate queries should be audited when Member 5 completes Flow 2 to make report scoping explicit.

## Technology

- .NET 10 / ASP.NET Core
- MVC + Razor Pages
- ASP.NET Core Identity
- EF Core
- PostgreSQL + pgvector
- Ollama / Gemini / OpenAI / OpenRouter provider adapters
- PDF parsing via PdfPig
- DOCX/PPTX parsing via OpenXml
- Bootstrap + Bootstrap Icons
- shared project design system

## Environment configuration

Copy `.env.example` to `.env` for Docker Compose. `.env` is ignored by Git.

Example:

```text
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
RAG_EMBEDDING_DIMENSIONS=1024
```

Recommended free-first hybrid for development/demo:

```text
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
```

Never commit real API keys.

## Render CD

`render.yaml` defines the Render deployment stack and waits for GitHub checks to pass before auto-deploying `master`.

The Render deployment uses OpenRouter for both contracts:

```text
Chat:      nvidia/nemotron-3.5-lightning:free
Embedding: nvidia/llama-nemotron-embed-vl-1b-v2:free (1024 dimensions)
```

Only `Rag__OpenRouter__ApiKey` is entered manually in the Render Dashboard. See `docs/render-deployment.md` for first-deploy steps, database wiring, health checks, and free-tier storage caveats.

## Commands

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
```

Local Ollama runtime:

```bash
docker compose --profile local-ai up -d --build
```

Cloud/hybrid runtime:

```bash
docker compose up -d --build
```

Do not run `docker compose down -v` unless data deletion is explicitly intended.

## Team coordination

**Member 1 is the sole repository documentation editor.**

Required reading:

- `AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/member-contributions.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/infrastructure.md`
- `docs/ai-provider-backup.md`
- `docs/render-deployment.md`
- `docs/member-3-document-indexing-handoff.md`
- `docs/member-4-rag-backend-handoff.md`
