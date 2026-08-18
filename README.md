# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, and a provider-neutral AI runtime.

> Documentation baseline: provider routing/fallback update on 2026-08-15 after the original provider-backup foundation was merged.

The repository name remains PRN222 RAG Assistant and PRN222 remains the seeded demo subject, but the application is designed to host multiple subjects. PRN222 is not the application-wide hard-coded workflow scope.

## Current status

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Multi-subject management + Subject Leader assignment | Complete / merged | Member 1 |
| AI provider foundation - Ollama / Gemini / OpenAI / OpenRouter | Extended with free chat fallback in current PR | **Member 1** |
| Flow 1 - Document Management & Indexing | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC integration |
| Flow 2 - RAG Q&A + Conversation Management | Pending | Member 4 backend + Member 5 MVC/evaluation |
| Flow 3 - Report & Statistics | Complete | Member 2 behavior; Member 1 subject/RBAC integration |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | **Member 3** |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 retains Identity/RBAC ownership |
| Repository documentation | Updated for provider routing/fallback | Member 1 only |

Product workflows:

1. **Flow 1 - Document Management & Indexing** - MVC Controllers + Views - complete.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - MVC Controllers + Views - pending.
3. **Flow 3 - Report & Statistics** - Razor Pages - complete.

Conversation History belongs to Flow 2 and is not counted as a separate flow.

## AI runtime - local, direct cloud, or OpenRouter

Workflow code consumes provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

`RAG_PROVIDER` remains backward compatible and supplies both contracts when no purpose-specific override is configured:

```text
RAG_PROVIDER=Ollama
RAG_PROVIDER=Gemini
RAG_PROVIDER=OpenAI
RAG_PROVIDER=OpenRouter
```

Chat and embedding providers may now be selected independently:

```text
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
```

Blank overrides inherit `RAG_PROVIDER`.

### Free/default paths

**Ollama - local, $0 provider fee**

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Inference runs on your own machine. Hardware, RAM/VRAM and electricity remain your responsibility.

**Google Gemini - direct online Free Tier path**

```text
Chat:      gemini-3.6-flash
Embedding: gemini-embedding-2
```

As of 2026-08-15, Google lists both models as available on the Gemini Developer API Standard Free Tier. Free Tier usage is rate-limited and Google states Free Tier content may be used to improve its products. Re-check official pricing/rate-limit pages before production deployment because cloud terms can change.

**OpenRouter - free-first routed path**

Default ordered chat fallback:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

The OpenRouter adapter sends this list through the provider's `models` fallback mechanism. If an earlier model errors, is rate-limited, or is unavailable, OpenRouter can try the next entry. `openrouter/free` is kept last as a catch-all router over the free models currently available.

Default fixed OpenRouter embedding model:

```text
nvidia/llama-nemotron-embed-vl-1b-v2:free
```

OpenRouter free model availability/rate limits can change and are intended for development/demo/low-volume workloads. The default free embedding endpoint also has provider-specific logging/data-use terms; review the current provider policy before sending sensitive material.

### Optional paid path

**OpenAI - online, paid API**

```text
Chat:      gpt-5.6-luna
Embedding: text-embedding-3-small
```

OpenAI is retained as an optional provider, not as the project's free online fallback. As of 2026-08-15, GPT-5.6 Luna has no general Free API tier and OpenAI API usage is billed by usage.

Canonical setup/research notes: `docs/ai-provider-backup.md`.

There is deliberately no hidden application-level local-to-cloud failover. Operators explicitly select cloud providers because doing so changes data egress and potentially cost. Once `OpenRouter` is explicitly selected for chat, model/provider fallback inside OpenRouter is intentional and configurable.

## Embedding compatibility

Default corpus dimension:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

The selected embedding adapter validates this dimension. OpenAI, Gemini, and OpenRouter adapters ask their configured embedding endpoint for the configured dimension.

**Do not mix embeddings from different models/providers in one searchable corpus.** Equal vector length does not mean equal vector space. Whenever the embedding provider, model, or dimension changes, re-index the complete document corpus before similarity retrieval.

Chat fallback is different: switching/falling back between chat models does not invalidate stored document vectors and therefore does not require re-indexing.

## UI/UX baseline after PR #19

Member 3 completed the current application-wide presentation redesign. This completed task remains Member 3-owned.

Implemented presentation scope includes:

- redesigned landing page and application shell;
- shared `design-tokens.css` and `components.css` design system;
- Bootstrap Icons restored through LibMan;
- redesigned Login/Register/Logout/AccessDenied/Error/Privacy experiences;
- public registration that always creates a `Student` account;
- refreshed Subjects, Admin Users, Admin Subjects, Chapters, Documents, and Reports screens;
- document search/status filtering and preserved filter context for delete/re-index actions.

