# Agent Instructions

## Scope

This file applies to the whole repository. If a deeper `AGENTS.md` exists, its instructions apply to that subtree in addition to this file.

Follow explicit user requests when they change the current phase. Otherwise preserve the merged architecture/ownership below and do not expand scope on your own.

Before changing workflow code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
```

For indexing/RAG work also read `docs/member-3-document-indexing-handoff.md`.
For reporting work also read `docs/flow-3-report-statistics-handoff.md`.

## Current project baseline

Repository: PRN222 course RAG assistant.

- Main project: `src/PRN222.RagAssistant`
- Test project: `tests/PRN222.RagAssistant.Tests`
- Target: `net10.0`
- Web: ASP.NET Core MVC + Razor Pages
- Auth: ASP.NET Core Identity + EF Core/PostgreSQL
- Roles: `SubjectLeader`, `Student`
- Database/vector store: PostgreSQL + pgvector
- Local AI runtime: Ollama
- Source storage: `storage/uploads/`
- Solution: `PRN222-RAG-Assistant.sln`

The demo is scoped to PRN222. Subject Leaders curate/upload course documents. Chapters are runtime-managed data. Students consume successfully indexed content through Flow 2 chat. Automatic FLM crawling is not authoritative ingestion.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING
3. **Flow 3 - Report & Statistics** - PENDING

Conversation History belongs to Flow 2 and is not counted as Flow 3.

## Current merged milestone

Latest synchronized baseline after PR #10 and PR #9:

- Member 1 Core/Data: **complete**
- Member 2 Flow 1 Document/Chapter Management request side: **complete / merged**
- Member 3 Flow 1 Document Indexing/Ingestion: **complete / merged through PR #9**
- Member 4 Flow 2 RAG backend: **pending**
- Member 5 Flow 2 chat/history/citation presentation + evaluation: **pending**
- Member 2 Flow 3 Report & Statistics: **defined / pending implementation**

PR #9 completed the Member 2 -> Member 3 handoff. Flow 1 is end-to-end implemented.

When status is unclear, latest merged code on `master` is the source of truth; synchronize docs afterward.

## Team ownership and integration boundaries

### Member 1 - Core/Data Lead - COMPLETE BASELINE

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- cross-workflow `Application/` contracts/models
- schema/migration conventions
- architecture/convention tests
- coordination for genuine EF model/migration changes

Do not move later workflow business logic into Member 1 simply because shared contracts live under `Application/`.

### Member 2 - Document Management + Report & Statistics

#### Flow 1 request/presentation - COMPLETE

Merged behavior includes:

- runtime PRN222 Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index request
- PDF/DOCX/PPTX upload validation
- 50 MB limit
- source-file persistence
- document metadata persistence
- PRN222 chapter validation
- `AppPolicies.ManageDocuments` server-side enforcement
- queue handoff after document persistence
- safe chapter deletion that unassigns referenced documents first

Flow 1 request handlers must not parse, chunk, embed, query pgvector, or call Ollama.

#### Flow 3 Report & Statistics - PENDING

Member 2 owns Flow 3 in a separate focused branch such as `feature/report-statistics`.

Initial scope is read-only:

- total PRN222 chapters/documents
- documents by indexing state
- documents by chapter/unassigned
- total chat sessions/messages/citations
- graceful empty states

Because Member 3 is complete, indexing metrics can use real persisted `Uploaded`/`Processing`/`Indexed`/`Failed` data now.

Flow 3 must not:

- enqueue/re-index documents
- alter parser/chunker/embedding/worker behavior
- mutate workflow data
- run pgvector similarity retrieval
- call Ollama
- duplicate Member 5 Conversation History UI
- add speculative analytics entities/migrations
- change shared contracts solely for dashboard convenience

If reporting exposes a genuine persistence gap, coordinate through Member 1.

### Member 3 - Document Indexing / Ingestion - COMPLETE

PR #9 merged:

- `DocumentParserFactory`
- PDF parser via PdfPig
- DOCX/PPTX parsers via OpenXml
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- single and ordered-batch `ITextEmbeddingService` support
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- coherent `DocumentChunk` replacement
- index state/error/timestamp persistence
- startup rehydration of `Uploaded`/`Processing` documents

Implemented state flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

The active `InMemoryDocumentIndexingQueue` is an in-process queue consumed by the worker. It is not a durable external broker. Worker startup recovery is based on persisted document state.

Do not create a second indexing pipeline in later member branches.

See `docs/member-3-document-indexing-handoff.md`.

### Member 4 - RAG / Chat Backend - PENDING

Owns Flow 2 backend:

- question embedding through `ITextEmbeddingService.EmbedAsync`
- pgvector similarity retrieval over successfully indexed PRN222 chunks
- top-K context selection
- grounded prompt construction
- explicit no-evidence/out-of-scope behavior
- `IChatCompletionService` implementation
- `IRagQueryService` implementation
- chat-session ownership validation
- user/assistant message persistence
- ordered `MessageCitation` persistence

Member 4 must not parse raw source files, chunk documents, generate document embeddings, or mutate indexing state as part of normal Q&A.

### Member 5 - Chat UI / Conversation Management / Evaluation - PENDING

Owns Flow 2 presentation and evaluation:

- chat UI
- chat-session creation/opening/navigation
- Conversation History
- citation/source rendering
- consumption of `IRagQueryService`
- human-authored 50-question evaluation set
- evaluation-facing tooling/tests

Browser/UI code must not call Ollama or query pgvector directly.

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

`ITextEmbeddingService` supports both single-text embedding and ordered batch embedding. Indexing and retrieval must use the same configured embedding model.

Treat public signatures as stable. Prefer additive changes. If a contract genuinely must change, update all affected producers/consumers together and synchronize docs.

Flow 3 should not add a cross-member contract unless a concrete need justifies it.

## Repository layout

- `src/PRN222.RagAssistant/Application/`: shared abstractions/models
- `src/PRN222.RagAssistant/Domain/Entities/`: persistence entities
- `src/PRN222.RagAssistant/Domain/Enums/`: domain enums
- `src/PRN222.RagAssistant/Data/`: DbContext/configurations/migrations/seed IDs
- `src/PRN222.RagAssistant/Security/`: role/policy constants
- `src/PRN222.RagAssistant/Infrastructure/Parsing/`: merged document parsers/chunker
- `src/PRN222.RagAssistant/Infrastructure/Services/`: queue, indexing, Ollama embedding services
- `src/PRN222.RagAssistant/Pages/Account/`: auth UI
- `src/PRN222.RagAssistant/Pages/Chapters/`: Chapter Management
- `src/PRN222.RagAssistant/Pages/Documents/`: Document Management
- `tests/PRN222.RagAssistant.Tests/`: unit/architecture/convention tests
- `docs/`: project status/architecture/ownership/handoffs
- `evaluation/`: version-controlled evaluation set
- `infrastructure/postgres/init/`: DB runtime initialization such as pgvector extension
- `storage/uploads/`: runtime source documents; never commit uploads

## Mandatory EF Core entity rules

1. **No navigation properties in entity classes.**
   - Store explicit scalar foreign keys.

2. **No EF mapping attributes inside entities.**
   - Mapping belongs in dedicated configuration classes.
   - Validation annotations belong on request/input models when needed.

3. **Every entity has a dedicated `IEntityTypeConfiguration<TEntity>`.**
   - Location: `Data/Configurations/`
   - Name: `<EntityName>Configuration`
   - Configure table/key/length/type/requiredness/enums/indexes/relationships/delete behavior/seed data there.

4. **Configure relationships without navigation properties.**

```csharp
builder.HasOne<Subject>()
    .WithMany()
    .HasForeignKey(x => x.SubjectId)
    .OnDelete(DeleteBehavior.Restrict);
