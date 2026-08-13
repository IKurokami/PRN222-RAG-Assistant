# Agent Instructions

## Scope

This file applies to the whole repository. If a deeper `AGENTS.md` exists, its instructions apply to that subtree in addition to this file.

Follow the user's explicit request when it changes the current project phase. Otherwise, preserve the merged architecture and ownership rules below and avoid expanding scope on your own.

Before changing workflow code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
```

For indexing work, also read `docs/member-2-document-management-handoff.md`.
For Flow 3 reporting work, also read `docs/flow-3-report-statistics-handoff.md`.

## Current project baseline

This repository is a PRN222 course RAG assistant with:

- Main project: `src/PRN222.RagAssistant`
- Test project: `tests/PRN222.RagAssistant.Tests`
- Target framework: `net10.0`
- Web stack: ASP.NET Core with MVC and Razor Pages enabled
- Authentication: ASP.NET Core Identity backed by EF Core/PostgreSQL
- Roles: `SubjectLeader` and `Student`
- Relational/vector database: PostgreSQL + pgvector through Docker Compose
- Local AI runtime: Ollama through Docker Compose
- Source-document storage: `storage/uploads/`
- Solution: `PRN222-RAG-Assistant.sln`

The product is scoped to one subject, PRN222. Course documents are selected and uploaded by the Subject Leader. Chapters are runtime-managed organizational data and must not be treated as a fixed seed-only list. Students consume indexed material through chat. Automatic FLM crawling is not an authoritative ingestion path.

The project defines three product workflows:

1. **Flow 1 - Document Management & Indexing**
2. **Flow 2 - RAG Question & Answer & Conversation Management**
3. **Flow 3 - Report & Statistics**

Conversation history is part of Flow 2 and must not be treated as the independent third workflow.

### Current merged milestone

The source baseline reviewed after PR #5 is:

- Member 1 Core/Data baseline: **merged/complete**
- Member 2 Document Management + Chapter Management: **merged/complete request side**
- Member 2 Flow 3 Report & Statistics: **planned/pending in a separate focused branch**
- Member 3 Document Indexing/Ingestion: **pending**
- Member 4 RAG/Chat backend: **pending**
- Member 5 Chat UI/Conversation Management/Evaluation: **pending**

Member 2's merged work includes runtime Chapter CRUD and Document list/upload/details/edit/removal/re-index request flows.

The current `InMemoryDocumentIndexingQueue` is a **temporary integration stub**. It exists so Member 2 can enqueue a persisted `Document.Id` through `IDocumentIndexingQueue`; it is not a completed indexing worker. Member 3 owns the real background worker, indexing service, parsing, chunking and embedding pipeline.

Flow 3 is intentionally read-only against existing persistence. It must not be used as a reason to redesign the data model, create speculative analytics tables, or modify Member 3/4/5 workflow behavior.

When status is unclear, treat the latest merged code on `master` as the source of truth and consult `docs/project-status.md`.

## Team ownership and integration boundaries

The team is split by workflow boundary. Do not reorganize the project into a different architecture or duplicate another member's responsibility without coordinating the change.

### Member 1 - Core/Data Lead - COMPLETE BASELINE

Owns the shared baseline:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- cross-workflow contracts under `Application/`
- schema/migration conventions and architecture tests
- shared infrastructure wiring when it affects multiple workflows

Member 1 is the default owner/coordinator for genuine EF Core schema changes and committed migrations. Other members may propose required persistence changes, but must not casually create competing migrations or redesign existing entities in isolation.

Do not move later workflow business logic into Member 1 simply because the baseline contracts live under `Application/`.

### Member 2 - Document Management + Report & Statistics

Member 2 owns two clearly separated responsibilities: the already merged request/presentation side of Flow 1 and the pending read-only Flow 3 reporting workflow.

#### Flow 1 request/presentation side - MERGED

Already merged:

- Razor Pages for document list/upload/details/edit/removal/re-index request
- Razor Pages for chapter list/create/edit/removal
- PDF/DOCX/PPTX upload validation
- 50 MB upload size limit
- configured source-file persistence
- creating/updating `Document` metadata
- creating/updating/removing PRN222 `Chapter` records through the existing model
- validating server-side that any selected `ChapterId` belongs to PRN222
- calling `IDocumentIndexingQueue` after a document is persisted
- server-side `AppPolicies.ManageDocuments` enforcement on write operations
- cleanup of a newly written upload if database persistence fails

Chapter Management is part of the merged document-management workflow. Subject Leaders must be able to create chapters without a code, seed, or migration change when the course outline changes.

Removing a chapter must preserve documents. Keep the existing restrictive `Document -> Chapter` relationship; when a referenced chapter is removed, the application layer must unassign affected documents (`ChapterId = null`) before removing the chapter. Never use cascade delete to make this flow easier.

Member 2 Flow 1 request handlers must not parse, chunk, embed, query pgvector, or call Ollama.

Do not create a second upload/chapter-management implementation in later member branches unless the team explicitly decides to replace the merged flow.

#### Flow 3 Report & Statistics - NEW / PENDING

Member 2 owns Flow 3 after synchronizing a separate reporting branch with the latest `master`.

Initial scope:

- Subject-Leader Reports/Statistics page
- total PRN222 chapters/documents
- document counts grouped by indexing state
- document counts grouped by chapter, including unassigned documents
- aggregate chat-session/message/citation counts from persisted Flow 2 data
- graceful zero/empty states while Members 3-5 are still pending

Flow 3 is read-only. Prefer aggregate/no-tracking EF Core queries over the existing persistence model.

Member 2 must not implement reporting by:

- modifying parsers, chunking, embedding, queues, workers, or index-state behavior owned by Member 3
- calling Ollama or implementing pgvector similarity retrieval owned by Member 4
- duplicating Member 5 chat/session/history UI
- mutating documents, chapters, indexing state, chat sessions, messages, or citations from report pages
- adding speculative analytics entities, custom event tracking, or migrations merely to produce counts

If reporting exposes a genuine persistence gap, document it and coordinate any schema/migration change through Member 1.

Keep Flow 3 in a focused branch such as `feature/report-statistics`. Do not mix Flow 1 fixes or unrelated architecture refactors into the reporting PR.

See `docs/flow-3-report-statistics-handoff.md` for detailed acceptance criteria.

### Member 3 - Document Indexing / Ingestion - NEXT / PENDING

Owns the background side of Flow 1:

- document parsers
- chunking
- final `IDocumentIndexingQueue` integration
- hosted/background indexing worker
- `IDocumentIndexingService` implementation
- `ITextEmbeddingService` implementation for Ollama embeddings
- replacing/persisting `DocumentChunk` rows
- document index-state transitions and indexing errors

Expected state flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-indexing must replace stale chunks coherently rather than append duplicate chunks.

Member 3 must integrate with the existing Member 2 handoff:

```text
Persisted Document
      |
      v
