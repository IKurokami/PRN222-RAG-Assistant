# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core, EF Core, PostgreSQL/pgvector, and Ollama for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are curated and uploaded by the Subject Leader and become the chatbot's authoritative knowledge source after indexing. Chapter organization is managed at runtime rather than fixed in seed data.

See:

- `docs/project-status.md` - latest merged project state
- `docs/team-workflow.md` - canonical member/workflow ownership
- `docs/infrastructure.md` - runtime/RAG architecture
- `docs/member-3-document-indexing-handoff.md` - completed indexing handoff to RAG
- `docs/flow-3-report-statistics-handoff.md` - completed Flow 3 reporting handoff

## Product workflows

The project defines three independent workflows:

1. **Flow 1 - Document Management & Indexing** - COMPLETE, implemented with Razor Pages. Subject Leader manages chapters/documents; uploaded material is parsed, chunked, embedded, and indexed.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING, and **must be implemented with ASP.NET Core MVC Controllers + Views**. Student asks grounded questions, receives citations, and can reopen Conversation History.
3. **Flow 3 - Report & Statistics** - COMPLETE, implemented with Razor Pages. Subject Leader reviews read-only document/indexing/chat-usage statistics.

Conversation History belongs to Flow 2 rather than being counted as Flow 3.

## Presentation model decision

The application intentionally hosts both ASP.NET Core presentation models:

```text
Flow 1 -> Razor Pages   [COMPLETE]
Flow 2 -> MVC           [PENDING]
Flow 3 -> Razor Pages   [COMPLETE]
```

Flow 2 is the workflow selected by the team to satisfy the MVC implementation requirement.

`Program.cs` already registers both `AddControllersWithViews()` and `AddRazorPages()` and maps both controller routes and Razor Pages.

For Flow 2, new presentation code should be created under MVC areas such as:

```text
src/PRN222.RagAssistant/Controllers/
src/PRN222.RagAssistant/Views/Chat/
```

Do **not** create a parallel `Pages/Chat` or other Razor Pages implementation for Flow 2. Existing Flow 1 and Flow 3 Razor Pages should remain unchanged.

MVC controllers should stay thin and delegate grounded Q&A to the application/service layer, especially `IRagQueryService`; they must not call Ollama or query pgvector directly.

## Current project status

Latest merged baseline after PR #12:

| Member | Scope | Status |
| --- | --- | --- |
| Member 1 | Core/Data, Identity, authorization, EF Core model/migrations, shared contracts | Complete |
| Member 2 | Flow 1 Document/Chapter Management | Complete / merged |
| Member 3 | Flow 1 parsing, chunking, embeddings, indexing worker/service | Complete / merged through PR #9 |
| Member 2 | Flow 3 Report & Statistics | Complete / merged through PR #12 |
| Member 4 | Flow 2 pgvector retrieval, grounded RAG backend, chat/citation persistence | Pending |
| Member 5 | Flow 2 **MVC** chat UI, Conversation History, citation rendering, evaluation | Pending |

PR #12 reported `75/75` automated tests passing. Post-merge local smoke testing also confirmed Flow 1 indexing and Flow 3 reporting against real PostgreSQL/pgvector + Ollama runtime data.

## Implemented Flow 1

Flow 1 is end-to-end implemented with Razor Pages on the request/presentation side.

```text
Subject Leader
    |
    +--> Manage PRN222 Chapters
    |
    \--> Upload / manage / re-index documents
            |
            v
Member 2 Razor Pages request side
            |
            +--> validate PDF / DOCX / PPTX and size
            +--> validate optional ChapterId
            +--> persist source file
            +--> persist Document with Uploaded status
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
Member 3 indexing pipeline
            |
            +--> parse
            +--> chunk
            +--> batch embed through Ollama
            +--> replace/persist DocumentChunk rows
            \--> Indexed / Failed
```

### Document/Chapter Management

Member 2's merged request-side behavior includes:

- runtime Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index request
- PDF/DOCX/PPTX upload validation
- 50 MB limit
- Subject Leader authorization on writes
- configured source-file storage
- safe chapter deletion that preserves documents
- queue handoff after `Document` persistence

### Document Indexing

Member 3's merged PR #9 includes:

- PDF parser via PdfPig
- DOCX/PPTX parsers via OpenXml
- `DocumentParserFactory`
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- coherent re-index chunk replacement
- `Uploaded -> Processing -> Indexed/Failed` transitions
- startup rehydration of persisted `Uploaded`/`Processing` documents

`InMemoryDocumentIndexingQueue` is the active process-local queue consumed by the worker. It is not a durable external broker; startup recovery is based on persisted document indexing state.

## Implemented Flow 3

PR #12 completed **Report & Statistics** for the Subject Leader using Razor Pages.

Primary route:

```text
/Reports/Index
```

Current dashboard includes:

