# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core, EF Core, PostgreSQL/pgvector, and Ollama for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are curated and uploaded by the Subject Leader and become the chatbot's authoritative knowledge source after indexing. Chapter organization is managed at runtime by the Subject Leader instead of being fixed in seed data.

See `docs/project-status.md` for the current whole-project milestone, `docs/team-workflow.md` for member ownership, and `docs/infrastructure.md` for infrastructure decisions and the intended RAG flow.

## Current project status

Current integration baseline after Member 2's Document Management work was merged into `master`:

| Member | Scope | Status |
| --- | --- | --- |
| Member 1 | Core/Data, Identity, authorization, EF Core model/migrations, shared contracts | Complete |
| Member 2 | Document Management + runtime Chapter Management | Complete and merged |
| Member 3 | Document parsing, chunking, embeddings, indexing queue/worker | Next / pending |
| Member 4 | pgvector retrieval, grounded RAG backend, chat/citation persistence | Pending |
| Member 5 | Chat UI, conversation history, citation rendering, evaluation set | Pending |

The repository currently provides:

- ASP.NET Core application with both MVC controllers/views and Razor Pages enabled
- ASP.NET Core Identity backed by EF Core/PostgreSQL
- `SubjectLeader` and `Student` roles
- `ManageDocuments` authorization policy restricted to `SubjectLeader`
- EF Core domain model for PRN222 subjects/chapters/documents/chunks and chat sessions/messages/citations
- PostgreSQL + pgvector
- Ollama local model runtime
- persistent source-document storage under `storage/uploads/`
- shared application contracts for document management -> indexing -> RAG -> presentation integration
- runtime PRN222 Chapter list/create/edit/delete for Subject Leaders
- document upload/list/details/edit/delete/re-index request workflow
- PDF/DOCX/PPTX upload validation with a 50 MB limit
- server-side validation that selected chapters belong to PRN222
- architecture, authorization, Chapter Management, and Document Management tests
- GitHub Actions build/test/EF-migration/Compose validation

### Implemented Document Management flow

Member 2's merged request-side Flow 1 now follows this boundary:

```text
Subject Leader
    |
    +--> Manage PRN222 Chapters at runtime
    |       +--> list
    |       +--> create
    |       +--> edit
    |       \--> delete
    |              \--> linked Document.ChapterId values are set to null first
    |
    \--> Upload / manage documents
            |
            +--> validate PDF / DOCX / PPTX and size
            +--> validate optional ChapterId belongs to PRN222
            +--> persist source file under storage/uploads/
            +--> persist Document with IndexStatus = Uploaded
            \--> IDocumentIndexingQueue.EnqueueAsync(document.Id)
```

Document and Chapter management request handlers must not parse, chunk, embed, or call Ollama directly.

### Current Member 3 handoff

`InMemoryDocumentIndexingQueue` is currently registered only as a **temporary integration stub** so the merged Member 2 upload/re-index workflow has a working `IDocumentIndexingQueue` implementation.

Member 3 owns the next integration step:

```text
IDocumentIndexingQueue
        |
        v
Hosted/background worker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
        |
        +--> parse PDF / DOCX / PPTX
        +--> chunk extracted text
        +--> ITextEmbeddingService -> Ollama
        +--> replace/persist DocumentChunk rows
        \--> update Document indexing state
```

Expected state transitions:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

When Member 3 lands the real indexing implementation, replace the temporary queue stub/DI registration rather than creating a second competing queue contract or moving indexing work into Razor Page handlers.

## Team development boundaries

The project is split by workflow so multiple members can work without duplicating or conflicting with each other:

- **Member 1 - Core/Data Lead:** completed domain entities/enums, EF Core configurations/migrations, security policies, cross-workflow `Application/` contracts, architecture tests, and shared integration rules.
- **Member 2 - Document Management:** completed document upload/list/details/edit/delete/re-index plus PRN222 Chapter list/create/edit/delete. Persists a `Document`, then hands the persisted `Document.Id` to `IDocumentIndexingQueue`.
- **Member 3 - Document Indexing:** owns parsers, chunking, the real indexing queue/worker, embeddings, `DocumentChunk` persistence, and document indexing status/error transitions.
- **Member 4 - RAG Backend:** owns question embedding, pgvector retrieval, grounded prompts, Ollama chat generation, chat/citation persistence, and `IRagQueryService` implementation.
- **Member 5 - Chat UI / History / Evaluation:** owns chat/history presentation, citation rendering, and the human-authored 50-question ground-truth evaluation set.

