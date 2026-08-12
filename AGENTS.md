# Agent Instructions

## Scope

This file applies to the whole repository. If a deeper `AGENTS.md` exists, its instructions apply to that subtree in addition to this file.

Follow the user's explicit request when it changes the current project phase. Otherwise, preserve the baseline rules below and avoid expanding scope on your own.

## Project baseline

This repository is the application/data/infrastructure baseline for a PRN222 course RAG assistant:

- Main project: `src/PRN222.RagAssistant`
- Test project: `tests/PRN222.RagAssistant.Tests`
- Target framework: `net10.0`
- Web stack: ASP.NET Core with both MVC controllers/views and Razor Pages enabled in the same application
- Authentication: ASP.NET Core Identity backed by EF Core/PostgreSQL
- Roles: `SubjectLeader` and `Student`
- Relational/vector database: PostgreSQL + pgvector through Docker Compose
- Local AI runtime: Ollama through Docker Compose
- Source-document storage: `storage/uploads/`
- Solution: `PRN222-RAG-Assistant.sln`

The product is scoped to one subject, PRN222. Course documents are selected and uploaded by the Subject Leader. Subject Leaders may organize PRN222 documents into user-managed chapters; chapter records must not be treated as fixed seed-only data. Students consume indexed material through chat. The application must not treat automatic FLM crawling as an authoritative ingestion path.

The current baseline includes authentication, authorization, EF Core domain persistence, infrastructure wiring, and shared application contracts for document indexing and RAG integration. Upload/parsing/chunking/indexing implementations, retrieval, grounded prompting, and chat UI are feature work owned by later workflow members.

## Team ownership and integration boundaries

The team is intentionally split by workflow boundary. Do not reorganize the project into a different architecture or duplicate another member's responsibility without coordinating the change.

### Member 1 - Core/Data Lead

Owns the shared baseline:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- cross-workflow contracts under `Application/`
- schema/migration conventions and architecture tests
- shared infrastructure wiring when it affects multiple workflows

Member 1 is the default owner for EF Core schema changes and committed migrations. Other members may propose required persistence changes, but must not casually create competing migrations or redesign existing entities in isolation.

### Member 2 - Document Management

Owns the document-management presentation/application workflow:

- MVC/Razor Pages for document list/upload/details/delete/re-index
- MVC/Razor Pages for chapter list/create/edit/delete
- upload validation and source-file persistence
- creating/updating `Document` metadata
- creating/updating/deleting PRN222 `Chapter` records through the existing model
- validating server-side that any selected `ChapterId` belongs to PRN222
- calling `IDocumentIndexingQueue` after a document has been persisted

Chapter management is part of the document-management workflow, not a seed-data-only concern. Subject Leaders must be able to create chapters without requiring a code, seed, or migration change when the course outline changes.

Deleting a chapter must not delete documents. Keep the existing `Document -> Chapter` relationship protected by `DeleteBehavior.Restrict`; when a Subject Leader explicitly deletes a chapter that is referenced by documents, the application layer must first set those documents' nullable `ChapterId` values to `null` and then delete the chapter in one coherent transaction. Do not silently change the FK to cascading delete or rely on database-side implicit unlinking.

Member 2 must not parse, chunk, embed, or call Ollama inside upload handlers. Document-management and chapter-management write endpoints must enforce `AppPolicies.ManageDocuments` server-side.

### Member 3 - Document Indexing / Ingestion

Owns indexing implementation:

- document parsers
- chunking
- `IDocumentIndexingQueue` implementation and hosted worker
- `IDocumentIndexingService` implementation
- `ITextEmbeddingService` implementation for Ollama embeddings
- replacing/persisting `DocumentChunk` rows
- document index-state transitions and indexing errors

Member 3 must not place indexing work directly inside MVC/Razor request handlers.

### Member 4 - RAG / Chat Backend

Owns retrieval and grounded answer generation:

- question embedding through `ITextEmbeddingService`
- pgvector similarity retrieval
- context construction and out-of-scope handling
- `IChatCompletionService` implementation for Ollama generation
- `IRagQueryService` implementation
- persistence of chat messages and `MessageCitation` records

Member 4 must validate chat-session ownership using the authenticated user ID supplied by the presentation layer. Controllers/PageModels must not execute pgvector/Ollama logic directly.

### Member 5 - Chat UI / History / Evaluation

Owns chat/history presentation and the evaluation deliverable:

