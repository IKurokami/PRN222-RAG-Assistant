# RAG infrastructure baseline

## Product context

The demo targets PRN222. Course documents are curated/uploaded by the Subject Leader and students consume successfully indexed content through Flow 2 chat. Supported source formats are PDF, DOCX, and PPTX.

The three workflows are:

1. **Flow 1 - Document Management & Indexing** - complete - **MVC Controllers + Views**
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - **MVC Controllers + Views**
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

Conversation History belongs to Flow 2.

## Current infrastructure state

The baseline includes:

- ASP.NET Core MVC + Razor Pages in one host
- ASP.NET Core Identity
- PostgreSQL + pgvector
- EF Core persistence/migrations
- `SubjectLeader` / `Student` roles and policies
- Ollama local runtime
- uploaded-file storage
- runtime PRN222 Chapter Management
- complete Document Management request/presentation flow
- complete document parsing/chunking/embedding/indexing pipeline
- complete read-only Report & Statistics dashboard
- shared application contracts for indexing and RAG handoffs

Still pending:

- Flow 2 pgvector retrieval and grounded RAG backend
- Flow 2 chat/session/history/citation MVC presentation and evaluation

## ASP.NET Core application

The web application remains a single process. Both presentation models are enabled:

```text
MVC Controllers + Views -> Flow 1 + pending Flow 2
Razor Pages             -> Flow 3 + auth/shell pages
```

`Program.cs` keeps:

```text
AddControllersWithViews()
AddRazorPages()
MapControllerRoute(...)
MapRazorPages()
```

### Flow 1 MVC presentation boundary

Flow 1 HTTP/presentation code now lives under:

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

The previous `Pages/Documents/` and `Pages/Chapters/` implementation is removed.

Flow 1 controllers handle HTTP/model binding/authorization/navigation and the already-existing request-side persistence/orchestration. They must not:

- parse/chunk/embed uploaded documents inside request actions
- call Ollama directly for indexing/generation
- run pgvector similarity retrieval
- recreate report aggregation
- absorb Flow 2 chat responsibilities

Upload/re-index crosses the existing background boundary through `IDocumentIndexingQueue`.

See `docs/flow-1-mvc-migration.md`.

### Flow 2 MVC presentation boundary

Flow 2 remains pending and should use focused MVC code such as:

```text
Controllers/ChatController.cs
Views/Chat/
```

It should consume `IRagQueryService`. Controllers must not query pgvector or call Ollama directly.

Do not create `Pages/Chat` or `Pages/Conversation`.

## Document indexing queue and worker

`IDocumentIndexingQueue` is the request-to-background handoff contract.

Active implementation:

```text
Infrastructure/Services/InMemoryDocumentIndexingQueue.cs
```

Architecture:

```text
DocumentsController upload / re-index
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
IDocumentIndexingService
```

The queue is process-local, not a durable broker. Worker startup recovery queries persisted documents still marked `Uploaded` or `Processing` and re-enqueues them.

For this course demo, do not add Redis/RabbitMQ solely to replace the current queue.

## Parsing, chunking, and embeddings

Merged parser support:

- PDF via PdfPig
- DOCX via OpenXml Wordprocessing
- PPTX via OpenXml Presentation

`DocumentParserFactory` selects a parser from the stored extension. `TextChunker` creates ordered overlapping chunks while preserving page/slide metadata where available.

`ITextEmbeddingService` supports:

- single-text embedding for retrieval
- ordered batch embedding for indexing

`OllamaTextEmbeddingService` uses Ollama `/api/embed`; `TextEmbeddingBatcher` performs bounded ordered batching.

Indexing and retrieval must use the same configured embedding model. Changing the embedding model requires affected documents to be re-indexed.

Default embedding model:

```text
qwen3-embedding:0.6b
```

## Document indexing service

```text
Uploaded
   |
   v
Processing
   |
   +--> resolve source file
   +--> parse
   +--> chunk
   +--> embed
   +--> replace DocumentChunk rows
   |
   +--> Indexed
   \--> Failed
```

On success:

