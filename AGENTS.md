# Agent Instructions

## Scope

This file applies to the whole repository. If a deeper `AGENTS.md` is added later, its instructions apply to that subtree.

Follow the user's explicit request when it changes the current project phase. Otherwise, preserve the baseline rules below and avoid expanding scope on your own.

## Project baseline

This repository is currently an initialization skeleton for a PRN222 course project:

- Main project: `src/PRN222.RagAssistant`
- Test project: `tests/PRN222.RagAssistant.Tests`
- Target framework: `net10.0`
- Web stack: ASP.NET Core Razor Pages
- Database runtime: PostgreSQL through Docker Compose
- Solution: `PRN222-RAG-Assistant.sln`

The baseline does not implement business features. Unless the user explicitly requests a phase change, do not add:

- Document management, uploads, chat, RAG, AI, embeddings, vector search, or statistics
- Authentication or authorization
- Domain models, entities, DTOs, view models, services, repositories, controllers, or `DbContext`
- Migrations, database schema, CRUD scaffolding, or PostgreSQL application connection configuration
- New Razor Pages, UI redesigns, frontend frameworks, Ollama, RAGFlow, pgvector, Qdrant, Redis, or other AI infrastructure

Keep the default Razor Pages template and `Program.cs` unchanged unless the requested work genuinely requires a change.

## Repository layout

- `src/`: application source
- `tests/`: xUnit tests
- `docs/`: project documentation
- `evaluation/`: version-controlled evaluation sets and ground truth
- `storage/uploads/`: runtime upload storage; never commit uploaded documents

Do not create architecture folders such as `Models`, `Services`, `Repositories`, or `Data` during initialization without an explicit request.

## Dependencies and frontend assets

The main project directly references:

- `Npgsql.EntityFrameworkCore.PostgreSQL` `10.0.0`
- `Microsoft.EntityFrameworkCore.Design` `10.0.0`

Frontend libraries are restored by LibMan. The source of truth is `libman.json`, and the local CLI tool is declared in `dotnet-tools.json`.

Do not edit downloaded files directly under these generated directories:

- `src/PRN222.RagAssistant/wwwroot/lib/bootstrap/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery-validation/dist/`
- `src/PRN222.RagAssistant/wwwroot/lib/jquery-validation-unobtrusive/dist/`

Update `libman.json` and run LibMan restore when frontend library versions or files need to change. Keep the library license files at the parent directories tracked.

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
docker compose up -d
docker compose ps
docker compose down
```

For ordinary source changes, run the relevant restore command(s), `dotnet build`, and targeted tests. Run `docker compose config` when Compose files change. Do not run `docker compose down -v` or remove the named PostgreSQL volume unless explicitly requested.

Do not run `dotnet ef database update`, create migrations, or create application schema during the initialization phase.

## Git and file hygiene

- Use the .NET CLI for scaffolding, solution/project changes, references, and package changes where applicable.
- Do not create a remote, push, or alter remote configuration unless explicitly requested.
- Never commit `.env`, credentials, private keys, database dumps, logs, uploaded documents, build output, or AI/RAG runtime data.
- Keep `.env.example`, `docker-compose.yml`, `README.md`, solution files, source, tests, and `evaluation/` version-controlled.
- `bin/`, `obj/`, and the LibMan-generated `wwwroot/lib/*/dist/` directories are ignored by design.
- Check `git status` before and after changes. Preserve unrelated user changes.

Before handing off a change, report the commands run, validation results, and any remaining warnings or errors. Do not silently broaden the task beyond the user's request.