IDocumentIndexingQueue.EnqueueAsync(documentId)
      |
      v
Background worker
      |
      v
IDocumentIndexingService.IndexAsync(documentId)
```

Member 3 may replace `InMemoryDocumentIndexingQueue` and its DI registration with the real integration, but should preserve `IDocumentIndexingQueue` unless all affected consumers are updated together.

Do not place parsing/chunking/embedding work directly inside MVC/Razor request handlers. Member 3 does not own report/dashboard implementation.

### Member 4 - RAG / Chat Backend - PENDING

Owns Flow 2 retrieval and grounded answer generation:

- question embedding through `ITextEmbeddingService`
- pgvector similarity retrieval
- context construction and out-of-scope handling
- `IChatCompletionService` implementation for Ollama generation
- `IRagQueryService` implementation
- persistence of chat messages and `MessageCitation` records

Member 4 must validate chat-session ownership using the authenticated user ID supplied by the presentation layer. Controllers/PageModels must not execute pgvector/Ollama logic directly.

Retrieval should use successfully indexed PRN222 chunks as evidence.

Member 4 does not own aggregate reporting queries or report presentation.

### Member 5 - Chat UI / Conversation Management / Evaluation - PENDING

Owns Flow 2 presentation and the evaluation deliverable:

- chat UI and chat-session/history UI
- rendering citations returned by `IRagQueryService`
- presentation-side session creation/opening/navigation
- persisted conversation-history presentation
- `evaluation/` human-authored 50-question ground-truth set
- evaluation-facing tests/tools that do not redefine the RAG backend

Member 5 depends on `IRagQueryService`; browser/UI code must not call Ollama or query pgvector directly.

Conversation history is part of Flow 2. Member 5 does not own the Flow 3 Reports/Statistics pages.

### Shared-contract rule

Cross-member integration points live under `src/PRN222.RagAssistant/Application/`. The deeper `Application/AGENTS.md` documents those contracts.

Treat existing public signatures as stable. Prefer additive changes. If a shared contract must change, update all affected consumers/implementations together and update the coordination docs.

Flow 3 should not add a cross-member application contract unless a concrete implementation requirement justifies it.

## Repository layout

- `src/PRN222.RagAssistant/Application/`: cross-workflow abstractions and transport/result models
- `src/PRN222.RagAssistant/Domain/Entities/`: persistence/domain entity classes
- `src/PRN222.RagAssistant/Domain/Enums/`: domain enums
- `src/PRN222.RagAssistant/Data/`: EF Core/Identity DbContext, configurations, migrations, seed identifiers
- `src/PRN222.RagAssistant/Security/`: role and authorization-policy constants
- `src/PRN222.RagAssistant/Infrastructure/`: external systems, DI registration, database startup, Identity seeding
- `src/PRN222.RagAssistant/Infrastructure/Services/InMemoryDocumentIndexingQueue.cs`: temporary Member 2 queue stub awaiting Member 3 integration
- `src/PRN222.RagAssistant/Pages/Account/`: minimal authentication UI
- `src/PRN222.RagAssistant/Pages/Chapters/`: merged PRN222 Chapter Management UI
- `src/PRN222.RagAssistant/Pages/Documents/`: merged Document Management UI
- `tests/PRN222.RagAssistant.Tests/`: unit and architecture/convention tests
- `docs/`: architecture, project status, team workflow, and member handoff documentation
- `docs/flow-3-report-statistics-handoff.md`: Flow 3 ownership, boundaries, and acceptance criteria
- `evaluation/`: version-controlled evaluation sets and human-authored ground truth
- `infrastructure/postgres/init/`: PostgreSQL runtime initialization such as enabling extensions; application tables must not be created here
- `storage/uploads/`: runtime upload storage; never commit uploaded documents

## Mandatory EF Core entity rules

These rules are project-wide defaults unless the user explicitly changes them.

1. **Do not use navigation properties in entity classes.**
   - No reference navigation properties.
   - No collection navigation properties.
   - Store explicit scalar foreign keys instead.

2. **Do not put EF Core mapping inside entity classes.**
   - No EF mapping annotations such as `[Table]`, `[Column]`, `[ForeignKey]`, `[Index]`, or relationship annotations.
   - Validation annotations belong on request/input models when UI validation is needed.

3. **Every entity must have a dedicated configuration class.**
   - Implement `IEntityTypeConfiguration<TEntity>`.
   - Place it under `Data/Configurations/`.
   - Name it `<EntityName>Configuration`.
   - Configure tables, keys, lengths/types, required/optional fields, enum conversions, indexes, unique constraints, relationships, delete behavior, and EF seed data there.

4. **Configure relationships without navigation properties.**

```csharp
builder.HasOne<Subject>()
    .WithMany()
    .HasForeignKey(x => x.SubjectId)
    .OnDelete(DeleteBehavior.Restrict);
