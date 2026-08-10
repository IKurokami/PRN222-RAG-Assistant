# Agent Instructions

## Scope

This file applies to the whole repository. If a deeper `AGENTS.md` is added later, its instructions apply to that subtree.

Follow the user's explicit request when it changes the current project phase. Otherwise, preserve the baseline rules below and avoid expanding scope on your own.

## Project baseline

This repository is the infrastructure baseline for a PRN222 course RAG assistant:

- Main project: `src/PRN222.RagAssistant`
- Test project: `tests/PRN222.RagAssistant.Tests`
- Target framework: `net10.0`
- Web stack: ASP.NET Core Razor Pages
- Relational/vector database: PostgreSQL + pgvector through Docker Compose
- Local AI runtime: Ollama through Docker Compose
- Source-document storage: `storage/uploads/`
- Application infrastructure registration: `src/PRN222.RagAssistant/Infrastructure`
- Solution: `PRN222-RAG-Assistant.sln`

The product is scoped to one subject, PRN222. Course documents are selected and uploaded by the Subject Leader. The application must not treat automatic FLM crawling as an authoritative ingestion path.

The baseline provides runtime dependencies but does not implement business features. Unless the user explicitly requests a feature/data phase, do not add:

- Document entities, chapters, chunks, chat sessions, messages, DTOs, view models, repositories, or a `DbContext`
- Migrations, application tables, vector dimensions/indexes, or CRUD scaffolding
- Authentication/authorization or the Subject Leader role
- Upload pages, document parsing, chunking, embedding jobs, retrieval, prompt construction, citations, chat, or statistics
- New external infrastructure such as Redis, RabbitMQ, Qdrant, RAGFlow, or a separate worker service without a demonstrated need
- New Razor Pages or UI redesigns

Keep infrastructure independent from domain features. For the first course demo, prefer an ASP.NET Core queued `BackgroundService` for future indexing work before adding a separate message broker/worker topology.

## Repository layout

- `src/`: application source
- `tests/`: xUnit tests
- `docs/`: project documentation, including infrastructure decisions
- `evaluation/`: version-controlled evaluation sets and human-authored ground truth
- `infrastructure/postgres/init/`: database runtime initialization such as enabling PostgreSQL extensions; do not create application tables here
- `storage/uploads/`: runtime upload storage; never commit uploaded documents
- `src/PRN222.RagAssistant/Infrastructure/`: infrastructure-only DI and external-system registration
- `src/PRN222.RagAssistant/Dockerfile`: application container build
- `.dockerignore`: Docker build-context exclusions

Do not create architecture folders such as `Models`, `Services`, `Repositories`, or `Data` during the infrastructure phase without an explicit request to start feature/data modeling.

## Dependencies and frontend assets

The main project directly references:

- `Npgsql` `10.0.0`
- `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.0`
- `Microsoft.EntityFrameworkCore.Design` `10.0.0`
- `Pgvector` `0.3.2`

Frontend libraries are restored by LibMan. The source of truth is `libman.json`, and the local CLI tool is declared in `dotnet-tools.json`.

Do not edit downloaded files directly under these generated directories:

- `src/PRN222.RagAssistant/wwwroot/lib/bootstrap/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery-validation/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery-validation-unobtrusive/dist/`

Update `libman.json` and run LibMan restore when frontend library versions or files need to change. Keep the library license files at the parent directories tracked.

## Infrastructure configuration

- `ConnectionStrings:Postgres` is the application-level PostgreSQL configuration key; use `ConnectionStrings__Postgres` for environment overrides.
- The registered `NpgsqlDataSource` has pgvector type support enabled.
- `Rag:Ollama:BaseUrl`, `Rag:Ollama:ChatModel`, and `Rag:Ollama:EmbeddingModel` describe the AI runtime.
- The named `Ollama` `HttpClient` is registered from `Rag:Ollama:BaseUrl`.
- `Rag:Storage:UploadsPath` describes source-document storage.
- Docker Compose provides container-to-container hostnames and waits for PostgreSQL and Ollama health before starting the app.
- `.env.example` documents Compose-level local defaults. Never commit the real `.env` file.
- PostgreSQL init scripts may enable extensions such as `vector`, but application schema belongs in EF Core migrations later.

Default local models are `qwen3:4b` for chat and `qwen3-embedding:0.6b` for embeddings. Treat model names as configuration. If the embedding model changes after documents have been indexed, affected documents must be re-indexed with the new model.

## Docker workflow

- Compose contains `app`, `postgres`, and `ollama` services.
- The app connects internally to PostgreSQL through `postgres` and to Ollama through `ollama`; host-run development uses `localhost`.
- Source documents are bind-mounted from `storage/uploads/` into the app container.
- Keep named PostgreSQL and Ollama data volumes persistent unless the user explicitly requests a reset.
- Do not add pgAdmin, Qdrant, Redis, RabbitMQ, Elasticsearch, RAGFlow, or other services without an explicit requirement.

## Standard commands

Run from the repository root:

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet run --project src/PRN222.RagAssistant
docker compose config
docker compose up -d --build
docker compose ps
docker compose logs app
docker compose exec ollama ollama list
docker compose down
```

For ordinary source changes, run the relevant restore command(s), `dotnet build`, and targeted tests. Run `docker compose config` when Compose files change. Do not run `docker compose down -v` or remove named PostgreSQL/Ollama volumes unless explicitly requested.

Do not run `dotnet ef database update`, create migrations, or create application schema during the infrastructure phase.

## Git and file hygiene

- Use the .NET CLI for scaffolding, solution/project changes, references, and package changes where applicable.
- Do not create a remote, push, or alter remote configuration unless explicitly requested.
- The current remote default branch is `origin/master`; `origin/main` does not exist. Use `git pull --ff-only origin master` unless the user explicitly requests a branch change.
- Never commit `.env`, credentials, private keys, database dumps, logs, uploaded documents, build output, downloaded Ollama models, or other AI/RAG runtime data.
- Keep `.env.example`, `docker-compose.yml`, `README.md`, infrastructure init scripts, solution files, source, tests, docs, and `evaluation/` version-controlled.
- `bin/`, `obj/`, and the LibMan-generated `wwwroot/lib/*/dist/` directories are ignored by design.
- Check `git status` before and after changes. Preserve unrelated user changes.

Before handing off a change, report the commands run, validation results, and any remaining warnings or errors. Do not silently broaden the task beyond the user's request.
