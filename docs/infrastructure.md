# Infrastructure baseline

> Synchronized with `master` after PR #40 on 2026-08-21.

## Runtime stack

- ASP.NET Core .NET 10 host.
- MVC Controllers + Views and Razor Pages in one application.
- ASP.NET Core Identity.
- EF Core + PostgreSQL 17.
- pgvector for semantic retrieval.
- provider-neutral AI services with Ollama/Gemini/OpenAI/OpenRouter adapters.
- process-local document indexing queue + hosted worker.
- runtime source storage under `storage/uploads/`.
- Bootstrap, Bootstrap Icons and project design styles.

PRN222 is seeded demo data; runtime workflows are multi-subject.

## Presentation allocation

```text
MVC:
  Flow 1 Documents / Chapters
  Flow 2 Chat / Evaluation
  Admin users / subjects

Razor Pages:
  authentication / shell
  Flow 3 Reports
```

`Pages/RagDemo` was removed in PR #35.

## Application boundaries

Important provider/presentation-safe contracts:

```text
IDocumentIndexingQueue
IDocumentIndexingService
ITextEmbeddingService
IChatCompletionService
IRagQueryService
IEvaluationService
IReportQueryService
```

Flow 3 architecture after PR #40:

```text
Report PageModel
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

The Razor Page keeps authorization/presentation responsibility while Infrastructure owns EF reporting queries.

## Flow 1 indexing

```text
subject-aware MVC request
 -> persist Document
 -> IDocumentIndexingQueue
 -> InMemoryDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> PDF/DOCX/PPTX parser
 -> TextChunker
 -> ITextEmbeddingService
 -> replace DocumentChunk rows / persist status
```

The queue is process-local. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

Parsers:

- PDF: PdfPig.
- DOCX/PPTX: OpenXml.

## Flow 2 RAG

```text
subject-aware ChatSession
 -> IRagQueryService
 -> ITextEmbeddingService
 -> PgVectorDocumentChunkRetriever
 -> GroundedPromptBuilder
 -> IChatCompletionService
 -> citation marker parsing
 -> ChatMessage + MessageCitation persistence
```

Retrieval filters indexed documents by `SubjectId` and, after PR #37, by `vector_dims(DocumentChunk.Embedding)` matching the query vector before cosine distance.

PR #35 also adds contextual follow-up query expansion when a short standalone follow-up yields no useful retrieval, plus stricter grounding/citation instructions.

## Chat transport

The MVC Chat browser calls:

```text
POST /Chat/AskStream
Content-Type response: text/event-stream
```

SSE event types include `tool_call`, `citations`, `delta`, `done`, and `error`. Client JavaScript consumes the response stream with `fetch` + `ReadableStream`.

This is **SSE, not SignalR**. The current service obtains the completed RAG answer via `IRagQueryService.AskAsync` and then emits application-level word deltas/typewriter output.

## Evaluation

`EvaluationController` consumes `IEvaluationService` and the packaged 50-question dataset. Evaluation is authenticated and resolves the active subject by the dataset subject code before invoking the RAG evaluation path.

## Provider selection

Backward-compatible default:

```text
Rag:Provider = Ollama | Gemini | OpenAI | OpenRouter
```

Purpose-specific overrides:

```text
Rag:ChatProvider
Rag:EmbeddingProvider
Rag:EmbeddingDimensions
```

Workflow code does not branch on concrete provider names.

### Embedding invariant and PR #37

A searchable corpus conceptually uses one embedding semantic space. Any provider/model/dimension change requires complete re-indexing.

PR #37 changed physical/runtime compatibility in two useful ways:

- Gemini batch embedding requests send configured output dimensionality correctly.
- pgvector retrieval filters candidate rows by actual vector dimensions before cosine distance.

Therefore different-dimension old/new rows can coexist temporarily during a gradual re-index without causing dimension errors. Rows with the old dimension simply do not participate in current retrieval. This does **not** make same-dimension embeddings from different models compatible.

## PostgreSQL system of record

PostgreSQL persists:

- Subjects/Chapters;
- Documents/index state;
- DocumentChunks/embeddings;
- Identity users/roles/claims;
- ChatSessions with `SubjectId`;
- ChatMessages;
- MessageCitations;
- ASP.NET Core Data Protection keys.

### Data Protection

PR #38 introduced a dedicated `DataProtectionKeyDbContext` using the PostgreSQL connection and configured:

```text
AddDataProtection()
 -> PersistKeysToDbContext<DataProtectionKeyDbContext>()
 -> SetApplicationName("PRN222-RAG-Assistant")
```

The `DataProtectionKeys` table is checked in CI. This keeps antiforgery/authentication key material across web-container restarts while the database persists.

## Flow 3 reporting

`ReportQueryService` produces `SubjectReportSnapshot` with:

- subject identity;
- Chapter/Document totals and grouping;
- indexing status totals;
- total chunks;
- recent failures/recently indexed documents;
- subject-scoped ChatSession, ChatMessage and MessageCitation counts.

The PageModel retains `ManageDocuments` + `ISubjectAccessService` authorization before requesting the snapshot.

## Render CD

`render.yaml` defines:

- Docker web service, free plan, Singapore;
- managed PostgreSQL 17, free plan, Singapore;
- `autoDeployTrigger: checksPass` from `master`;
- `/healthz` health check;
- startup migration/pgvector enablement.

Current Render AI runtime is **hybrid**:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimension: 1024
```

Manual AI secrets:

```text
Rag__Gemini__ApiKey
Rag__OpenRouter__ApiKey
```

## Docker modes

Local Ollama:

```bash
docker compose --profile local-ai up -d --build
```

Cloud/hybrid:

```bash
docker compose up -d --build
```

If a selected contract uses Ollama, enable the `local-ai` profile.

## Storage boundary

Local Compose bind-mounts `./storage/uploads` into the app. Free Render web-service storage is ephemeral, so hosted source-file durability still requires a persistent disk on an eligible plan or external object storage.

## CI validation

Current CI performs:

- local tool/frontend/NuGet restore;
- Release build and tests;
- pending-model check for `ApplicationDbContext`;
- Docker Compose validation;
- real PostgreSQL startup and EF migrations;
- pgvector/seed/migration/DataProtectionKeys schema checks;
- mixed-dimension pgvector compatibility smoke test.

## Intentionally not added

- SignalR for Chat;
- Redis/RabbitMQ/external job broker;
- a second vector database;
- provider-specific logic in workflow controllers/pages;
- automatic hidden local-to-cloud failover;
- provider-specific contracts in Application;
- repository-stored production API keys.
