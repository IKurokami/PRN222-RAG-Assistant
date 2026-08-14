# Agent Instructions

## Scope

This file applies to the whole repository. Deeper `AGENTS.md` files add rules for their subtree.

Follow explicit user requests when they change the current phase. Otherwise preserve the architecture and ownership below.

Before changing workflow code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/role-access-control.md
docs/flow-1-mvc-migration.md
```

For indexing/RAG work also read `docs/member-3-document-indexing-handoff.md`.
For reporting work also read `docs/flow-3-report-statistics-handoff.md`.

## Current project baseline

- Main project: `src/PRN222.RagAssistant`
- Test project: `tests/PRN222.RagAssistant.Tests`
- Target: `net10.0`
- Web host: ASP.NET Core MVC + Razor Pages
- Auth: ASP.NET Core Identity + EF Core/PostgreSQL
- Roles: `Admin`, `SubjectLeader`, `Student`
- Policies: `ManageUsers`, `ManageDocuments`
- Database/vector store: PostgreSQL + pgvector
- Local AI runtime: Ollama
- Source storage: `storage/uploads/`
- Solution: `PRN222-RAG-Assistant.sln`

Presentation allocation:

```text
Flow 1 -> MVC Controllers + Views   [COMPLETE]
Flow 2 -> MVC Controllers + Views   [PENDING]
Flow 3 -> Razor Pages               [COMPLETE]
Auth/shell -> Razor Pages
Admin user management -> MVC        [COMPLETE]
```

The demo is scoped to PRN222. Subject Leaders curate/upload course documents. Admins manage application accounts/roles and may override academic management when operationally required. Students consume indexed content and pending Flow 2 chat.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - MVC
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - MVC
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages

Conversation History belongs to Flow 2 and is not counted as a separate workflow.

## Team ownership and integration boundaries

### Member 1 - Core/Data + RBAC + documentation owner

Member 1 owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- cross-workflow `Application/` contracts/models
- schema/migration conventions
- architecture/convention tests
- ASP.NET Core Identity setup and seeding
- Admin/SubjectLeader/Student role model
- global authorization policies
- Admin user/role management MVC controller/models/views
- role-aware shared navigation/UI
- role/policy regression tests
- coordination for genuine EF model changes
- **all repository documentation edits**: `README.md`, root/deeper `AGENTS.md`, and `docs/`

Members 2-5 do not independently edit coordination/handoff documentation. They report code/status changes to Member 1, who synchronizes docs.

Member 1 owns role-aware UI/policy wiring even when it affects another member's screen. This does not transfer that screen's workflow business logic to Member 1.

### Member 2 - Flow 1 Document Management + Flow 3 Reporting

Flow 1 request/presentation is complete under:

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

The old `Pages/Documents/` and `Pages/Chapters/` implementation must not be recreated.

Owned Flow 1 behavior:

- runtime PRN222 Chapter CRUD
- Document list/filter/upload/details/edit/delete/re-index
- PDF/DOCX/PPTX validation and 50 MB limit
- source-file persistence
- document metadata persistence
- PRN222 chapter validation
- queue handoff after persistence
- safe chapter deletion that unassigns referenced documents

Flow 1 write actions use `AppPolicies.ManageDocuments`. Member 1 owns changes to who satisfies that policy; Member 2 owns the business actions protected by it.

Flow 1 controllers must not parse, chunk, embed, query pgvector, or call Ollama directly.

Flow 3 remains read-only under `Pages/Reports/`. It uses `AppPolicies.ManageDocuments` and must not mutate workflow state, enqueue indexing, run similarity retrieval, call Ollama, duplicate Conversation History, or add speculative analytics schema.

### Member 3 - Document Indexing / Ingestion - COMPLETE

Owns the merged parser/chunker/embedding/indexing pipeline:

- `DocumentParserFactory`
- PDF via PdfPig
- DOCX/PPTX via OpenXml
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- coherent `DocumentChunk` replacement
- indexing state/error/timestamp persistence
- startup recovery of `Uploaded`/`Processing` documents

Do not create a second indexing pipeline.

### Member 4 - RAG / Chat Backend - PENDING

Owns:

- question embedding through `ITextEmbeddingService`
- pgvector retrieval over successfully indexed PRN222 chunks
- top-K context selection
- grounded prompt construction
- explicit no-evidence/out-of-scope behavior
- `IChatCompletionService`
- `IRagQueryService`
- authenticated chat-session ownership validation
- user/assistant message persistence
- ordered `MessageCitation` persistence

Member 4 remains presentation-agnostic. Any new global role/policy requirement must be coordinated with Member 1.

### Member 5 - Flow 2 MVC Presentation / Conversation Management / Evaluation - PENDING

Owns:

- focused MVC chat/session controller actions
- `Views/Chat/`
- session creation/open/navigation
- Conversation History
- citation/source rendering
- consumption of `IRagQueryService`
- human-authored 50-question evaluation set/tooling

Do not implement Flow 2 under Razor Pages. Do not add role-management UI. Do not independently edit repository docs; report status/doc changes to Member 1.

## RBAC rules

Canonical design: `docs/role-access-control.md`.

Role responsibilities:

```text
Admin         -> accounts/roles + academic-management override + reports
SubjectLeader -> PRN222 chapters/documents/indexing requests + reports
Student       -> learning consumer; pending own Flow 2 sessions/history
```

Policy mapping:

```text
AppPolicies.ManageUsers     -> Admin
AppPolicies.ManageDocuments -> Admin OR SubjectLeader
```

Rules:

- role names live only in `Security/AppRoles.cs`;
- policy names live only in `Security/AppPolicies.cs`;
- Admin user-management endpoints require `ManageUsers` server-side;
- Flow 1 writes and Flow 3 reports require `ManageDocuments` server-side;
- state-changing MVC/Razor form posts use anti-forgery validation;
- hiding UI is never authorization;
- users must never self-select Admin or SubjectLeader through public/self-service UI;
- Admin cannot remove their own Admin role through the management UI;
- the last Admin cannot be demoted;
- do not hard-delete users while workflow rows reference `ApplicationUser`;
- demo-user seeding is config-driven and disabled by default;
- never commit real credentials.

## Presentation model rules

The mixed host is intentional:

- Flow 1: MVC Controllers + Views - complete.
- Flow 2: MVC Controllers + Views - required.
- Flow 3: Razor Pages - complete.
- Authentication/shell pages may remain Razor Pages.
- Admin user management uses MVC.

MVC controllers handle HTTP/model binding/authorization/navigation and delegate provider/indexing/RAG logic behind service boundaries.

## Shared-contract rules

Cross-member integration points live under `src/PRN222.RagAssistant/Application/`:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Treat public signatures as stable. Prefer additive changes. If a contract must change, update affected producers/consumers together. Member 1 owns the corresponding documentation update.

## Mandatory EF Core entity rules

1. No navigation properties in entity classes; use scalar foreign keys.
2. No EF mapping attributes in entities; mapping belongs in dedicated configurations.
3. Every entity has a dedicated `IEntityTypeConfiguration<TEntity>`.
4. Configure relationships without navigation properties.
5. Keep `ApplicationDbContext` thin and use `ApplyConfigurationsFromAssembly(...)`.
6. Use EF Core migrations for application schema; do not use `EnsureCreated` or init scripts for application tables.
7. Use established `Guid` PK, UTC timestamp, delete-behavior, and enum conventions.
8. Keep architecture/convention tests green.

Adding an Identity role or user-role membership does not by itself require a new EF migration because the Identity role tables already exist.

## Indexing rules

- Flow 1 persists source file + `Document`, then enqueues `Document.Id`.
- Worker performs indexing outside request actions.
- Re-indexing replaces stale chunks coherently.
- Success -> `Indexed`, set `IndexedAtUtc`, clear error.
- Failure -> `Failed`, persist bounded `IndexError`.
- Retrieval must use the same embedding model as indexing.
- Do not add an external broker without a concrete requirement.

## Reporting rules

- Reports read PostgreSQL persistence only.
- Use aggregate EF Core queries and `AsNoTracking()` where appropriate.
- Do not scan `storage/uploads/` for counts.
- Do not mutate workflow rows from reports.
- Chat metrics tolerate zero data until Flow 2 persists rows.
- Access is Admin-or-SubjectLeader through `ManageDocuments`.

## Infrastructure configuration

- `ConnectionStrings:Postgres`
- `Database:ApplyMigrationsOnStartup`
- `Auth:SeedUsers:Enabled`
- `Auth:SeedUsers:Admin:*`
- `Auth:SeedUsers:SubjectLeader:*`
- `Auth:SeedUsers:Student:*`
- `Rag:Ollama:BaseUrl`
- `Rag:Ollama:ChatModel`
- `Rag:Ollama:EmbeddingModel`
- `Rag:Storage:UploadsPath`

Default models:

- chat: `qwen3:4b`
- embedding: `qwen3-embedding:0.6b`

Do not add pgAdmin, Qdrant, Redis, RabbitMQ, Elasticsearch, RAGFlow, analytics warehouses, or other services without an explicit requirement.

## Standard commands

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant
dotnet run --project src/PRN222.RagAssistant
docker compose config
docker compose up -d --build
docker compose ps
docker compose logs app
docker compose down
```

Never run `docker compose down -v` unless explicitly requested.

## Git, docs, and file hygiene

- Remote default branch is `master`.
- Use focused feature branches/PRs.
- Do not recreate already merged indexing/reporting implementations.
- Do not recreate Flow 1 Razor Pages or create Flow 2 Razor Pages.
- Never commit `.env`, credentials, keys, DB dumps, logs, uploaded documents, build output, downloaded Ollama models, or runtime data.
- `storage/uploads/` is runtime data except `.gitkeep`.
- Preserve unrelated changes.
- **Member 1 is the sole documentation editor.** Other members include needed status/doc notes in their PR description or handoff to Member 1 rather than editing `README.md`, `AGENTS.md`, or `docs/` themselves.
- After a major merge, Member 1 synchronizes documentation against actual `master`.
