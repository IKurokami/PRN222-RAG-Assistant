# Agent Instructions

## Scope

This file applies to the whole repository. Deeper `AGENTS.md` files add rules for their subtree.

Follow explicit user requests when they change the current phase. Otherwise preserve the architecture/ownership below and do not expand scope on your own.

Before changing workflow code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
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
- Roles: `SubjectLeader`, `Student`
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
```

The demo is scoped to PRN222. Subject Leaders curate/upload course documents. Chapters are runtime-managed. Students consume successfully indexed content through Flow 2 chat. Automatic FLM crawling is not authoritative ingestion.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - MVC
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - MVC
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages

Conversation History belongs to Flow 2 and is not counted as a separate workflow.

## Current milestone

- Member 1 Core/Data: **complete**
- Member 2 Flow 1 Document/Chapter Management request side: **complete**, now MVC
- Member 3 Flow 1 Document Indexing/Ingestion: **complete / merged through PR #9**
- Member 2 Flow 3 Report & Statistics: **complete / merged through PR #12**
- Member 4 Flow 2 RAG backend: **pending**
- Member 5 Flow 2 MVC chat/history/citation presentation + evaluation: **pending**

When status is unclear, latest merged code on `master` is the source of truth; synchronize docs afterward. During an open migration PR, its branch docs describe the intended post-merge state.

## Team ownership and integration boundaries

### Member 1 - Core/Data Lead

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- cross-workflow `Application/` contracts/models
- schema/migration conventions
- architecture/convention tests
- coordination for genuine EF model changes

Do not move workflow business logic into Member 1 simply because shared contracts live under `Application/`.

### Member 2 - Flow 1 Document Management + Flow 3 Reporting

#### Flow 1 request/presentation - COMPLETE / MVC

Current locations:

```text
src/PRN222.RagAssistant/Controllers/DocumentsController.cs
src/PRN222.RagAssistant/Controllers/ChaptersController.cs
src/PRN222.RagAssistant/Models/Documents/
src/PRN222.RagAssistant/Models/Chapters/
src/PRN222.RagAssistant/Views/Documents/
src/PRN222.RagAssistant/Views/Chapters/
```

The previous `Pages/Documents/` and `Pages/Chapters/` Flow 1 implementation has been removed. **Do not recreate a parallel Razor Pages implementation.**

Flow 1 behavior includes:

- runtime PRN222 Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index request
- PDF/DOCX/PPTX upload validation
- 50 MB limit
- source-file persistence
- document metadata persistence
- PRN222 chapter validation
- `AppPolicies.ManageDocuments` enforcement on writes
- anti-forgery validation on POST actions
- queue handoff after document persistence
- safe chapter deletion that unassigns referenced documents first

Flow 1 controllers must not parse, chunk, embed, query pgvector, or call Ollama directly. Upload/re-index hands off through `IDocumentIndexingQueue`.

See `docs/flow-1-mvc-migration.md` and `docs/member-2-document-management-handoff.md`.

#### Flow 3 Report & Statistics - COMPLETE / Razor Pages

Flow 3 remains under `Pages/Reports/`.

It includes read-only Subject-Leader metrics for chapters/documents, indexing state, chunks, recent indexing failures/successes, and chat session/message/citation totals.

Flow 3 must not:

- enqueue/re-index documents as part of reporting
- alter parser/chunker/embedding/worker behavior
- mutate workflow data
- run pgvector similarity retrieval
- call Ollama
- duplicate Conversation History UI
- add speculative analytics entities/migrations
- change shared contracts solely for dashboard convenience

### Member 3 - Document Indexing / Ingestion - COMPLETE

Merged indexing responsibilities:

- `DocumentParserFactory`
- PDF parser via PdfPig
- DOCX/PPTX parsers via OpenXml
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- coherent `DocumentChunk` replacement
- indexing state/error/timestamp persistence
- startup rehydration of persisted `Uploaded`/`Processing` documents

State flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

`InMemoryDocumentIndexingQueue` is process-local, not a durable broker. Recovery is based on persisted document state.

Do not create a second indexing pipeline.

### Member 4 - RAG / Chat Backend - PENDING

Owns:

- question embedding through `ITextEmbeddingService.EmbedAsync`
- pgvector similarity retrieval over successfully indexed PRN222 chunks
- top-K context selection
- grounded prompt construction
- explicit no-evidence/out-of-scope behavior
- `IChatCompletionService`
- `IRagQueryService`
- chat-session ownership validation
- user/assistant message persistence
- ordered `MessageCitation` persistence

Member 4 must remain presentation-agnostic and must not depend on MVC `Controller`, Razor `PageModel`, `HttpContext`, or browser-specific DTOs.

Member 4 must not parse raw source files, duplicate indexing, recreate Flow 3 reporting, or place provider/retrieval logic directly in controllers.

### Member 5 - Flow 2 MVC Presentation / Conversation Management / Evaluation - PENDING

Owns:

- focused MVC chat controller/actions
- MVC chat/session views
- chat-session creation/opening/navigation presentation
- Conversation History presentation
- citation/source rendering
- consumption of `IRagQueryService`
- human-authored 50-question evaluation set
- evaluation-facing tooling/tests

Expected locations:

```text
src/PRN222.RagAssistant/Controllers/ChatController.cs
src/PRN222.RagAssistant/Views/Chat/
```

Supporting shared MVC view files may be reused where appropriate.

**Do not implement Flow 2 under `Pages/Chat`, `Pages/Conversation`, or another Razor Pages folder.**

Because Flow 1 already uses MVC, do not mix Flow 2 responsibilities into `DocumentsController` or `ChaptersController`.

MVC chat controllers must stay thin and delegate grounded Q&A to `IRagQueryService`. They must not call Ollama or query pgvector directly.

## Presentation model rules

The mixed host is intentional:

- **Flow 1:** MVC Controllers + Views - complete.
- **Flow 2:** MVC Controllers + Views - required for implementation.
- **Flow 3:** Razor Pages - complete.
- Authentication/shell pages may remain Razor Pages.

`Program.cs` registers/maps both MVC and Razor Pages.

For MVC workflow code:

- use controller actions as HTTP entry points;
- render views under `Views/`;
- keep request/view models out of domain entities;
- enforce authorization server-side;
- use anti-forgery protection for state-changing form posts;
- keep provider/retrieval/indexing business logic behind service boundaries.

## Shared-contract rules

Cross-member integration points live under `src/PRN222.RagAssistant/Application/`.

Current contracts/models:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

`ITextEmbeddingService` supports single-text and ordered batch embedding. Indexing and retrieval must use the same configured embedding model.

Treat public signatures as stable. Prefer additive changes. If a contract must change, update affected producers/consumers together and synchronize docs.

## Repository layout

- `Application/`: shared abstractions/models
- `Domain/Entities/`: persistence entities
- `Domain/Enums/`: domain enums
- `Data/`: DbContext/configurations/migrations/seed IDs
- `Security/`: role/policy constants
- `Infrastructure/Parsing/`: parsers/chunker
- `Infrastructure/Services/`: queue/indexing/Ollama services
- `Controllers/DocumentsController.cs`: Flow 1 document MVC request side
- `Controllers/ChaptersController.cs`: Flow 1 chapter MVC request side
- `Models/Documents/`, `Models/Chapters/`: Flow 1 MVC view/input models
- `Views/Documents/`, `Views/Chapters/`: Flow 1 MVC views
- `Pages/Account/`: auth Razor Pages
- `Pages/Reports/`: Flow 3 Reports/Statistics Razor Pages
- `Controllers/` + `Views/Chat/`: shared MVC root / pending Flow 2 presentation
- `tests/PRN222.RagAssistant.Tests/`: tests
- `docs/`: status/architecture/ownership/handoffs
- `evaluation/`: evaluation set
- `infrastructure/postgres/init/`: pgvector/runtime DB initialization
- `storage/uploads/`: runtime source documents; never commit uploads

## Mandatory EF Core entity rules

1. **No navigation properties in entity classes.** Use scalar foreign keys.
2. **No EF mapping attributes in entities.** Mapping belongs in dedicated configuration classes; validation annotations belong on request/input models when needed.
3. **Every entity has a dedicated `IEntityTypeConfiguration<TEntity>`.**
4. **Configure relationships without navigation properties.**

```csharp
builder.HasOne<Subject>()
    .WithMany()
    .HasForeignKey(x => x.SubjectId)
    .OnDelete(DeleteBehavior.Restrict);