- total PRN222 chapters
- total PRN222 documents
- unassigned document count
- document counts by chapter
- document counts by `Uploaded`, `Processing`, `Indexed`, and `Failed`
- indexing completion percentage
- total persisted PRN222 chunks
- recent indexing failures with `IndexError`
- recently indexed documents with chunk counts and timestamps
- total chat sessions/messages/citations
- graceful zero/empty states while Flow 2 has no chat data

The page is protected server-side by `AppPolicies.ManageDocuments`, which requires `SubjectLeader`.

Flow 3 is read-only and uses existing EF Core/PostgreSQL persistence. It does not add analytics schema, call Ollama, run pgvector similarity retrieval, or mutate the indexing pipeline.

Post-merge local smoke testing confirmed that after a PDF was uploaded and indexed through Flow 1, the dashboard reflected the resulting chapter/document/chunk/indexing values.

See `docs/flow-3-report-statistics-handoff.md`.

## Pending Flow 2 - ASP.NET Core MVC

The main unfinished product work is now Flow 2, and **Flow 2 is the workflow chosen to satisfy the MVC presentation requirement**.

Member 4 owns the RAG backend and can rely on successfully indexed `DocumentChunk` rows.

Expected backend path:

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

Member 5 owns the MVC presentation layer:

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
Member 4 RAG backend
    |
    v
MVC Views/Chat -> answer + citations + Conversation History
```

Member 5 owns MVC chat/session views, session navigation, Conversation History, citation rendering, and the evaluation deliverable.

Flow 2 must build on the completed Flow 1 indexing pipeline and must not recreate the completed Flow 3 reporting workflow.

## Team development boundaries

- **Member 1 - Core/Data Lead:** schema/migration coordination, domain/data/security/shared contracts.
- **Member 2 - Document Management + Reporting:** completed Flow 1 Razor Pages request side and completed Flow 3 Razor Pages reporting.
- **Member 3 - Document Indexing:** completed parser/chunker/embedding/worker/index-state pipeline.
- **Member 4 - RAG Backend:** pending question embedding/retrieval/grounding/generation/chat persistence; stays presentation-agnostic.
- **Member 5 - MVC Chat UI / Conversation Management / Evaluation:** pending Flow 2 Controllers + Views, Conversation History, citations, and evaluation.

Before implementing new workflow work, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

Do not duplicate cross-member contracts under `Application/`. Flow 2 MVC controllers/views must not bypass `IRagQueryService` by putting Ollama/pgvector retrieval logic directly in the presentation layer.

## Local setup

Copy the example environment file when overriding Docker Compose defaults:

```text
cp .env.example .env
```

Windows Command Prompt:

```text
copy .env.example .env
```

Restore dependencies:

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
- PostgreSQL + pgvector
- Ollama
- persistent PostgreSQL/Ollama volumes
- bind-mounted `storage/uploads/`

Check containers:

```text
docker compose ps
```

Stop:

```text
docker compose down
```

Do not use `docker compose down -v` unless intentionally deleting local PostgreSQL/Ollama data.

## Authentication and demo accounts

Roles:

- `SubjectLeader`
- `Student`

`ManageDocuments` is restricted to `SubjectLeader`. The completed Reports page uses the same server-side policy.

Demo-user seeding is disabled by default. To enable the local example users, copy `.env.example` to `.env` and set:

```text
AUTH_SEED_USERS=true
```

Change example passwords before use.

Default example identities:

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

Runtime uploads are ignored by Git; only `.gitkeep` is version-controlled.

PostgreSQL remains the source of truth for document metadata/indexing state/chunks and report aggregates.

## EF Core model and migrations

Application persistence uses `ApplicationDbContext` with ASP.NET Core Identity.

Project conventions:

- no navigation properties in domain entities
- scalar foreign keys
- dedicated `IEntityTypeConfiguration<TEntity>` per entity
- entity-specific Fluent API stays out of `ApplicationDbContext`
- application schema changes use EF Core migrations

Generate a migration only when the EF model genuinely changes:

```text
dotnet tool restore
dotnet ef migrations add <MigrationName> \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant \
  --output-dir Data/Migrations
```

Check for uncommitted model changes:

```text
dotnet ef migrations has-pending-model-changes \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant
```

Do not create application tables through PostgreSQL init scripts or `EnsureCreated`.

Flow 3 completed without a reporting-specific migration.

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

## Verify pgvector

```text
docker compose exec postgres sh -lc 'psql -U "$POSTGRES_USER" -d "$POSTGRES_DB" -c "SELECT extversion FROM pg_extension WHERE extname = '\''vector'\'';"'
```

## Run directly on host

Start PostgreSQL/Ollama with Compose, then:

```text
dotnet run --project src/PRN222.RagAssistant
```

For detailed project-wide conventions and ownership rules, see `AGENTS.md`.