Before implementing the next member workflow, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
```

The cross-member contracts under `src/PRN222.RagAssistant/Application/` are intentional integration boundaries. Do not duplicate those interfaces in feature folders or bypass them by calling Ollama/pgvector directly from MVC controllers or Razor Page models.

### Migration ownership

The repository keeps one EF Core migration chain. Member 1 is the default schema/migration coordinator. A later member who discovers a genuine persistence requirement should first explain the persistence gap, update the model/configuration coherently, synchronize with the latest integration branch, generate one migration, and run the pending-model check. Do not create speculative fields or parallel competing migrations.

Runtime Chapter CRUD does not require a migration because the existing model already supports it through `Chapter` and nullable `Document.ChapterId`.

## Local setup

Copy the example environment file when you want to override Docker Compose defaults:

```text
cp .env.example .env
```

On Windows Command Prompt you can use:

```text
copy .env.example .env
```

Restore .NET and frontend dependencies:

```text
dotnet tool restore
dotnet libman restore
dotnet restore
```

Build and test:

```text
dotnet build
dotnet test
```

Start Docker Compose:

```text
docker compose up -d --build
```

Compose starts:

- ASP.NET Core application
- PostgreSQL with pgvector support
- Ollama local model runtime
- persistent PostgreSQL and Ollama volumes
- bind-mounted `storage/uploads/` for source documents

The application applies committed EF Core migrations on startup when `DATABASE_APPLY_MIGRATIONS_ON_STARTUP=true`.

Check containers:

```text
docker compose ps
```

Stop Docker Compose:

```text
docker compose down
```

Do not use `docker compose down -v` unless you intentionally want to delete the PostgreSQL and Ollama data volumes.

## Authentication and demo accounts

The app uses ASP.NET Core Identity with two application roles:

- `SubjectLeader` - the only role allowed by the `ManageDocuments` policy
- `Student` - normal learner access

The two roles are ensured at application startup after the database schema is available. Demo-user seeding is disabled by default. To create the local demo users documented in `.env.example`, copy the file to `.env` and set:

```text
AUTH_SEED_USERS=true
```

Change the example passwords before using the accounts. The default example identities are:

```text
leader@prn222.local
student@prn222.local
```

Sign in at:

```text
http://localhost:8080/Account/Login
```

The app does not expose public role selection, so a user cannot self-assign the `SubjectLeader` role.

## Document and Chapter Management

Subject Leaders can manage the current Flow 1 request-side functionality through Razor Pages under:

```text
Pages/Chapters/
Pages/Documents/
```

Chapter records are **runtime-managed application data**. They are not restricted to fixed seed values. A Subject Leader can add a new PRN222 chapter when the course outline changes without modifying source code or creating a migration.

When deleting a Chapter that is referenced by documents, the application keeps the restrictive database relationship and explicitly unlinks affected documents by setting `Document.ChapterId` to `null` before deleting the Chapter. Documents are never cascade-deleted as a side effect of Chapter deletion.

Uploaded documents are persisted under the configured `Rag:Storage:UploadsPath`, which defaults to `storage/uploads/` for local development. Runtime uploaded files are intentionally ignored by Git; only `storage/uploads/.gitkeep` is version-controlled.

## EF Core model and migrations

Application persistence uses `ApplicationDbContext`, which also hosts the ASP.NET Core Identity schema.

The project intentionally uses **no navigation properties in entity classes**. Entities store scalar foreign-key IDs, while relationships and all other EF mappings live in dedicated `IEntityTypeConfiguration<TEntity>` classes under:

```text
src/PRN222.RagAssistant/Data/Configurations/
```

`ApplicationDbContext.OnModelCreating` only invokes Identity's base configuration and scans the assembly for these configuration classes.

When the EF model changes, generate a migration with the repository-local tool:

```text
dotnet tool restore
dotnet ef migrations add <MigrationName> \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant \
  --output-dir Data/Migrations
```

Verify that the model is fully represented by committed migrations:

```text
dotnet ef migrations has-pending-model-changes \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant
```

Apply migrations manually when needed:

```text
dotnet ef database update \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant
```

Do not use PostgreSQL init scripts to create application tables. They are only for database-runtime concerns such as enabling the `vector` extension.

## PRN222 seed data

Only the PRN222 subject identity/scope is seeded as the authoritative course baseline:

```text
Code: PRN222
```

Chapters are intentionally **not seed-only data**. Their names and numbers are created and maintained by the Subject Leader at runtime. Do not invent chapter names/numbers from FLM in code or migrations without a verified requirement.

## Ollama models

Pull the default local AI models after Ollama is running:

```text
docker compose exec ollama ollama pull qwen3:4b
docker compose exec ollama ollama pull qwen3-embedding:0.6b
```

List installed Ollama models:

```text
docker compose exec ollama ollama list
```

If you change `OLLAMA_CHAT_MODEL` or `OLLAMA_EMBEDDING_MODEL` in `.env`, pull those model names instead. If the embedding model changes after documents have been indexed, affected documents must be re-indexed so indexing and retrieval use compatible vectors.

## Verify pgvector

```text
docker compose exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT extversion FROM pg_extension WHERE extname = '\''vector'\'';"'
```

The init script enables pgvector automatically when PostgreSQL creates a new database volume.

## Run the application directly

Start PostgreSQL and Ollama with Compose, then run the web application on the host:

```text
dotnet run --project src/PRN222.RagAssistant
```

`appsettings.Development.json` points the host-run application at `localhost:5432` and `localhost:11434` and enables migration-on-startup for local development.

## Environment configuration

`.env.example` documents local defaults for:

- application/runtime ports
- PostgreSQL and pgvector
- EF migration-on-startup
- optional Identity demo users
- Ollama runtime and model names
- uploaded-document storage

The `.env` file is intentionally ignored by Git. Keep real credentials and machine-specific values there.

ASP.NET Core settings use standard double-underscore environment-variable keys in Compose, including:

```text
ConnectionStrings__Postgres
Database__ApplyMigrationsOnStartup
Auth__SeedUsers__Enabled
Rag__Ollama__BaseUrl
Rag__Ollama__ChatModel
Rag__Ollama__EmbeddingModel
Rag__Storage__UploadsPath
```

For project-wide coding conventions, especially EF Core entity/configuration rules and team ownership boundaries, see `AGENTS.md`.
