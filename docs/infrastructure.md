# RAG infrastructure baseline

## Product context

The demo targets one subject: PRN222. Course documents are curated and uploaded by the Subject Leader; students consume indexed content through the chatbot. The system should not scrape FLM automatically. FLM remains the upstream reference for the Subject Leader when selecting course materials:

- https://flm.fpt.edu.vn/gui/role/teacher/SyllabusDetails?sylID=13892

Expected source formats are PDF, DOCX, and lecture slides. Upload authorization, parsing, chunking, indexing, retrieval, citation rendering, and chat history are feature work built on top of the infrastructure in this document.

## Runtime components

### ASP.NET Core Razor Pages app

The web application is the single application process for the course demo. It will host the UI, authentication/authorization, document-management endpoints, chat endpoints, and an in-process background indexing queue.

For this scale, document indexing can initially use ASP.NET Core `BackgroundService`/hosted services instead of introducing Redis, RabbitMQ, or a separate worker service. A separate worker can be introduced later if indexing becomes too expensive for the web process.

### PostgreSQL + pgvector

PostgreSQL remains the system of record for application data such as document metadata, chapters, users/roles, chat sessions, messages, and indexing state.

The Compose service uses the pgvector PostgreSQL image so document-chunk embeddings can live beside relational metadata. The database init script enables the `vector` extension for a new database. The .NET application registers `NpgsqlDataSource` with pgvector type support.

No application tables, `DbContext`, migrations, vector dimensions, or search indexes are defined during the infrastructure phase. Those belong to the data-model phase after the entities are agreed.

### Ollama

Ollama provides a local model runtime so the demo does not require a paid hosted AI API. It exposes one HTTP endpoint for both chat generation and embeddings.

Default development models:

- Chat: `qwen3:4b`
- Embedding: `qwen3-embedding:0.6b`

Model names are configuration, not hard-coded business rules. They can be replaced through `.env` without changing application code.

### Document storage

Uploaded source documents are persisted under `storage/uploads/` and mounted into the application container at `/app/storage/uploads`.

The directory is version-controlled only through `.gitkeep`; uploaded course files must never be committed. PostgreSQL stores document metadata and future chunk/index records, while the original binary files remain in storage so citations can point back to the source document.

## Intended RAG flow

```text
Subject Leader
    |
    | uploads PDF / DOCX / slides
    v
ASP.NET Core app
    |
    | enqueue indexing work
    v
BackgroundService
    |
    +--> parse document
    +--> split into chunks
    +--> Ollama embedding model
    +--> PostgreSQL + pgvector

Student
    |
    | asks a question
    v
ASP.NET Core app
    |
    +--> embed question with the same embedding model
    +--> retrieve relevant chunks from pgvector
    +--> build grounded prompt with source metadata
    +--> Ollama chat model
    v
Answer + citations
    |
    +--> persist chat session/messages in PostgreSQL
```

The same embedding model must be used for indexing and querying a given vector collection. Changing the embedding model requires re-indexing affected documents.

## What is intentionally not added yet

- Redis or RabbitMQ: unnecessary for the first single-app demo; hosted background services are sufficient.
- Qdrant or another separate vector database: pgvector avoids running a second database and is enough for the expected PRN222 document set.
- RAGFlow/LangChain service: the project is a .NET course application, so orchestration should remain explicit in the ASP.NET Core code unless a later requirement justifies another service.
- Automatic FLM crawling: only Subject Leader uploads are authoritative in this product model.
- Authentication schema and Subject Leader role: required before document upload is released, but should be introduced together with ASP.NET Core Identity and migrations in the authentication/data phase.
- Document parsing packages: PDF/DOCX/PPTX extraction libraries should be selected when the ingestion feature is implemented, not during container infrastructure setup.

## Evaluation deliverable

The repository already reserves `evaluation/` for the required human-authored PRN222 evaluation set. The final deliverable should contain at least 50 question/ground-truth-answer cases, ideally with source-document/chapter references so retrieval and citation quality can also be evaluated.

## Configuration ownership

- `.env.example`: local Docker Compose defaults and infrastructure model/image selection.
- `.env`: developer-specific overrides; never commit it.
- `appsettings.Development.json`: sensible defaults when running the ASP.NET Core app directly on the host.
- Docker Compose environment variables: container-specific hostnames and mounted storage paths.

Secrets should never be added to committed configuration files.