```

5. **Keep `ApplicationDbContext` thin.**
   - Call `base.OnModelCreating(builder)`.
   - Use `ApplyConfigurationsFromAssembly(...)`.
   - Do not add entity-specific Fluent API mapping directly in DbContext.

6. **Use EF Core migrations for application schema.**
   - repository-local `dotnet-ef`
   - migrations under `Data/Migrations/`
   - do not create application tables in PostgreSQL init scripts
   - do not use `EnsureCreated`
   - do not hand-edit generated migration/snapshot files unless repairing a known issue
   - synchronize with latest `master` before generating migrations

7. **Stable persistence conventions.**
   - application PKs use `Guid` unless requirement says otherwise
   - timestamps use UTC and `Utc` suffix
   - explicit delete behavior
   - domain enums follow established persisted-string convention unless justified otherwise

8. **Convention tests must stay green.**
   - `EntityModelConventionsTests`
   - `CoreDataArchitectureTests`
   - existing Chapter/Document/indexing tests

## Identity and authorization rules

- `ApplicationUser` extends `IdentityUser<Guid>` and must not gain navigation properties.
- Role names live in `Security/AppRoles.cs`.
- Policy names live in `Security/AppPolicies.cs`.
- Document/Chapter writes require `AppPolicies.ManageDocuments` and `SubjectLeader`.
- Initial Flow 3 reports are Subject-Leader-facing and read-only.
- Hiding UI is not authorization; enforce server-side.
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

Only PRN222 is seeded as subject scope. Chapters are runtime-managed and must not be invented from FLM in migrations/code without verified requirements.

The model already supports completed Flow 1, planned Flow 2 persistence, and initial Flow 3 aggregate reporting. Do not add duplicate fields merely because a later workflow is pending.

## Indexing rules

- Member 2 persists source file + `Document`, then enqueues `Document.Id`.
- Worker performs indexing out of request handlers.
- Re-indexing replaces stale chunks coherently.
- On success: `Indexed`, `IndexedAtUtc`, clear error.
- On failure: `Failed`, bounded `IndexError`.
- Do not use a different embedding model for retrieval than indexing.
- If embedding model changes, re-index affected documents.
- Do not introduce an external broker unless required.

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

Frontend libraries are restored by LibMan; source of truth is `libman.json`.
Repository-local .NET CLI tools are declared in `dotnet-tools.json`.

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

- Remote default branch is `origin/master`.
- Use focused feature branches/PRs.
- Do not have multiple members independently modify the same shared schema/contract without coordination.
- Do not recreate the already merged document-indexing implementation.
- Member 2 Flow 3 work stays on a separate reporting branch.
- Never commit `.env`, credentials, keys, DB dumps, logs, uploaded documents, build output, downloaded Ollama models, or runtime data.
- Keep source/tests/docs/migrations/config examples version-controlled.
- `storage/uploads/` is runtime data except `.gitkeep`; temp/processed/chunks paths remain ignored.
- Preserve unrelated changes.

After a major workflow merge, update `docs/project-status.md`, `docs/team-workflow.md`, relevant handoff docs, `README.md`, and agent instructions so later members work from the actual merged baseline.