Provider-routing copy changes only remove obsolete assumptions about a single runtime and add the OpenRouter cloud boundary; they do not transfer UI/UX ownership away from Member 3.

## Multi-subject model

`Subject` is the application boundary for chapters, documents, reports, and future RAG retrieval.

```text
Admin
  |
  +--> create/edit/activate/deactivate Subject
  +--> assign Subject Leader(s)
  \--> manage any Subject as an operational override

Subject Leader
  |
  \--> manage only assigned Subject(s)
       +--> Chapters
       +--> Documents
       +--> Re-index requests
       \--> Reports

Student
  |
  \--> view active Subject(s) and their document catalogue
```

PRN222 is seeded so a fresh environment has a usable demo subject. Additional subjects can be created at runtime by Admin.

Subject Leader assignments use ASP.NET Core Identity user claims:

```text
Claim type  = prn222:managed-subject
Claim value = <Subject Guid>
```

## Roles and authorization

Roles:

```text
Admin
SubjectLeader
Student
```

Coarse policies:

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

`ManageDocuments` is only the coarse role gate. Subject-specific write/report actions also check `ISubjectAccessService` against the concrete `SubjectId`.

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
      +--> parse
      +--> chunk
      +--> ITextEmbeddingService (selected embedding provider)
      \--> persist DocumentChunk / status
```

The indexing pipeline stays document-ID driven for every subject and does not branch by provider.

## Flow 3

Reports remain Razor Pages and require a concrete `subjectId`. Chapter/document/index/chunk/failure metrics are subject-scoped and provider-independent.

Chat totals remain temporarily global because Flow 2 is pending and `ChatSession` does not yet contain `SubjectId`.

## Flow 2 requirement before implementation

Flow 2 must be subject-scoped and provider-neutral from the start.

Before retrieval/chat persistence is considered complete:

- a chat session must belong to one subject;
- question embeddings must use `ITextEmbeddingService`;
- retrieval must be constrained to indexed documents of that subject;
- grounded generation must use `IChatCompletionService`;
- Conversation History must preserve subject context;
- citations must not cross subject boundaries.

Member 4 must not call Ollama, Gemini, OpenAI, or OpenRouter directly. Member 1 owns provider wiring; Member 4 owns RAG behavior.

## Technology

- .NET 10 / ASP.NET Core
- MVC + Razor Pages
- ASP.NET Core Identity
- EF Core
- PostgreSQL + pgvector
- Ollama local provider
- Google Gemini Developer API direct online Free Tier provider
- OpenRouter free-first routed provider
- optional OpenAI API provider
- PDF parsing via PdfPig
- DOCX/PPTX parsing via OpenXml
- Bootstrap + Bootstrap Icons
- project design system via `design-tokens.css` + `components.css`

## Environment configuration

Copy `.env.example` to `.env` for Docker Compose. `.env` is ignored by Git.

Shared/backward-compatible default:

```text
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
RAG_EMBEDDING_DIMENSIONS=1024
```

Recommended free-first hybrid - OpenRouter chat fallback + Gemini embeddings:

```text
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
OPENROUTER_API_KEY=<your key>
GEMINI_API_KEY=<your key>
```

OpenRouter chat/embedding configuration:

```text
OPENROUTER_CHAT_MODELS=google/gemma-4-26b-a4b-it:free,nvidia/nemotron-3-ultra-550b-a55b:free,openrouter/free
OPENROUTER_EMBEDDING_MODEL=nvidia/llama-nemotron-embed-vl-1b-v2:free
```

Gemini direct Free Tier:

```text
RAG_PROVIDER=Gemini
GEMINI_API_KEY=<your key>
GEMINI_CHAT_MODEL=gemini-3.6-flash
GEMINI_EMBEDDING_MODEL=gemini-embedding-2
```

Optional OpenAI paid provider:

```text
RAG_PROVIDER=OpenAI
OPENAI_API_KEY=<your key>
OPENAI_CHAT_MODEL=gpt-5.6-luna
OPENAI_EMBEDDING_MODEL=text-embedding-3-small
```

Never commit real API keys.

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

Cloud/hybrid runtime (Ollama container is not required unless a selected contract uses Ollama):

```bash
docker compose up -d --build
```

Do not run `docker compose down -v` unless data deletion is explicitly intended.

## Team coordination

**Member 1 is the sole repository documentation editor.** This includes:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

Required reading:

- `AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/infrastructure.md`
- `docs/ai-provider-backup.md`
- `docs/member-3-ui-ux-handoff.md`
