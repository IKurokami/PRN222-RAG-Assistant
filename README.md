# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, and Ollama.

> Documentation baseline: `master` after merged PR #19 on 2026-08-15.

The repository name remains PRN222 RAG Assistant and PRN222 remains the seeded demo subject, but the application is designed to host multiple subjects. PRN222 is not the application-wide hard-coded workflow scope.

## Current status

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Multi-subject management + Subject Leader assignment | Complete / merged | Member 1 |
| Flow 1 - Document Management & Indexing | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC integration |
| Flow 2 - RAG Q&A + Conversation Management | Pending | Member 4 backend + Member 5 MVC/evaluation |
| Flow 3 - Report & Statistics | Complete | Member 2 behavior; Member 1 subject/RBAC integration |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | **Member 3** |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 retains Identity/RBAC ownership |
| Repository documentation | Synchronized after PR #19 | Member 1 only |

Product workflows:

1. **Flow 1 - Document Management & Indexing** - MVC Controllers + Views - complete.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - MVC Controllers + Views - pending.
3. **Flow 3 - Report & Statistics** - Razor Pages - complete.

Conversation History belongs to Flow 2 and is not counted as a separate flow.

## UI/UX baseline after PR #19

Member 3 completed the current application-wide presentation redesign. This is a completed task and must not be treated as unassigned work.

Implemented presentation scope includes:

- redesigned landing page and application shell;
- shared `design-tokens.css` and `components.css` design system;
- Bootstrap Icons restored through LibMan;
- redesigned Login/Register/Logout/AccessDenied/Error/Privacy experiences;
- public registration that always creates a `Student` account;
- refreshed Subjects, Admin Users, Admin Subjects, Chapters, Documents, and Reports screens;
- document search/status filtering and preserved filter context for delete/re-index actions;
- landing showcase/testimonial/FAQ/CTA sections and supporting local media assets.

This UI/UX ownership does **not** transfer existing business logic ownership. Member 2 still owns established Flow 1/Flow 3 behavior, Member 1 still owns RBAC/multi-subject rules, and Member 5 still owns future Flow 2 MVC presentation/evaluation.

See `docs/member-3-ui-ux-handoff.md`.

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

The assignment model reuses `AspNetUserClaims`, so it does not require an extra application table.

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

Public self-registration is intentionally restricted to `Student`. Elevated roles are never user-selectable from the registration form.

## Subject lifecycle

Subjects support create, edit, activate/deactivate, and assignment of zero, one, or multiple Subject Leaders. Hard-delete is intentionally not exposed while workflow data references a Subject.

If a user is changed away from `SubjectLeader`, managed-subject claims are removed.

## Flow 1

Flow 1 is MVC:

```text
SubjectsController
      |
      v
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
      +--> embed
      \--> persist DocumentChunk / status
```

`Document` and `Chapter` contain `SubjectId`, so indexing remains document-ID driven for every subject.

## Flow 3

Reports remain Razor Pages and require a concrete `subjectId`. Chapter/document/index/chunk/failure metrics are subject-scoped.

Chat totals remain temporarily global because Flow 2 is pending and `ChatSession` does not yet contain `SubjectId`.

## Flow 2 requirement before implementation

Flow 2 must be subject-scoped from the start. Member 4/5 must not retrieve from a global corpus.

Before retrieval/chat persistence is considered complete:

- a chat session must belong to one subject;
- retrieval must be constrained to indexed documents of that subject;
- Conversation History must preserve subject context;
- citations must not cross subject boundaries;
- Flow 3 chat metrics should become subject-scoped after the persistence model supports it.

Any EF model change is coordinated by Member 1 to avoid competing migrations.

## Technology

- .NET 10 / ASP.NET Core
- MVC + Razor Pages
- ASP.NET Core Identity
- EF Core
- PostgreSQL + pgvector
- Ollama
- PDF parsing via PdfPig
- DOCX/PPTX parsing via OpenXml
- Bootstrap + Bootstrap Icons
- project design system via `design-tokens.css` + `components.css`

Default local models:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

## Local configuration

Copy `.env.example` to `.env` for local Docker use and change demo credentials before using a non-disposable environment.

Important configuration:

```text
ConnectionStrings:Postgres
Database:ApplyMigrationsOnStartup
Auth:SeedUsers:Enabled
Auth:SeedUsers:Admin:*
Auth:SeedUsers:SubjectLeader:*
Auth:SeedUsers:Student:*
Rag:Ollama:BaseUrl
Rag:Ollama:ChatModel
Rag:Ollama:EmbeddingModel
Rag:Storage:UploadsPath
```

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

Members 2-5 report code/status/doc impacts in their PR descriptions or handoff notes; Member 1 synchronizes documentation against actual `master`.

Required reading:

- `AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/infrastructure.md`
- `docs/member-3-ui-ux-handoff.md`