- chat UI and chat-session/history UI
- rendering citations returned by `IRagQueryService`
- presentation-side session creation/opening/navigation
- `evaluation/` human-authored 50-question ground-truth set
- evaluation-facing tests/tools that do not redefine the RAG backend

Member 5 depends on `IRagQueryService`; browser/UI code must not call Ollama or query pgvector directly.

### Shared-contract rule

Cross-member integration points live under `src/PRN222.RagAssistant/Application/`. A deeper `Application/AGENTS.md` documents those contracts. Treat existing public signatures as stable after this baseline lands. Prefer additive changes. If a shared contract must change, update all affected consumers/implementations together and document the change.

See `docs/team-workflow.md` and `docs/member-1-core-data-handoff.md` before starting Member 2-5 work.

## Repository layout

- `src/PRN222.RagAssistant/Application/`: cross-workflow abstractions and transport/result models
- `src/PRN222.RagAssistant/Domain/Entities/`: persistence/domain entity classes
- `src/PRN222.RagAssistant/Domain/Enums/`: domain enums
- `src/PRN222.RagAssistant/Data/ApplicationDbContext.cs`: EF Core/Identity DbContext
- `src/PRN222.RagAssistant/Data/Configurations/`: one EF Core configuration class per entity
- `src/PRN222.RagAssistant/Data/Migrations/`: one committed migration chain for the application schema
- `src/PRN222.RagAssistant/Data/Seed/`: deterministic seed identifiers/data used by EF configuration
- `src/PRN222.RagAssistant/Security/`: role and authorization-policy constants
- `src/PRN222.RagAssistant/Infrastructure/`: external systems, DI registration, database startup, Identity seeding
- `src/PRN222.RagAssistant/Pages/Account/`: minimal authentication UI
- `tests/PRN222.RagAssistant.Tests/`: unit and architecture/convention tests
- `docs/`: architecture, team workflow, and handoff documentation
- `evaluation/`: version-controlled evaluation sets and human-authored ground truth
- `infrastructure/postgres/init/`: PostgreSQL runtime initialization such as enabling extensions; application tables must not be created here
- `storage/uploads/`: runtime upload storage; never commit uploaded documents

## Mandatory EF Core entity rules

These rules are project-wide defaults for every current and future entity unless the user explicitly changes them.

1. **Do not use navigation properties in entity classes.**
   - No reference navigation such as `public Subject Subject { get; set; }`.
   - No collection navigation such as `public ICollection<Document> Documents { get; set; }`.
   - Store explicit scalar foreign keys instead, for example `SubjectId`, `DocumentId`, or nullable `ChapterId`.

2. **Do not put EF Core mapping inside entity classes.**
   - Do not use EF mapping data annotations such as `[Table]`, `[Column]`, `[ForeignKey]`, `[Index]`, or relationship annotations on entities.
   - Keep entity classes focused on state only.
   - Validation annotations belong on request/input models when UI validation is needed, not on persistence entities as a substitute for EF configuration.

3. **Every entity must have a dedicated configuration class.**
   - Implement `IEntityTypeConfiguration<TEntity>`.
   - Place it under `Data/Configurations/`.
   - Name it `<EntityName>Configuration`.
   - Configure table names, keys, property lengths/types, required/optional fields, enum conversions, indexes, unique constraints, relationships, delete behavior, and EF seed data there.

4. **Configure relationships without navigation properties.**
   - Use patterns such as:

```csharp
builder.HasOne<Subject>()
    .WithMany()
    .HasForeignKey(x => x.SubjectId)
    .OnDelete(DeleteBehavior.Restrict);
```

5. **Keep `ApplicationDbContext` thin.**
   - It may expose `DbSet<TEntity>` properties.
   - `OnModelCreating` must call `base.OnModelCreating(builder)` because Identity depends on it.
   - Then load entity configurations with `ApplyConfigurationsFromAssembly(...)`.
   - Do not add entity-specific Fluent API mappings directly to `ApplicationDbContext`.

6. **Use EF Core migrations for application schema.**
   - Generate migrations with the repository-local `dotnet-ef` tool.
   - Keep migrations under `Data/Migrations/`.
   - Do not create application tables in PostgreSQL init scripts.
   - Do not replace migrations with `EnsureCreated`.
   - Do not hand-edit migration/model-snapshot files unless explicitly repairing a known migration issue.
   - Keep a single migration chain. Before generating a migration, synchronize with the latest integration branch and confirm the model change is genuinely required.