```

5. **Keep `ApplicationDbContext` thin.** Use `ApplyConfigurationsFromAssembly(...)`.
6. **Use EF Core migrations for application schema.** Do not create application tables in PostgreSQL init scripts or use `EnsureCreated`.
7. Use stable conventions: `Guid` PKs unless required otherwise, UTC timestamps with `Utc` suffix, explicit delete behavior, established enum persistence.
8. Keep architecture/convention tests green.

A presentation-only MVC migration does **not** justify an EF Core migration.

## Identity and authorization rules

- `ApplicationUser` extends `IdentityUser<Guid>` and must not gain navigation properties.
- Role names live in `Security/AppRoles.cs`.
- Policy names live in `Security/AppPolicies.cs`.
- Document/Chapter writes require `AppPolicies.ManageDocuments` and `SubjectLeader`.
- Flow 1 POST write actions use anti-forgery validation.
- Flow 3 Reports require `AppPolicies.ManageDocuments` and remain read-only.
- Flow 2 must validate authenticated user/session ownership server-side.
- Hiding UI is not authorization.
- Users must never self-select `SubjectLeader` through public registration.
- Demo-user seeding is config-driven and disabled by default.
- Never commit real credentials.

## Current domain model

- `ApplicationUser`
- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Only PRN222 subject scope is seeded. Chapters are runtime-managed and must not be invented from FLM in migrations/code without a verified requirement.

## Indexing rules

- Flow 1 persists source file + `Document`, then enqueues `Document.Id`.
- Worker performs indexing outside request actions.
- Re-indexing replaces stale chunks coherently.
- On success: `Indexed`, set `IndexedAtUtc`, clear error.
- On failure: `Failed`, persist bounded `IndexError`.
- Retrieval must use the same embedding model as indexing.
- If embedding model changes, re-index affected documents.
- Do not introduce an external broker without a concrete requirement.

## Reporting rules

- Reports read existing PostgreSQL persistence only.
- Use aggregate EF Core queries and `AsNoTracking()` where appropriate.
- Do not scan `storage/uploads/` for counts.
- Do not mutate workflow rows from reports.
- Chat metrics must tolerate zero data until Flow 2 persists chat rows.
- Preserve Subject-Leader-only authorization.

## Infrastructure configuration

- `ConnectionStrings:Postgres`
- `Database:ApplyMigrationsOnStartup`
- `Auth:SeedUsers:Enabled`
- `Rag:Ollama:BaseUrl`
- `Rag:Ollama:ChatModel`
- `Rag:Ollama:EmbeddingModel`
- `Rag:Storage:UploadsPath`

Default local models:

- chat: `qwen3:4b`
- embedding: `qwen3-embedding:0.6b`

Compose services remain `app`, `postgres`, and `ollama`.

Do not add pgAdmin, Qdrant, Redis, RabbitMQ, Elasticsearch, RAGFlow, analytics warehouses, or other services without an explicit requirement.

## Dependencies and frontend assets

Frontend libraries are restored by LibMan; `libman.json` is the source of truth. Repository-local .NET CLI tools are declared in `dotnet-tools.json`.

Do not directly edit generated/downloaded frontend library files.

## Standard commands

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations add <MigrationName> --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant
dotnet ef database update --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant
dotnet run --project src/PRN222.RagAssistant
docker compose config
docker compose up -d --build
docker compose ps
docker compose logs app
docker compose exec ollama ollama list
docker compose down
```

Do not run `docker compose down -v` or remove named volumes unless explicitly requested.

## Git and file hygiene

- Remote default branch is `master`.
- Use focused feature branches/PRs.
- Do not let multiple members independently modify the same shared schema/contract without coordination.
- Do not recreate already merged indexing/reporting implementations.
- Do not recreate Flow 1 Razor Pages or create Flow 2 Razor Pages.
- Never commit `.env`, credentials, keys, DB dumps, logs, uploaded documents, build output, downloaded Ollama models, or runtime data.
- `storage/uploads/` is runtime data except `.gitkeep`; temp/processed/chunks paths remain ignored.
- Preserve unrelated changes.

After a major workflow merge, update `docs/project-status.md`, `docs/team-workflow.md`, relevant handoff docs, `README.md`, and agent instructions so later members work from the actual merged baseline.
