# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core Razor Pages and PostgreSQL/pgvector, designed as the foundation for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are uploaded by the Subject Leader and used as the chatbot's authoritative knowledge source. See `docs/infrastructure.md` for the infrastructure decisions and intended RAG flow.

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

Check containers:

```text
docker compose ps
```

Pull the default local AI models after Ollama is running:

```text
docker compose exec ollama ollama pull qwen3:4b
docker compose exec ollama ollama pull qwen3-embedding:0.6b
```

If you change `OLLAMA_CHAT_MODEL` or `OLLAMA_EMBEDDING_MODEL` in `.env`, pull those model names instead.

List installed Ollama models:

```text
docker compose exec ollama ollama list
```

Verify pgvector:

```text
docker compose exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT extversion FROM pg_extension WHERE extname = '\''vector'\'';"'
```

The init script enables pgvector automatically when PostgreSQL creates a new database volume. If you already have a `postgres_data` volume from the earlier PostgreSQL-only setup, enable the extension once with:

```text
docker compose exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "CREATE EXTENSION IF NOT EXISTS vector;"'
```

The application is available at the configured `APP_PORT` (`http://localhost:8080` by default). Ollama is exposed on port `11434` by default for local development and debugging.

Stop Docker Compose:

```text
docker compose down
```

Do not use `docker compose down -v` unless you intentionally want to delete the PostgreSQL and Ollama data volumes.

## Run the application directly

Start PostgreSQL and Ollama with Compose, then run the web application on the host:

```text
dotnet run --project src/PRN222.RagAssistant
```

`appsettings.Development.json` points the host-run application at `localhost:5432` and `localhost:11434`.

## Environment configuration

`.env.example` documents the local infrastructure defaults:

- `APP_PORT`
- `POSTGRES_IMAGE`, database credentials, and port
- `OLLAMA_IMAGE` and port
- `OLLAMA_CHAT_MODEL`
- `OLLAMA_EMBEDDING_MODEL`

The `.env` file is intentionally ignored by Git. Keep local credentials and machine-specific values there.

ASP.NET Core settings can also be overridden with standard double-underscore environment-variable keys. Compose currently provides:

```text
ConnectionStrings__Postgres
Rag__Ollama__BaseUrl
Rag__Ollama__ChatModel
Rag__Ollama__EmbeddingModel
Rag__Storage__UploadsPath
```

## Current infrastructure boundary

The repository now has the runtime dependencies needed for the RAG implementation, but it intentionally does not yet contain document entities, a `DbContext`, migrations, authentication/Subject Leader roles, upload pages, parsers, chunking logic, embedding jobs, retrieval logic, chat history tables, or citation rendering. Those should be implemented in feature/data-model phases on top of this baseline.
