# RAG infrastructure baseline

## Product context

The demo targets one subject: PRN222. Course documents are curated and uploaded by the Subject Leader; students consume indexed content through the chatbot. Automatic FLM crawling is not an authoritative ingestion path.

Expected source formats are PDF, DOCX, and PPTX.

The project defines three independent product workflows:

1. **Flow 1 - Document Management & Indexing** - complete
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending
3. **Flow 3 - Report & Statistics** - complete through PR #12

Conversation History belongs to Flow 2.

## Current merged infrastructure state

After PR #12, the baseline includes:

- ASP.NET Core MVC + Razor Pages
- ASP.NET Core Identity
- PostgreSQL + pgvector
- EF Core persistence/migrations
- SubjectLeader/Student roles and authorization
- Ollama local runtime
- uploaded-file storage
- runtime PRN222 Chapter Management
- Document Management request/presentation flow
- complete document parsing/chunking/embedding/indexing pipeline
- completed read-only Report & Statistics dashboard
- shared application contracts for indexing and RAG handoffs
- three-flow ownership model

Still pending:

- Flow 2 pgvector retrieval and grounded RAG backend (Member 4)
- Flow 2 chat/session/history/citation presentation and evaluation (Member 5)

See `docs/project-status.md` for the current milestone.

## Runtime components

### ASP.NET Core application

The web application is the single application process for the course demo. Both MVC controllers/views and Razor Pages are enabled so assignment requirements can share the same data/application infrastructure.

The same application currently hosts:

- authentication/authorization
- Chapter Management
- Document Management
- the document indexing background worker
- Flow 3 Reports/Statistics
- future Flow 2 RAG/chat endpoints

Do not split indexing/reporting into extra services without a concrete requirement.

### Document indexing queue and worker

`IDocumentIndexingQueue` is the request-to-background handoff contract.

The active implementation is:

```text
Infrastructure/Services/InMemoryDocumentIndexingQueue.cs
```

It is consumed by the merged `DocumentIndexingWorker`.

Architecture:

```text
Document upload / re-index
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

The queue is process-local, not a durable broker. Recovery comes from persisted document state: at worker startup, documents still marked `Uploaded` or `Processing` are queried and re-enqueued.

For this single-app course demo, this is the intended baseline. Do not introduce Redis/RabbitMQ solely to replace the current channel-backed queue.

### Document parsing

Member 3's merged parser layer supports:

- PDF through PdfPig
- DOCX through OpenXml Wordprocessing
- PPTX through OpenXml Presentation

`DocumentParserFactory` selects the parser from the stored file extension. Parsed output preserves page/slide metadata where available.

### Chunking and embeddings

`TextChunker` creates ordered text chunks with overlap while retaining source page/slide metadata.

`ITextEmbeddingService` supports:

- single-text embedding for retrieval
- ordered batch embedding for indexing

`OllamaTextEmbeddingService` implements this boundary using Ollama `/api/embed`, and `TextEmbeddingBatcher` performs bounded ordered batching for document chunks.

The same configured embedding model must be used for indexing and retrieval. Changing the embedding model requires affected documents to be re-indexed.

Default development embedding model:

```text
qwen3-embedding:0.6b
```

### Document indexing service

`DocumentIndexingService` implements the merged Flow 1 background pipeline:

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

On successful indexing:

- stale chunks are removed
- new chunks and embeddings are persisted
- `IndexStatus = Indexed`
- `IndexedAtUtc` is set
- `IndexError` is cleared

On failure:

- `IndexStatus = Failed`
- a bounded error message is persisted

### Authentication and authorization

ASP.NET Core Identity is backed by PostgreSQL.

Application roles:

- `SubjectLeader`
- `Student`

Document/Chapter write operations require `AppPolicies.ManageDocuments`, restricted to Subject Leaders.

The completed Flow 3 Reports/Statistics area also uses `AppPolicies.ManageDocuments` and is Subject-Leader-only. Access is enforced server-side; navigation visibility is not the security boundary.

### PostgreSQL + pgvector

PostgreSQL is the system of record for:

- subjects/chapters
- documents/index state
- document chunks/embeddings
- users/roles
- chat sessions/messages
- citations

Current application entities:

- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`
- ASP.NET Core Identity entities/tables

Application schema changes use EF Core migrations. PostgreSQL init scripts are only for runtime concerns such as enabling the `vector` extension.

Flow 3 aggregates this existing persistence. PR #12 did not add analytics tables, duplicated counters, or a reporting migration.

### Ollama

Ollama provides local embeddings and future chat generation.

Default development models:

- Chat: `qwen3:4b`
- Embedding: `qwen3-embedding:0.6b`

A named `Ollama` `HttpClient` is registered from `Rag:Ollama:BaseUrl`.

Ownership:

- Member 3 uses Ollama behind `ITextEmbeddingService` for indexing.
- Member 4 will use the same embedding boundary for question embeddings and `IChatCompletionService` for chat generation.
- Flow 3 reporting does not call Ollama.