```

5. **Keep `ApplicationDbContext` thin.**
   - It may expose `DbSet<TEntity>` properties.
   - `OnModelCreating` must call `base.OnModelCreating(builder)`.
   - Load mappings with `ApplyConfigurationsFromAssembly(...)`.
   - Do not add entity-specific Fluent API mapping directly to the DbContext.

6. **Use EF Core migrations for application schema.**
   - Use the repository-local `dotnet-ef` tool.
   - Keep migrations under `Data/Migrations/`.
   - Do not create application tables in PostgreSQL init scripts.
   - Do not replace migrations with `EnsureCreated`.
   - Do not hand-edit migration/model-snapshot files unless repairing a known migration issue.
   - Keep one migration chain and synchronize with latest `master` before adding a migration.

7. **Use stable persistence conventions.**
   - Application entity primary keys use `Guid` unless a requirement says otherwise.
   - Persist timestamps as UTC and use the `Utc` suffix.
   - Persist domain enums as strings unless a concrete requirement justifies otherwise.
   - Configure relationship delete behavior explicitly.

8. **Convention tests must stay green.**
   - `EntityModelConventionsTests` rejects navigation properties and missing dedicated entity configurations.
   - `CoreDataArchitectureTests` protects important relationship, persistence, and authorization invariants.
   - Member 2 tests additionally protect Chapter/Document request-side behavior and authorization.

## Identity and authorization rules

- `ApplicationUser` extends `IdentityUser<Guid>` and must not add navigation properties.
- Application role names live in `Security/AppRoles.cs`; do not scatter role-name string literals.
- Authorization policy names live in `Security/AppPolicies.cs`.
- Document-management and chapter-management writes require `AppPolicies.ManageDocuments`, restricted to `AppRoles.SubjectLeader`.
- Flow 3 reports are Subject-Leader-facing and read-only. Do not make aggregate reporting public to Students without an explicit requirement.
- Hiding a management button is not authorization. Enforce authorization server-side.
- Do not add public role selection during registration. Users must never self-select `SubjectLeader`.
- Local demo-user seeding is configuration-driven and disabled by default. Never commit real credentials.

## Current domain model

The current model contains:

- `ApplicationUser`
- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Only PRN222 is seeded. The seeded subject establishes current course scope, but chapters are runtime-managed data. Do not invent chapter names/numbers from FLM without verified source data.

The model already contains the planned persistence for document index status/error/timestamps, chunk content/page/slide/embedding, chat timestamps, and message-to-chunk citations. Do not add speculative duplicate fields simply because later workflows are still pending.

Use these existing records as the first source for Flow 3 aggregate counts. Do not introduce an analytics schema solely for dashboard convenience.

## Infrastructure configuration

- `ConnectionStrings:Postgres` is the PostgreSQL configuration key; use `ConnectionStrings__Postgres` for environment overrides.
- `Database:ApplyMigrationsOnStartup` controls startup migration application.
- Identity roles are seeded after the database is available; `Auth:SeedUsers:Enabled` separately controls optional local demo users.
- The registered `NpgsqlDataSource` and EF Core provider both have pgvector support enabled.
- `Rag:Ollama:BaseUrl`, `Rag:Ollama:ChatModel`, and `Rag:Ollama:EmbeddingModel` describe the AI runtime.
- The named `Ollama` `HttpClient` is registered from `Rag:Ollama:BaseUrl`.
- `Rag:Storage:UploadsPath` describes source-document storage.
- `.env.example` documents local Docker Compose defaults. Never commit the real `.env` file.

Default local models are `qwen3:4b` for chat and `qwen3-embedding:0.6b` for embeddings. Treat model names as configuration. If the embedding model changes after documents have been indexed, affected documents must be re-indexed.

## Docker workflow

- Compose contains `app`, `postgres`, and `ollama` services.
- The app connects internally to PostgreSQL through `postgres` and to Ollama through `ollama`; host-run development uses `localhost`.
- Source documents are bind-mounted from `storage/uploads/` into the app container.
- Keep named PostgreSQL and Ollama volumes persistent unless the user explicitly requests a reset.
- Do not add pgAdmin, Qdrant, Redis, RabbitMQ, Elasticsearch, RAGFlow, or other services without an explicit requirement.
- Flow 3 must not add a separate analytics service/database for its initial scope.

## Dependencies and frontend assets

Frontend libraries are restored by LibMan. The source of truth is `libman.json`, and repository-local .NET CLI tools are declared in `dotnet-tools.json`.

Do not edit generated/downloaded frontend library files directly. Update `libman.json` and restore when versions/files change.

## Standard commands

Run from the repository root:

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

For ordinary source changes, run the relevant restore command(s), `dotnet build`, and targeted tests. When the EF model changes, add a migration in the same change and validate no pending model changes remain. Run `docker compose config` when Compose files change.

Do not run `docker compose down -v` or remove named PostgreSQL/Ollama volumes unless explicitly requested.

## Git and file hygiene

- Use the .NET CLI for scaffolding, solution/project changes, package changes, and EF Core migrations where applicable.
- The remote default branch is `origin/master`; `origin/main` does not exist.
- Use focused feature branches and pull requests.
- Do not have multiple members independently modify the same shared contract/schema file without coordination.
- Member 2 must implement Flow 3 on a separate reporting branch; do not mix it into Member 3/4/5 branches.
- Never commit `.env`, real credentials, private keys, database dumps, logs, uploaded documents, build output, downloaded Ollama models, or runtime data.
- Keep `.env.example`, `docker-compose.yml`, `README.md`, solution files, source, tests, docs, migrations, and `evaluation/` version-controlled.
- `storage/uploads/` is runtime data except for `.gitkeep`; processing/temp/chunk storage paths are also ignored.
- Preserve unrelated user changes.

Before handing off a change, report validation results and any remaining warnings or errors. After a major member workflow is merged, update `docs/project-status.md`, `docs/team-workflow.md`, relevant handoff docs, `README.md`, and these agent instructions if ownership/integration status changed.
