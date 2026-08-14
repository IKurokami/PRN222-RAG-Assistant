# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, and Ollama.

The repository name remains PRN222 RAG Assistant and PRN222 remains the seeded demo subject, but the application is now designed to host **multiple subjects**. PRN222 is no longer the hard-coded workflow scope.

## Current status

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity/RBAC | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Multi-subject management + Subject Leader assignment | Complete on current feature branch | Member 1 |
| Flow 1 - Document Management & Indexing | Complete | Members 2 + 3; subject-scoping owned by Member 1 |
| Flow 2 - RAG Q&A + Conversation Management | Pending | Members 4 + 5 |
| Flow 3 - Report & Statistics | Complete | Member 2; subject-scoping owned by Member 1 |
| Repository documentation | Member 1 only | Member 1 |

Product workflows:

1. **Flow 1 - Document Management & Indexing** - MVC Controllers + Views - complete.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - MVC Controllers + Views - pending.
3. **Flow 3 - Report & Statistics** - Razor Pages - complete.

Conversation History belongs to Flow 2 and is not counted as a separate flow.

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

PRN222 is seeded so a fresh environment still has a usable demo subject. Additional subjects such as PRJ301/SWT301/SWP391 can be created at runtime by Admin.

Subject Leader assignments are persisted through ASP.NET Core Identity user claims:

```text
Claim type  = prn222:managed-subject
Claim value = <Subject Guid>
```

This reuses the existing `AspNetUserClaims` table, so the multi-subject assignment feature does **not** require a new EF Core migration.

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

`ManageDocuments` is only the coarse role gate. Every subject-specific write/report action also checks `ISubjectAccessService` against the concrete `SubjectId`.

This means a Subject Leader assigned to PRN222 cannot modify PRJ301 merely because they have the `SubjectLeader` role.

Admin routes:

```text
/admin/users
/admin/subjects
/admin/subjects/create
/admin/subjects/{id}/edit
/admin/subjects/{id}/leaders
```

Authenticated subject catalogue:

```text
/subjects
```

Flow 1/3 requests carry subject context through `subjectId`.

## Subject lifecycle

Subjects support:

- create;
- edit code/name;
- activate/deactivate;
- assign zero, one, or multiple Subject Leaders.

Hard-delete is intentionally not exposed because Chapters/Documents reference Subjects. Deactivation is the safe operational lifecycle for now.

A Subject Leader may be assigned multiple subjects. If a user is demoted away from `SubjectLeader`, managed-subject claims are removed so stale assignments cannot reactivate later.

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

`Document` and `Chapter` already contain `SubjectId`, so the indexing pipeline stays document-ID driven and does not need a second implementation for additional subjects.

## Flow 3

Reports remain Razor Pages and are now opened with a concrete `subjectId`. Chapter/document/index/chunk/failure metrics are scoped to that subject.

Chat totals are temporarily global because Flow 2 is not implemented and `ChatSession` does not yet contain `SubjectId`. This is documented intentionally rather than pretending those metrics are subject-scoped.

## Flow 2 requirement before implementation

Flow 2 must be subject-scoped from the start. Member 4/5 must not retrieve from a global corpus.

Before implementing retrieval/chat persistence, coordinate with Member 1 to introduce the necessary subject context so that:

- a chat session belongs to one subject;
- retrieval filters indexed chunks through documents belonging to that subject;
- Conversation History preserves the subject boundary;
- citations cannot leak sources from another subject;
- report chat metrics can later become subject-scoped.

Any real EF model change for Flow 2 is coordinated by Member 1 to avoid competing migrations.

## Technology

- .NET 10 / ASP.NET Core
- MVC + Razor Pages
- ASP.NET Core Identity
- EF Core
- PostgreSQL + pgvector
- Ollama
- PDF parsing via PdfPig
- DOCX/PPTX parsing via OpenXml

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

Members 2-5 report code/status/doc impacts in their PR descriptions or handoff notes; Member 1 synchronizes documentation against the actual code state.

Required reading:

- `AGENTS.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/infrastructure.md`
