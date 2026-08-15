# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, and a provider-neutral AI runtime.

> Documentation baseline: AI-provider backup work branched from `master` after merged PR #20 on 2026-08-15.

The repository name remains PRN222 RAG Assistant and PRN222 remains the seeded demo subject, but the application is designed to host multiple subjects. PRN222 is not the application-wide hard-coded workflow scope.

## Current status

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Multi-subject management + Subject Leader assignment | Complete / merged | Member 1 |
| AI provider foundation - Ollama / Gemini / OpenAI | Implemented in this PR | **Member 1** |
| Flow 1 - Document Management & Indexing | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC integration |
| Flow 2 - RAG Q&A + Conversation Management | Pending | Member 4 backend + Member 5 MVC/evaluation |
| Flow 3 - Report & Statistics | Complete | Member 2 behavior; Member 1 subject/RBAC integration |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | **Member 3** |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 retains Identity/RBAC ownership |
| Repository documentation | Updated for provider backup | Member 1 only |

Product workflows:

1. **Flow 1 - Document Management & Indexing** - MVC Controllers + Views - complete.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - MVC Controllers + Views - pending.
3. **Flow 3 - Report & Statistics** - Razor Pages - complete.

Conversation History belongs to Flow 2 and is not counted as a separate flow.

## AI runtime - local or online

Workflow code consumes provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

Choose one provider through `.env` / deployment environment:

```text
RAG_PROVIDER=Ollama
RAG_PROVIDER=Gemini
RAG_PROVIDER=OpenAI
```

### Free/default paths

**Ollama - local, $0 provider fee**

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Inference runs on your own machine. Hardware, RAM/VRAM and electricity remain your responsibility.

**Google Gemini - online Free Tier backup**

```text
Chat:      gemini-3.6-flash
Embedding: gemini-embedding-2
```

As of 2026-08-15, Google lists both models as available on the Gemini Developer API Standard Free Tier. Free Tier usage is rate-limited and Google states Free Tier content may be used to improve its products. Re-check official pricing/rate-limit pages before production deployment because cloud terms can change.

### Optional paid path

**OpenAI - online, paid API**

```text
Chat:      gpt-5.6-luna
Embedding: text-embedding-3-small
```

OpenAI is retained as an optional provider, not as the project's free online fallback. As of 2026-08-15, GPT-5.6 Luna has no general Free API tier and OpenAI API usage is billed by usage.

Canonical setup/research notes: `docs/ai-provider-backup.md`.

There is deliberately no silent local-to-cloud failover. Operators must explicitly select a cloud provider because doing so changes data egress and potentially cost.

## Embedding compatibility

Default corpus dimension:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

The selected embedding adapter validates this dimension. OpenAI and Gemini are asked to return the configured dimension.

**Do not mix embeddings from different models/providers in one searchable corpus.** Equal vector length does not mean equal vector space. Whenever the embedding provider, model, or dimension changes, re-index the complete document corpus before similarity retrieval.

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

Provider-backup copy changes only remove the obsolete claim that AI must always be local; they do not transfer UI/UX ownership away from Member 3.

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
      +--> ITextEmbeddingService (selected provider)
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

Member 4 must not call Ollama, Gemini, or OpenAI directly. Member 1 owns provider wiring; Member 4 owns RAG behavior.

## Technology

- .NET 10 / ASP.NET Core
- MVC + Razor Pages
- ASP.NET Core Identity
- EF Core
- PostgreSQL + pgvector
- Ollama local provider
- Google Gemini Developer API online Free Tier provider
- optional OpenAI API provider
- PDF parsing via PdfPig
- DOCX/PPTX parsing via OpenXml
- Bootstrap + Bootstrap Icons
- project design system via `design-tokens.css` + `components.css`

## Environment configuration

Copy `.env.example` to `.env` for Docker Compose. `.env` is ignored by Git.

Shared:

```text
RAG_PROVIDER=Ollama
RAG_EMBEDDING_DIMENSIONS=1024
```

Gemini Free Tier online backup:

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

Gemini/OpenAI runtime (Ollama container is not required):

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