### Shared application contracts

Cross-workflow contracts live under:

```text
src/PRN222.RagAssistant/Application/
```

Current contracts/models:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService` - single and ordered batch embedding
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Member 3 provides the indexing-side implementations behind the first three contracts as appropriate. Member 4 should build on these boundaries rather than duplicating provider/indexing logic.

Flow 3 completed without changing these interfaces.

### Document storage

Uploaded source documents are persisted under `storage/uploads/` and mounted into the app container at `/app/storage/uploads`.

Uploaded files are runtime data and must not be committed. PostgreSQL remains the metadata/index source of truth.

Flow 3 queries persisted metadata rather than scanning the upload directory.

## Product workflow architecture

### Flow 1 - Document Management & Indexing - COMPLETE

```text
Subject Leader
    |
    +--> manages Chapters
    \--> uploads / re-indexes document
            |
            v
Member 2 request side                 [MERGED]
            |
            v
Persist Document
            |
            v
IDocumentIndexingQueue
            |
            v
DocumentIndexingWorker                [MERGED]
            |
            v
Member 3 indexing pipeline            [MERGED]
            |
            +--> parse
            +--> chunk
            +--> embed
            +--> DocumentChunk + pgvector
            \--> Indexed / Failed
```

Flow 1 is end-to-end implemented in `master`.

### Flow 2 - RAG Question & Answer & Conversation Management - PENDING

```text
Student
    |
    | creates/opens chat session
    | asks question
    v
Member 5 presentation                 [PENDING]
    |
    v
IRagQueryService
    |
    v
Member 4 RAG backend                  [PENDING]
    |
    +--> ITextEmbeddingService.EmbedAsync
    +--> pgvector retrieval over indexed chunks
    +--> grounded context
    +--> IChatCompletionService -> Ollama chat
    +--> persist messages + citations
    v
RagAnswer + RagCitation[]
```

Conversation History remains part of this flow.

### Flow 3 - Report & Statistics - COMPLETE

PR #12 merged the reporting workflow:

```text
Subject Leader
      |
      v
Reports / Statistics                  [MERGED]
      |
      +--> Chapter/document totals
      +--> Documents by IndexStatus
      +--> Documents by Chapter / Unassigned
      +--> Total DocumentChunk count
      +--> Recent indexed / failed documents
      +--> Chat session/message/citation totals
      |
      v
Read-only dashboard / tables
```

Flow 3 uses EF Core aggregate queries with `AsNoTracking()` where appropriate. Document/indexing metrics use real Flow 1 data. Chat metrics safely return zero until Flow 2 persists chat data.

## Flow 3 non-interference boundary

Future reporting changes must not:

- alter the indexing queue/worker/parser/chunker/embedding behavior
- enqueue or re-index documents as part of reporting
- run pgvector similarity retrieval
- call Ollama
- duplicate chat/history presentation
- mutate workflow entities
- add speculative analytics infrastructure or schema

If reporting exposes a genuine missing persistence requirement, coordinate through Member 1.

## Validation snapshot

PR #12 reported `75/75` automated tests passing.

Post-merge local smoke testing reported:

- PostgreSQL + pgvector container healthy
- Ollama runtime healthy with `qwen3-embedding:0.6b`
- ASP.NET Core app healthy
- anonymous Reports access redirected to login
- Student Reports access denied
- Subject Leader Reports access successful
- Chapter creation and PDF upload/indexing successful
- `Uploaded -> Processing -> Indexed` observed through the real background pipeline
- the dashboard reflected the resulting chapter/document/chunk/indexing data

This validates the current single-app infrastructure baseline for completed Flow 1 + Flow 3. Future Flow 2 branches must rerun their own relevant validation.

## Intentionally not added

- Redis/RabbitMQ or a separate worker service
- Qdrant or another vector database
- RAGFlow/LangChain service
- automatic FLM crawling
- analytics warehouse/event pipeline/scheduled reporting

Pending product implementation is limited to Flow 2.

## Evaluation deliverable

`evaluation/` is reserved for Member 5's human-authored PRN222 evaluation set of at least 50 question/ground-truth cases. Evaluation is not part of Flow 3 merely because it contains metrics.

## Configuration ownership

- `.env.example`: local Docker Compose defaults
- `.env`: developer-specific overrides; never commit
- `appsettings.Development.json`: host-run development defaults
- Docker Compose environment variables: container-specific settings
- `AGENTS.md`: project-wide conventions/ownership
- `src/PRN222.RagAssistant/Application/AGENTS.md`: shared contract rules
- `docs/project-status.md`: current merged status
- `docs/team-workflow.md`: canonical ownership
- `docs/member-3-document-indexing-handoff.md`: completed indexing -> RAG handoff
- `docs/flow-3-report-statistics-handoff.md`: completed reporting boundary

Secrets must never be committed.