- stale chunks are replaced
- new chunks/embeddings are persisted
- `IndexStatus = Indexed`
- `IndexedAtUtc` is set
- `IndexError` is cleared

On failure:

- `IndexStatus = Failed`
- a bounded error message is persisted

## Authentication and authorization

Roles:

- `SubjectLeader`
- `Student`

Document/Chapter MVC write actions require `AppPolicies.ManageDocuments`, which requires `SubjectLeader`.

Flow 1 POST actions use anti-forgery validation. UI visibility is never the server-side authorization boundary.

Flow 3 Reports also use `AppPolicies.ManageDocuments`.

Flow 2 MVC actions must enforce authenticated user/session ownership server-side.

## PostgreSQL + pgvector

PostgreSQL is the system of record for:

- subjects/chapters
- documents/index state
- document chunks/embeddings
- users/roles
- chat sessions/messages
- citations

Application entities:

- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`
- ASP.NET Core Identity entities

Application schema changes use EF Core migrations. PostgreSQL init scripts are limited to runtime concerns such as enabling `vector`.

The Flow 1 MVC migration has no schema change and requires no migration.

## Ollama

Default development models:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Ownership:

- Member 3 uses the embedding boundary for indexing.
- Member 4 will use question embeddings and `IChatCompletionService` for Flow 2.
- MVC controllers must not call Ollama directly.
- Flow 3 reporting does not call Ollama.

## Shared application contracts

Under `src/PRN222.RagAssistant/Application/`:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

`IRagQueryService` is the presentation-facing boundary for Flow 2. Flow 1 continues to use `IDocumentIndexingQueue` as its background handoff.

## Document storage

Uploaded source documents are stored under `storage/uploads/` and mounted into the application container at `/app/storage/uploads`.

Runtime uploads must not be committed. PostgreSQL remains the metadata/index source of truth. Flow 3 queries persistence rather than scanning the filesystem.

## Workflow architecture

### Flow 1 - COMPLETE / MVC request side

```text
Subject Leader
    |
    +--> ChaptersController
    \--> DocumentsController
            |
            +--> validate / persist / manage
            |
            v
IDocumentIndexingQueue
            |
            v
DocumentIndexingWorker
            |
            v
DocumentIndexingService
            |
            +--> parse
            +--> chunk
            +--> embed
            +--> DocumentChunk + pgvector
            \--> Indexed / Failed
```

### Flow 2 - PENDING / MVC

```text
Student browser
    |
    v
ChatController + Views/Chat            [PENDING]
    |
    v
IRagQueryService
    |
    v
RAG backend                            [PENDING]
    |
    +--> question embedding
    +--> pgvector retrieval
    +--> grounded context
    +--> Ollama chat
    +--> persist messages/citations
    v
RagAnswer + RagCitation[]
```

### Flow 3 - COMPLETE / Razor Pages

```text
Subject Leader
      |
      v
Pages/Reports
      |
      v
Read-only aggregate EF Core queries
```

Flow 3 must not mutate indexing/chat data, call Ollama, perform similarity retrieval, or add speculative analytics infrastructure.

## Intentionally not added

- Redis/RabbitMQ or separate worker service
- another vector database
- RAGFlow/LangChain service
- automatic FLM crawling
- analytics warehouse/event pipeline
- duplicate Razor Pages implementations for Flow 1 or Flow 2

## Evaluation deliverable

`evaluation/` is reserved for Member 5's human-authored PRN222 evaluation set of at least 50 question/ground-truth cases.

## Configuration ownership

- `.env.example`: local Compose defaults
- `.env`: developer overrides; never commit
- `appsettings.Development.json`: host-run defaults
- Docker Compose environment variables: container settings
- `AGENTS.md`: project-wide conventions/ownership
- `src/PRN222.RagAssistant/Application/AGENTS.md`: shared contract rules
- `docs/project-status.md`: current status
- `docs/team-workflow.md`: canonical ownership
- `docs/flow-1-mvc-migration.md`: Flow 1 presentation migration
- `docs/member-3-document-indexing-handoff.md`: indexing -> RAG handoff
- `docs/flow-3-report-statistics-handoff.md`: reporting boundary

Secrets must never be committed.
