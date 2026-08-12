# RAG infrastructure baseline

## Product context

The demo targets one subject: PRN222. Course documents are curated and uploaded by the Subject Leader; students consume indexed content through the chatbot. The system should not scrape FLM automatically. FLM remains an upstream reference for the Subject Leader when selecting course materials.

Expected source formats are PDF, DOCX, and PPTX lecture slides.

The merged baseline now includes:

- authentication/authorization
- EF Core domain persistence and migration baseline
- PostgreSQL + pgvector integration
- shared application contracts
- runtime Chapter Management for PRN222
- document upload/list/details/edit/removal/re-index request flows
- request-side queue handoff through `IDocumentIndexingQueue`

The following are still pending later workflow implementation:

- real background indexing worker
- document parsing/chunking
- Ollama embedding implementation
- retrieval and grounded prompting
- chat backend
- chat UI/history/citation rendering

See `docs/project-status.md` for the current team milestone.

## Runtime components

### ASP.NET Core application

The web application is the single application process for the course demo. Both MVC controllers/views and Razor Pages are enabled so the same shared services/data layer can support the course assignment requirements.

The application already hosts authentication/authorization and the merged document/chapter management endpoints.

For this project scale, document indexing should initially use an ASP.NET Core hosted/background service instead of introducing Redis, RabbitMQ, or a separate worker service. A separate worker can be introduced later only if a concrete scaling requirement justifies it.

### Current queue state

`IDocumentIndexingQueue` is already the request-to-background handoff contract used by Member 2.

The repository currently registers:

```text
Infrastructure/Services/InMemoryDocumentIndexingQueue.cs
```

This implementation is a **temporary integration stub** merged with Member 2. It only lets upload/re-index request code enqueue document IDs before the real Member 3 background subsystem exists.

It must not be mistaken for completed indexing. No hosted worker currently consumes documents into parsed/chunked/embedded `DocumentChunk` data as part of the merged Member 2 scope.

Member 3 owns replacement/integration of this temporary queue with the hosted worker and `IDocumentIndexingService`.

### Authentication and authorization

ASP.NET Core Identity is backed by the same PostgreSQL database.

Application roles:

- `SubjectLeader`
- `Student`

Document-management and chapter-management write operations are protected by the `ManageDocuments` policy, which allows only the `SubjectLeader` role. Public role selection is intentionally not exposed.

The merged Member 2 pages enforce this server-side rather than relying only on hidden UI controls.

### PostgreSQL + pgvector

PostgreSQL is the system of record for document metadata, chapters, users/roles, chat sessions, messages, indexing state, chunks, and citations.

The Compose service uses a pgvector-enabled PostgreSQL image so document-chunk embeddings can live beside relational metadata. The database init script enables the `vector` extension for a new database. The .NET application registers both `NpgsqlDataSource` and EF Core provider support for pgvector.

The EF Core model and committed migration baseline already exist. Application schema changes must continue through EF Core migrations; PostgreSQL init scripts are only for runtime concerns such as enabling extensions.

Current persistence includes:

- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`
- ASP.NET Core Identity tables

The current model already carries document indexing state/error/timestamps, page/slide metadata, vector embeddings, chat history, and citation links. Do not add duplicate persistence fields unless a concrete workflow requirement cannot be represented by the existing model.

Runtime Chapter CRUD does not require a schema change because the model already supports it. Member 2 now uses that capability directly.

### Ollama

Ollama provides a local model runtime so the demo does not require a paid hosted AI API. It exposes HTTP endpoints used later for chat generation and embeddings.

Default development models:

- Chat: `qwen3:4b`
- Embedding: `qwen3-embedding:0.6b`

Model names are configuration, not hard-coded business rules. They can be replaced through environment configuration without changing business code.

A named `Ollama` `HttpClient` is already registered from `Rag:Ollama:BaseUrl`.

Member 2 does not call Ollama. Member 3 should use Ollama behind `ITextEmbeddingService`; Member 4 should use it behind `IChatCompletionService`.

### Shared application contracts

Cross-workflow contracts live under:

```text
src/PRN222.RagAssistant/Application/
```

Current contracts:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

The Member 2 request flow already consumes `IDocumentIndexingQueue`. The remaining contracts are intended for Members 3-5.

See `src/PRN222.RagAssistant/Application/AGENTS.md`, `docs/team-workflow.md`, and `docs/member-2-document-management-handoff.md` before implementing later workflows.

### Document storage

Uploaded source documents are persisted under `storage/uploads/` and mounted into the application container at `/app/storage/uploads`.

The directory is version-controlled only through `.gitkeep`; uploaded course files must never be committed. Runtime processing directories are also ignored according to `.gitignore`.

Member 2 now writes source files into the configured upload path and stores document metadata in PostgreSQL. If database persistence fails after a new source file has been written, the upload flow removes that new file to avoid leaving an orphaned upload.

PostgreSQL remains the source of truth for document metadata and chunk/index records, while original binaries stay in storage.

## Current end-to-end RAG flow

The request side up to the queue handoff is merged; the background and RAG sections remain pending.

```text
Subject Leader
    |
    | manages PRN222 Chapters
    | uploads PDF / DOCX / PPTX
    v
Member 2 document-management workflow       [MERGED]
    |
    | persist source file + Document
    v
IDocumentIndexingQueue
    |
    v
InMemoryDocumentIndexingQueue               [TEMPORARY STUB]
    |
    v
Member 3 hosted worker                      [PENDING]
    |
    | IDocumentIndexingService
    +--> parse document
    +--> split into chunks
    +--> ITextEmbeddingService -> Ollama embedding model
    +--> PostgreSQL + pgvector

Student
    |
    | asks a question
    v
Member 5 chat presentation                  [PENDING]
    |
    | IRagQueryService
    v
Member 4 RAG backend                        [PENDING]
    |
    +--> ITextEmbeddingService -> question embedding
    +--> retrieve relevant indexed chunks from pgvector
    +--> build grounded prompt with source metadata
    +--> IChatCompletionService -> Ollama chat model
    +--> persist chat messages + MessageCitation rows
    v
RagAnswer + RagCitation[]
```

The same embedding model must be used for indexing and querying a given vector collection. Changing the embedding model requires re-indexing affected documents.

## What is intentionally not added yet

- Redis or RabbitMQ: unnecessary for the first single-app demo.
- Qdrant or another separate vector database: pgvector is already the chosen vector store.
- RAGFlow/LangChain service: orchestration should remain explicit in the .NET application unless a requirement justifies otherwise.
- Automatic FLM crawling: Subject Leader uploads remain authoritative.
- Real document parsing packages: Member 3 should choose extraction libraries as part of the indexing implementation.
- Real indexing worker/service: Member 3 responsibility.
- Retrieval/grounded generation: Member 4 responsibility.
- Chat/history UI and citation rendering: Member 5 responsibility.

Document and Chapter Management are no longer in this "not added" list because Member 2 has merged them.

## Evaluation deliverable

The repository reserves `evaluation/` for the required human-authored PRN222 evaluation set. The final deliverable should contain at least 50 question/ground-truth-answer cases, ideally with source-document/chapter references so retrieval and citation quality can also be evaluated.

## Configuration ownership

- `.env.example`: local Docker Compose defaults and infrastructure model/image selection.
- `.env`: developer-specific overrides; never commit it.
- `appsettings.Development.json`: sensible defaults when running the ASP.NET Core app directly on the host.
- Docker Compose environment variables: container-specific hostnames and mounted storage paths.
- `AGENTS.md`: project-wide architecture, schema, authorization, current workflow status, and team ownership rules.
- `src/PRN222.RagAssistant/Application/AGENTS.md`: shared application-contract rules.
- `docs/project-status.md`: current merged milestone and next integration owner.

Secrets should never be added to committed configuration files.
