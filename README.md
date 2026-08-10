# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core Razor Pages and PostgreSQL, designed as the foundation for a document-grounded RAG assistant.

## Development commands

Restore:

```text
dotnet restore
```

Restore frontend libraries:

```text
dotnet tool restore
dotnet libman restore
```

Build:

```text
dotnet build
```

Run application locally:

```text
dotnet run --project src/PRN222.RagAssistant
```

Start Docker Compose:

```text
docker compose up -d --build
```

Check containers:

```text
docker compose ps
```

Stop Docker Compose:

```text
docker compose down
```

Docker Compose starts the application and its infrastructure dependencies. PostgreSQL is available to the application through the internal `postgres` service hostname.

## Environment configuration

Copy `.env.example` to `.env` when you want to override the local Docker Compose defaults:

```text
cp .env.example .env
```

The `.env` file is intentionally ignored by Git. Keep local credentials and machine-specific values there, and add new infrastructure settings to `.env.example` as dependencies are introduced.

ASP.NET Core configuration can also be overridden with environment variables. For example, the PostgreSQL connection string uses the standard configuration key:

```text
ConnectionStrings__Postgres=Host=localhost;Port=5432;Database=prn222_rag;Username=postgres;Password=postgres
```
