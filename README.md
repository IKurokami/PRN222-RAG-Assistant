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

Run application:

```text
dotnet run --project src/PRN222.RagAssistant
```

Start PostgreSQL:

```text
docker compose up -d
```

Check containers:

```text
docker compose ps
```

Stop PostgreSQL:

```text
docker compose down
```
