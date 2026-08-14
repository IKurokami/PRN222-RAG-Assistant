# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core, EF Core, PostgreSQL/pgvector, and Ollama for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are curated/uploaded by the Subject Leader and become the chatbot's authoritative source after indexing. Chapters are runtime-managed application data.

## Product workflows

The project defines three independent workflows:

1. **Flow 1 - Document Management & Indexing** - COMPLETE - **ASP.NET Core MVC Controllers + Views**.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - **ASP.NET Core MVC Controllers + Views**.
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages.

Conversation History belongs to Flow 2 rather than being counted as a separate workflow.

## Presentation architecture

```text
Flow 1 -> MVC           [COMPLETE]
Flow 2 -> MVC           [PENDING]
Flow 3 -> Razor Pages   [COMPLETE]
Auth/shell -> Razor Pages
```

The application intentionally remains a mixed ASP.NET Core host. `Program.cs` registers both `AddControllersWithViews()` and `AddRazorPages()` and maps both MVC conventional routes and Razor Pages.

Flow 1 was migrated from Razor Pages to MVC. Its current presentation code lives under:

```text
src/PRN222.RagAssistant/Controllers/DocumentsController.cs
src/PRN222.RagAssistant/Controllers/ChaptersController.cs
src/PRN222.RagAssistant/Models/Documents/
src/PRN222.RagAssistant/Models/Chapters/
src/PRN222.RagAssistant/Views/Documents/
src/PRN222.RagAssistant/Views/Chapters/
```

The old `Pages/Documents/` and `Pages/Chapters/` implementations are removed to avoid parallel handlers.

See `docs/flow-1-mvc-migration.md`.

## Current project status

| Member | Scope | Status |
| --- | --- | --- |
| Member 1 | Core/Data, Identity, authorization, EF Core model/migrations, shared contracts | Complete |
| Member 2 | Flow 1 Document/Chapter Management MVC request side | Complete |
| Member 3 | Flow 1 parsing, chunking, embeddings, indexing worker/service | Complete / merged through PR #9 |
| Member 2 | Flow 3 Report & Statistics Razor Pages | Complete / merged through PR #12 |
| Member 4 | Flow 2 pgvector retrieval, grounded RAG backend, chat/citation persistence | Pending |
| Member 5 | Flow 2 MVC chat UI, Conversation History, citation rendering, evaluation | Pending |

Before the Flow 1 presentation migration, PR #12 reported `75/75` automated tests passing and local smoke testing confirmed Flow 1 indexing and Flow 3 reporting against PostgreSQL/pgvector + Ollama. This migration updates the Flow 1 presentation/tests without changing persistence or indexing services.

## Flow 1 - Document Management & Indexing

### MVC request/presentation side

Current behavior includes:

- runtime PRN222 Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index
- PDF/DOCX/PPTX validation
- 50 MB upload limit
- Subject-Leader-only writes through `AppPolicies.ManageDocuments`
- anti-forgery validation on POST actions
- configured source-file storage
- optional server-side PRN222 `ChapterId` validation
- safe chapter deletion that preserves documents
- queue handoff after `Document` persistence

Primary routes use the conventional MVC route:

```text
/Documents/Index
/Documents/Upload
/Documents/Details/{id}
/Documents/Edit/{id}
/Chapters/Index
/Chapters/Create
/Chapters/Edit/{id}
/Chapters/Delete/{id}
```

### Background indexing

```text
DocumentsController upload / re-index
        |
        v
Persist Document / update state
        |
        v
IDocumentIndexingQueue
        |
        v
InMemoryDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
DocumentIndexingService
        |
        +--> PDF parser via PdfPig
        +--> DOCX/PPTX parsers via OpenXml
        +--> TextChunker
        +--> TextEmbeddingBatcher
        +--> OllamaTextEmbeddingService
        +--> replace/persist DocumentChunk rows
        \--> Indexed / Failed
```

State flow:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

The active queue is process-local. Startup recovery is based on persisted `Uploaded`/`Processing` document state.

The MVC controllers must not absorb parsing, chunking, embedding, pgvector retrieval, or Ollama generation logic.

## Flow 3 - Report & Statistics

Flow 3 remains Razor Pages under:

```text
src/PRN222.RagAssistant/Pages/Reports/
```

The Subject-Leader dashboard includes:

- total PRN222 chapters/documents
- unassigned document count
- documents by chapter
- documents by indexing state
- indexing completion percentage
- total persisted PRN222 chunks
- recent indexing failures
- recently indexed documents
- total chat sessions/messages/citations
- zero/empty states before Flow 2 chat data exists

