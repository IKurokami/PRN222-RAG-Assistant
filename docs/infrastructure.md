# RAG infrastructure baseline

## Product context

The demo targets one subject: PRN222. Course documents are curated and uploaded by the Subject Leader; students consume indexed content through the chatbot. The system should not scrape FLM automatically. FLM remains the upstream reference for the Subject Leader when selecting course materials:

- https://flm.fpt.edu.vn/gui/role/teacher/SyllabusDetails?sylID=13892

Expected source formats are PDF, DOCX, and lecture slides. Authentication/authorization, the core data model, migrations, PostgreSQL/pgvector integration, and shared application contracts are already part of the baseline. Upload/parsing/chunking/indexing, retrieval, grounded prompting, citation rendering, and chat UI/history are the workflow implementations built on top of that baseline.

## Runtime components

### ASP.NET Core application

The web application is the single application process for the course demo. Both MVC controllers/views and Razor Pages are enabled so the same shared services/data layer can support the course assignment requirements.

The application hosts authentication/authorization, document-management endpoints, chat endpoints, and an in-process background indexing queue.

For this scale, document indexing should initially use ASP.NET Core `BackgroundService`/hosted services instead of introducing Redis, RabbitMQ, or a separate worker service. A separate worker can be introduced later only if indexing becomes too expensive for the web process.

### Authentication and authorization

ASP.NET Core Identity is backed by the same PostgreSQL database.

Application roles:

- `SubjectLeader`
- `Student`

Document-management write operations are protected by the `ManageDocuments` policy, which allows only the `SubjectLeader` role. Public role selection is intentionally not exposed.

### PostgreSQL + pgvector

PostgreSQL is the system of record for application data such as document metadata, chapters, users/roles, chat sessions, messages, indexing state, chunks, and citations.

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

### Ollama

Ollama provides a local model runtime so the demo does not require a paid hosted AI API. It exposes one HTTP endpoint for both chat generation and embeddings.

Default development models:

- Chat: `qwen3:4b`
- Embedding: `qwen3-embedding:0.6b`

Model names are configuration, not hard-coded business rules. They can be replaced through `.env` without changing application code.

A named `Ollama` `HttpClient` is already registered from `Rag:Ollama:BaseUrl`.

### Shared application contracts

Cross-workflow contracts live under:

```text
src/PRN222.RagAssistant/Application/
```

They intentionally separate presentation and workflow code from concrete infrastructure/provider details.

Current shared contracts:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

See `src/PRN222.RagAssistant/Application/AGENTS.md` and `docs/team-workflow.md` before implementing later workflows.

### Document storage

Uploaded source documents are persisted under `storage/uploads/` and mounted into the application container at `/app/storage/uploads`.

The directory is version-controlled only through `.gitkeep`; uploaded course files must never be committed. PostgreSQL stores document metadata and chunk/index records, while the original binary files remain in storage so citations can point back to the source document.

## Intended RAG flow

```text
Subject Leader
    |
    | uploads PDF / DOCX / slides
    v
Document-management workflow
    |
    | persist source file + Document
    v
IDocumentIndexingQueue
    |
    v
BackgroundService
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
Chat presentation
    |
    | IRagQueryService
    v
RAG backend
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

- Redis or RabbitMQ: unnecessary for the first single-app demo; hosted background services are sufficient.
- Qdrant or another separate vector database: pgvector avoids running a second database and is enough for the expected PRN222 document set.
- RAGFlow/LangChain service: the project is a .NET course application, so orchestration should remain explicit in the ASP.NET Core code unless a later requirement justifies another service.
- Automatic FLM crawling: only Subject Leader uploads are authoritative in this product model.
- Document parsing packages: PDF/DOCX/PPTX extraction libraries should be selected by the indexing workflow when that feature is implemented.
- Upload/indexing/RAG/chat business implementations: these belong to Members 2-5 according to `docs/team-workflow.md`.

## Evaluation deliverable

The repository already reserves `evaluation/` for the required human-authored PRN222 evaluation set. The final deliverable should contain at least 50 question/ground-truth-answer cases, ideally with source-document/chapter references so retrieval and citation quality can also be evaluated.

## Configuration ownership

- `.env.example`: local Docker Compose defaults and infrastructure model/image selection.
- `.env`: developer-specific overrides; never commit it.
- `appsettings.Development.json`: sensible defaults when running the ASP.NET Core app directly on the host.
- Docker Compose environment variables: container-specific hostnames and mounted storage paths.
- `AGENTS.md`: project-wide architecture, schema, authorization, and team ownership rules.
- `src/PRN222.RagAssistant/Application/AGENTS.md`: shared application-contract rules.

Secrets should never be added to committed configuration files.
