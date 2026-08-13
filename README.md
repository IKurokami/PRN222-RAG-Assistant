# PRN222 RAG Assistant

A PRN222 course project built with ASP.NET Core, EF Core, PostgreSQL/pgvector, and Ollama for a document-grounded RAG assistant.

The demo is scoped to PRN222. Course documents are curated and uploaded by the Subject Leader and become the chatbot's authoritative knowledge source after indexing. Chapter organization is managed at runtime rather than fixed in seed data.

See:

- `docs/project-status.md` - latest merged project state
- `docs/team-workflow.md` - canonical member/workflow ownership
- `docs/infrastructure.md` - runtime/RAG architecture
- `docs/member-3-document-indexing-handoff.md` - completed indexing handoff to RAG
- `docs/flow-3-report-statistics-handoff.md` - Flow 3 reporting scope

## Product workflows

The project defines three independent workflows:

1. **Flow 1 - Document Management & Indexing** - Subject Leader manages chapters/documents; uploaded material is parsed, chunked, embedded, and indexed.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - Student asks grounded questions, receives citations, and can reopen Conversation History.
3. **Flow 3 - Report & Statistics** - Subject Leader reviews read-only document/indexing/chat-usage statistics.

Conversation History belongs to Flow 2 rather than being counted as Flow 3.

## Current project status

Latest baseline after PR #10 and PR #9:

| Member | Scope | Status |
| --- | --- | --- |
| Member 1 | Core/Data, Identity, authorization, EF Core model/migrations, shared contracts | Complete |
| Member 2 | Flow 1 Document/Chapter Management | Complete / merged |
| Member 3 | Flow 1 parsing, chunking, embeddings, indexing worker/service | Complete / merged through PR #9 |
| Member 4 | Flow 2 pgvector retrieval, grounded RAG backend, chat/citation persistence | Pending |
| Member 5 | Flow 2 chat UI, Conversation History, citation rendering, evaluation | Pending |
| Member 2 | Flow 3 Report & Statistics | Defined / pending implementation |

PR #9 head CI run #43 completed successfully before merge.

## Implemented Flow 1

Flow 1 is now end-to-end implemented.

```text
Subject Leader
    |
    +--> Manage PRN222 Chapters
    |
    \--> Upload / manage / re-index documents
            |
            v
Member 2 request side
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

## Pending Flow 2

Member 4 owns the RAG backend and can now rely on successfully indexed `DocumentChunk` rows.

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

Member 5 owns presentation, session navigation, Conversation History, citation rendering, and the evaluation deliverable.

## Pending Flow 3

Member 2 owns **Report & Statistics** in a separate focused branch such as:

```text
feature/report-statistics
```

Initial read-only metrics:

- total chapters/documents
- documents by indexing state
- documents by chapter/unassigned
- total chat sessions/messages/citations
- optional chunk totals/recent indexing failures using existing persisted data

Document/indexing statistics are already meaningful now that Member 3 is merged. Chat usage metrics should render zero/empty states until Flow 2 persists chat data.

Flow 3 must not add speculative analytics tables, call Ollama, run pgvector similarity retrieval, mutate indexing/chat data, or duplicate Member 5 pages.

## Team development boundaries

- **Member 1 - Core/Data Lead:** schema/migration coordination, domain/data/security/shared contracts.
- **Member 2 - Document Management + Reporting:** completed Flow 1 request side; pending read-only Flow 3.
- **Member 3 - Document Indexing:** completed parser/chunker/embedding/worker/index-state pipeline.
- **Member 4 - RAG Backend:** pending question embedding/retrieval/grounding/generation/chat persistence.
- **Member 5 - Chat UI / Conversation Management / Evaluation:** pending Flow 2 presentation/evaluation.

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

Do not duplicate cross-member contracts under `Application/` or bypass them by putting Ollama/pgvector logic directly in Razor Page models/controllers.

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

`ManageDocuments` is restricted to `SubjectLeader`.

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

PostgreSQL remains the source of truth for document metadata/indexing state/chunks.

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