7. **Use explicit, stable persistence conventions.**
   - Application entity primary keys use `Guid` unless a specific requirement says otherwise.
   - Persist timestamps as UTC and name them with the `Utc` suffix.
   - Persist domain enums as strings unless a specific storage/performance requirement justifies otherwise.
   - Configure delete behavior explicitly for relationships rather than relying on accidental conventions.

8. **Convention tests are required to stay green.**
   - `EntityModelConventionsTests` rejects navigation properties in `Domain/Entities`.
   - It also rejects new entities that do not have a dedicated `IEntityTypeConfiguration<TEntity>`.
   - `CoreDataArchitectureTests` protects important relationship, persistence, and authorization invariants.
   - When adding an entity, add its configuration in the same change.

## Identity and authorization rules

- `ApplicationUser` extends `IdentityUser<Guid>` and must not add navigation properties.
- Application role names live in `Security/AppRoles.cs`; do not scatter role-name string literals through controllers/pages/services.
- Authorization policy names live in `Security/AppPolicies.cs`.
- Document-management write operations, including chapter create/edit/delete operations, must require `AppPolicies.ManageDocuments`, which is restricted to `AppRoles.SubjectLeader`.
- Hiding an Upload/Delete/Re-index/Create Chapter/Edit Chapter/Delete Chapter button is not authorization. Enforce authorization server-side on MVC actions/Razor Pages/handlers.
- Do not add public role selection during registration. A user must never be able to self-select `SubjectLeader`.
- Local demo-user seeding is configuration-driven and disabled by default. Never commit real credentials.

## Current domain model

The baseline currently contains:

- `ApplicationUser`
- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Only PRN222 is seeded. The seeded subject establishes the current course scope, but chapters are runtime-managed data and must not be limited to seed data. Do not invent chapter names/numbers from FLM without verified source data.

The current model already contains the persistence needed for the planned workflows, including document index status/error/timestamps, chunk content/page/slide/embedding, chat timestamps, and message-to-chunk citations. Do not add speculative duplicate fields simply because a later workflow has not yet been implemented.

## Infrastructure configuration

- `ConnectionStrings:Postgres` is the application-level PostgreSQL configuration key; use `ConnectionStrings__Postgres` for environment overrides.
- `Database:ApplyMigrationsOnStartup` controls startup migration application.
- Identity roles are seeded at startup after the database is available; `Auth:SeedUsers:Enabled` separately controls optional local demo users.
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
- Keep named PostgreSQL and Ollama data volumes persistent unless the user explicitly requests a reset.
- Do not add pgAdmin, Qdrant, Redis, RabbitMQ, Elasticsearch, RAGFlow, or other services without an explicit requirement.

## Dependencies and frontend assets

Frontend libraries are restored by LibMan. The source of truth is `libman.json`, and repository-local .NET CLI tools are declared in `dotnet-tools.json`.

Do not edit downloaded files directly under these generated directories:

- `src/PRN222.RagAssistant/wwwroot/lib/bootstrap/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery-validation/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery-validation-unobtrusive/dist/`

Update `libman.json` and run LibMan restore when frontend library versions or files need to change.

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

For ordinary source changes, run the relevant restore command(s), `dotnet build`, and targeted tests. When the EF model changes, add a migration in the same change and validate that the model has no pending changes. Run `docker compose config` when Compose files change.

Do not run `docker compose down -v` or remove named PostgreSQL/Ollama volumes unless explicitly requested.

## Git and file hygiene

- Use the .NET CLI for scaffolding, solution/project changes, references, package changes, and EF Core migrations where applicable.
- Do not create a remote, push, or alter remote configuration unless explicitly requested.
- The remote default branch is `origin/master`; `origin/main` does not exist.
- Use focused feature branches and pull requests. Do not have multiple members independently modify the same shared contract/schema file without coordination.
- Never commit `.env`, real credentials, private keys, database dumps, logs, uploaded documents, build output, downloaded Ollama models, or other runtime data.
- Keep `.env.example`, `docker-compose.yml`, `README.md`, solution files, source, tests, docs, migrations, and `evaluation/` version-controlled.
- `bin/`, `obj/`, and LibMan-generated `wwwroot/lib/*/dist/` directories are ignored by design.
- Preserve unrelated user changes.

Before handing off a change, report validation results and any remaining warnings or errors. Do not silently broaden the task beyond the user's request.