Flow 3 is read-only and uses existing EF Core/PostgreSQL persistence. It does not call Ollama or perform pgvector similarity retrieval.

## Pending Flow 2 - MVC

Member 4 owns the presentation-agnostic RAG backend:

```text
Question
   |
   v
ITextEmbeddingService.EmbedAsync
   |
   v
pgvector retrieval over Indexed PRN222 chunks
   |
   v
Grounded context
   |
   v
IChatCompletionService -> Ollama
   |
   +--> persist ChatMessage
   \--> persist MessageCitation
   |
   v
RagAnswer + RagCitation[]
```

Member 5 owns MVC presentation:

```text
Student browser
    |
    v
ChatController / MVC actions
    |
    v
IRagQueryService
    |
    v
MVC Views/Chat -> answer + citations + Conversation History
```

Do not implement Flow 2 under `Pages/Chat` or `Pages/Conversation`. MVC controllers must not call Ollama or query pgvector directly.

## Team development boundaries

- **Member 1 - Core/Data Lead:** schema/migration coordination, domain/data/security/shared contracts.
- **Member 2 - Document Management + Reporting:** Flow 1 MVC request side and Flow 3 Razor Pages reporting.
- **Member 3 - Document Indexing:** parser/chunker/embedding/worker/index-state pipeline.
- **Member 4 - RAG Backend:** question retrieval/grounding/generation/chat persistence.
- **Member 5 - MVC Chat UI / Conversation Management / Evaluation:** pending Flow 2 presentation/evaluation.

Read before implementing workflow work:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/flow-1-mvc-migration.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

## Local setup

Copy the example environment file when overriding Docker Compose defaults:

```text
cp .env.example .env
```

Windows Command Prompt:

```text
copy .env.example .env
```

Restore dependencies and validate:

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
```

Start Docker Compose:

```text
docker compose up -d --build
docker compose ps
```

Compose starts:

- ASP.NET Core application
- PostgreSQL + pgvector
- Ollama
- persistent PostgreSQL/Ollama volumes
- bind-mounted `storage/uploads/`

Stop without deleting data:

```text
docker compose down
```

Do not use `docker compose down -v` unless intentionally deleting local PostgreSQL/Ollama data.

## Authentication and demo accounts

Roles:

- `SubjectLeader`
- `Student`

`ManageDocuments` is restricted to `SubjectLeader`. Flow 1 MVC write actions and Flow 3 Reports use this server-side policy.

Demo-user seeding is disabled by default. To enable local example users, copy `.env.example` to `.env` and set:

```text
AUTH_SEED_USERS=true
```

Change example passwords before use.

Example identities:

```text
leader@prn222.local
student@prn222.local
```

Sign in at:

```text
http://localhost:8080/Account/Login
```

Users cannot self-select the `SubjectLeader` role.

## Document storage

Uploaded documents use configured `Rag:Storage:UploadsPath`, defaulting to `storage/uploads/` locally.

Runtime uploads are ignored by Git; only `.gitkeep` is version-controlled. PostgreSQL remains the source of truth for document metadata/indexing state/chunks.

## EF Core model and migrations

Application persistence uses `ApplicationDbContext` with ASP.NET Core Identity.

Project conventions:

- no navigation properties in domain entities
- scalar foreign keys
- dedicated `IEntityTypeConfiguration<TEntity>` per entity
- entity-specific Fluent API stays out of `ApplicationDbContext`
- application schema changes use EF Core migrations

The Flow 1 MVC migration does **not** require an EF Core migration.

Generate a migration only when the EF model genuinely changes:

```text
dotnet tool restore
dotnet ef migrations add <MigrationName> --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant --output-dir Data/Migrations
dotnet ef migrations has-pending-model-changes --project src/PRN222.RagAssistant --startup-project src/PRN222.RagAssistant
```

Do not create application tables through PostgreSQL init scripts or `EnsureCreated`.

## PRN222 seed data

Only PRN222 subject identity/scope is seeded. Chapters are runtime-managed application data and must not be invented from FLM in code/migrations without a verified requirement.

## Ollama models

Default local models:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Pull them after Ollama is running:

```text
docker compose exec ollama ollama pull qwen3:4b
docker compose exec ollama ollama pull qwen3-embedding:0.6b
```

If the embedding model changes after indexing, affected documents must be re-indexed.
