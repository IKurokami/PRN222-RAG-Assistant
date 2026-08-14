# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core, EF Core, PostgreSQL/pgvector, and Ollama for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are curated by Subject Leaders and become the chatbot's authoritative source after indexing. Admins manage application accounts/roles. Chapters are runtime-managed application data.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - ASP.NET Core MVC Controllers + Views.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - ASP.NET Core MVC Controllers + Views.
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages.

Conversation History belongs to Flow 2 rather than being counted as a separate workflow.

## Presentation architecture

```text
Flow 1 -> MVC                      [COMPLETE]
Flow 2 -> MVC                      [PENDING]
Flow 3 -> Razor Pages              [COMPLETE]
Auth/shell -> Razor Pages
Admin user management -> MVC       [COMPLETE]
```

The application intentionally remains a mixed ASP.NET Core host. `Program.cs` registers/maps both MVC and Razor Pages.

## Roles and access control

Application roles:

- `Admin`
- `SubjectLeader`
- `Student`

Policy mapping:

```text
ManageUsers     -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Role responsibilities:

| Capability | Admin | Subject Leader | Student |
|---|:---:|:---:|:---:|
| Use authenticated learning features | Yes | Yes | Yes |
| View document catalogue/details | Yes | Yes | Yes |
| Manage chapters/documents/re-indexing | Yes | Yes | No |
| View Report & Statistics | Yes | Yes | No |
| Create accounts / assign roles | Yes | No | No |

Admin user management lives at:

```text
/admin/users
```

It supports creating accounts and assigning one managed application role. The UI prevents an Admin from removing their own Admin role and prevents demoting the last Admin. Hard-delete is intentionally not exposed because workflow data references application users.

See `docs/role-access-control.md`.

## Current project status and ownership

| Member | Scope | Status |
| --- | --- | --- |
| Member 1 | Core/Data, Identity/RBAC, Admin + Subject Leader access model, Admin user-management UI, global role-aware UI, all repository documentation | Complete baseline + RBAC |
| Member 2 | Flow 1 Document/Chapter Management MVC request side | Complete |
| Member 3 | Flow 1 parsing, chunking, embeddings, indexing worker/service | Complete / merged through PR #9 |
| Member 2 | Flow 3 Report & Statistics Razor Pages | Complete / merged through PR #12 |
| Member 4 | Flow 2 pgvector retrieval, grounded RAG backend, chat/citation persistence | Pending |
| Member 5 | Flow 2 MVC chat UI, Conversation History, citation rendering, evaluation | Pending |

**Documentation ownership is centralized in Member 1.** Members 2-5 report implementation/status changes to Member 1 instead of independently modifying `README.md`, `AGENTS.md`, or `docs/`.

## Flow 1 - Document Management & Indexing

MVC presentation lives under:

```text
src/PRN222.RagAssistant/Controllers/DocumentsController.cs
src/PRN222.RagAssistant/Controllers/ChaptersController.cs
src/PRN222.RagAssistant/Models/Documents/
src/PRN222.RagAssistant/Models/Chapters/
src/PRN222.RagAssistant/Views/Documents/
src/PRN222.RagAssistant/Views/Chapters/
```

Current behavior includes:

- runtime PRN222 Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index
- PDF/DOCX/PPTX validation
- 50 MB upload limit
- `AppPolicies.ManageDocuments` on writes
- anti-forgery validation on POST actions
- configured source-file storage
- optional server-side PRN222 `ChapterId` validation
- safe chapter deletion that preserves documents
- queue handoff after `Document` persistence

`ManageDocuments` is satisfied by Admin or Subject Leader. Student cannot mutate academic content.

Background indexing remains:

```text
DocumentsController upload / re-index
        |
        v
IDocumentIndexingQueue
        |
        v
InMemoryDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
DocumentIndexingService
        |
        +--> PDF parser via PdfPig
        +--> DOCX/PPTX parsers via OpenXml
        +--> TextChunker
        +--> TextEmbeddingBatcher
        +--> OllamaTextEmbeddingService
        +--> replace/persist DocumentChunk rows
        \--> Indexed / Failed
```

Flow 1 MVC controllers must not absorb parser/chunker/embedding/RAG-provider logic.

## Flow 3 - Report & Statistics

Flow 3 remains Razor Pages under `src/PRN222.RagAssistant/Pages/Reports/`.

It reports read-only PRN222 aggregates including:

- chapters/documents
- unassigned documents
- documents by chapter
- indexing states/completion
- persisted chunks
- recent failures and recently indexed documents
- chat session/message/citation totals

Access uses `AppPolicies.ManageDocuments`, so Admin and Subject Leader can view it. Flow 3 remains read-only and does not call Ollama or perform pgvector similarity retrieval.

## Pending Flow 2 - MVC

Member 4 owns the presentation-agnostic RAG backend:

```text
Question
   |
   v
ITextEmbeddingService.EmbedAsync
   |
   v
pgvector retrieval over Indexed PRN222 chunks
   |
   v
Grounded context
   |
   v
IChatCompletionService -> Ollama
   |
   +--> persist ChatMessage
   \--> persist MessageCitation
   |
   v
RagAnswer + RagCitation[]
```

Member 5 owns MVC chat/session/history/citation presentation. Student session ownership must be enforced server-side; no member should create a parallel role-management UI.

## Member 1 RBAC surface

Member 1 owns end-to-end:

```text
Security/AppRoles.cs
Security/AppPolicies.cs
Infrastructure/Identity/IdentitySeeder.cs
Controllers/AdminUsersController.cs
Models/Admin/AdminUserViewModels.cs
Views/AdminUsers/
Pages/Shared/_Layout.cshtml role-aware navigation
role/policy regression tests
README.md / AGENTS.md / docs/
```

This ownership means role-aware UI changes around Flow 1/Flow 3 belong to Member 1, while the underlying Flow 1/Flow 3 business behavior remains with Member 2.

## Local setup

Copy environment defaults:

```text
cp .env.example .env
```

Windows Command Prompt:

```text
copy .env.example .env
```

Restore and validate:

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
```

Start services:

```text
docker compose up -d --build
docker compose ps
```

Compose starts ASP.NET Core, PostgreSQL + pgvector, and Ollama.

## Authentication and demo accounts

Demo-user seeding is disabled by default. To enable it locally:

```text
AUTH_SEED_USERS=true
```

Example identities in `.env.example`:

```text
admin@prn222.local
leader@prn222.local
student@prn222.local
```

Change example passwords before use outside a disposable development environment.

Sign in at:

```text
http://localhost:8080/Account/Login
```

There is no public self-service role selection. Admin/SubjectLeader elevation is controlled through seeded configuration or Admin user management.

## Persistence and migrations

Application persistence uses `ApplicationDbContext` with ASP.NET Core Identity.

Project conventions:

- no navigation properties in domain entities
- scalar foreign keys
- dedicated `IEntityTypeConfiguration<TEntity>` per entity
- entity-specific Fluent API stays out of `ApplicationDbContext`
- application schema changes use EF Core migrations

Adding `Admin` and changing user-role memberships does **not** require an EF migration because Identity role/user-role tables already exist.

Generate a migration only for a genuine model change:

```text
dotnet ef migrations add <MigrationName> --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant
```

## Document storage

Uploaded documents use configured `Rag:Storage:UploadsPath`, defaulting to `storage/uploads/` locally. Runtime uploads are ignored by Git; PostgreSQL is the source of truth for document metadata/indexing state/chunks.

## Ollama models

Default local models:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

If the embedding model changes after indexing, affected documents must be re-indexed.

## Required project reading

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/role-access-control.md
docs/flow-1-mvc-migration.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```
