# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core, EF Core, PostgreSQL/pgvector, and Ollama as the foundation for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are managed by the Subject Leader and used as the chatbot's authoritative knowledge source. See `docs/infrastructure.md` for the infrastructure decisions and intended RAG flow.

## Current baseline

The repository currently provides:

- ASP.NET Core application with both MVC controllers/views and Razor Pages enabled
- ASP.NET Core Identity backed by EF Core/PostgreSQL
- `SubjectLeader` and `Student` roles
- `ManageDocuments` authorization policy restricted to `SubjectLeader`
- EF Core domain model for PRN222 subjects/chapters/documents/chunks and chat sessions/messages/citations
- PostgreSQL + pgvector
- Ollama local model runtime
- Persistent `storage/uploads/`
- shared application contracts for upload -> indexing -> RAG -> presentation integration
- architecture/convention tests protecting core persistence and authorization assumptions
- GitHub Actions build/test/EF-migration/Compose validation

Business implementations for upload parsing, chunking/embedding jobs, retrieval, grounded prompting, citations, and chat UI are intentionally implemented by the later workflow members on top of this baseline.

## Team development boundaries

The project is split by workflow so multiple members can work without duplicating or conflicting with each other:

- **Member 1 - Core/Data Lead:** domain entities/enums, EF Core configurations/migrations, security policies, cross-workflow `Application/` contracts, architecture tests, shared integration rules.
- **Member 2 - Document Management:** upload/list/details/delete/re-index UI and request workflow. Persists a `Document`, then hands the `Document.Id` to `IDocumentIndexingQueue`.
- **Member 3 - Document Indexing:** parsers, chunking, indexing queue/worker, embeddings, `DocumentChunk` persistence, document indexing status/error transitions.
- **Member 4 - RAG Backend:** question embedding, pgvector retrieval, grounded prompts, Ollama chat generation, chat/citation persistence, `IRagQueryService` implementation.
- **Member 5 - Chat UI / History / Evaluation:** chat/history presentation, citation rendering, and the human-authored 50-question ground-truth evaluation set.

Before implementing a member workflow, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/team-workflow.md
docs/member-1-core-data-handoff.md
```

The cross-member contracts under `src/PRN222.RagAssistant/Application/` are intentional integration boundaries. Do not duplicate those interfaces in feature folders or bypass them by calling Ollama/pgvector directly from MVC controllers or Razor Page models.

### Migration ownership

The repository keeps one EF Core migration chain. Member 1 is the default schema/migration owner. A later member who discovers a genuine persistence requirement should first make the required model/configuration change coherently, synchronize with the latest branch, generate one migration, and run the pending-model check. Do not create speculative fields or parallel competing migrations.

## Local setup

Copy the example environment file when you want to override Docker Compose defaults:

```text
cp .env.example .env
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
- Persistent PostgreSQL and Ollama volumes
- Bind-mounted `storage/uploads/` for source documents

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

Only the subject itself is seeded at this stage:

```text
Code: PRN222
```

Chapter names/numbers are deliberately not invented from the FLM syllabus. Add verified chapter data only after the source information is available.

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

If you change `OLLAMA_CHAT_MODEL` or `OLLAMA_EMBEDDING_MODEL` in `.env`, pull those model names instead.

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
