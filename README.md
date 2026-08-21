# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, MVC + Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, background document indexing, and provider-neutral AI services.

> Documentation baseline: synchronized with `master` after PR #40 on 2026-08-21.

PRN222 is the seeded demo subject. The runtime application is multi-subject and must not treat PRN222 as a global hard-coded scope.

## Current status

| Area | Status |
|---|---|
| Core/Data/Identity/RBAC | Complete |
| Multi-subject management + Subject Leader assignment | Complete |
| AI provider runtime - Ollama/Gemini/OpenAI/OpenRouter | Complete |
| Flow 1 - Document Management & Indexing | Complete - MVC |
| Flow 2 - RAG backend | Complete |
| Flow 2 - Chat/history/citations/evaluation | Complete - MVC |
| Flow 3 - Report & Statistics | Complete - Razor Pages |
| Render CI/CD deployment | Complete for demo use |
| Repository documentation | Synchronized through PR #40 |

The three product workflows are:

1. **Flow 1 - Document Management & Indexing** - MVC Controllers + Views.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - MVC Chat + Evaluation backed by the RAG service.
3. **Flow 3 - Report & Statistics** - Razor Pages backed by an application query service.

Conversation History is part of Flow 2, not a separate workflow.

## Presentation architecture

```text
MVC Controllers + Views
  - Flow 1: Documents / Chapters
  - Flow 2: Chat / Evaluation
  - Admin users / subjects

Razor Pages
  - authentication / shell pages
  - Flow 3 Reports
```

The obsolete internal `Pages/RagDemo` surface was removed in PR #35. Product chat is the MVC `ChatController` + `Views/Chat` flow.

## Flow 2 transport

The Chat UI uses normal ASP.NET Core MVC plus **Server-Sent Events (SSE)** for progress/typewriter updates.

```text
browser fetch(POST /Chat/AskStream)
  -> text/event-stream
  -> tool_call / citations / delta / done / error events
```

There is **no SignalR hub in the current Chat flow**. The current server calls `IRagQueryService.AskAsync` and then emits SSE progress/result events; provider token streaming is not required by this implementation.

## RAG pipeline

```text
selected Subject
  -> subject-aware ChatSession
  -> IRagQueryService
  -> ITextEmbeddingService
  -> pgvector retrieval constrained by SubjectId + embedding dimensions
  -> grounded prompt/history
  -> IChatCompletionService
  -> citation marker parsing
  -> ChatMessage + MessageCitation persistence
  -> MVC Chat UI
```

PR #35 strengthened grounding, inline citations, contextual follow-up retrieval, citation reading, Markdown rendering, and the SSE progress experience.

## Evaluation

Flow 2 includes an authenticated MVC evaluation surface backed by `IEvaluationService` and the 50-question dataset under:

```text
Infrastructure/Data/evaluation_dataset_50.json
```

Single-question and full-suite evaluation resolve an active subject that matches the dataset subject code.

## Flow 3 reporting

Flow 3 remains read-only Razor Pages, but PR #40 moved its data access behind an application-facing query boundary:

```text
Reports Razor Page
  -> IReportQueryService
  -> ReportQueryService
  -> ApplicationDbContext
```

Document, indexing, chat-session, chat-message, and citation metrics are explicitly scoped to the selected `SubjectId`.

## AI runtime

Workflow code consumes provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

Backward-compatible provider:

```text
RAG_PROVIDER=Ollama
```

Optional purpose-specific overrides:

```text
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
```

Supported providers:

```text
Ollama
Gemini
OpenAI
OpenRouter
```

### Local defaults

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
Dimensions: 1024
```

### Embedding compatibility

Changing the embedding provider, model, or dimensions still requires a complete corpus re-index. Equal vector dimensions do not imply compatible semantic vector spaces.

PR #37 made re-index transitions safer when **dimensions change**: pgvector retrieval filters candidates with `vector_dims(...)` before cosine distance, so old rows with a different dimension are temporarily excluded instead of crashing retrieval. This does not make two different embedding models with the same dimensions interchangeable.

Changing only the chat provider/model/fallback order does not require re-indexing.

Canonical provider notes: `docs/ai-provider-backup.md`.

## Render deployment

`render.yaml` defines a Docker web service and Render PostgreSQL 17 in Singapore, with deployment from `master` after GitHub checks pass.

Current Render AI split after PR #39:

```text
Chat provider:      Gemini
Chat model:         gemini-3.6-flash
Embedding provider: OpenRouter
Embedding model:    nvidia/llama-nemotron-embed-vl-1b-v2:free
Embedding dims:     1024
```

Render therefore needs **two server-side AI secrets**:

```text
Rag__Gemini__ApiKey
Rag__OpenRouter__ApiKey
```

PR #38 persists ASP.NET Core Data Protection keys in PostgreSQL through `DataProtectionKeyDbContext`, so login/antiforgery key material survives web-container restarts as long as the database persists.

Uploaded source files are still stored under `/app/storage/uploads`; on a free Render web service that filesystem is ephemeral. See `docs/render-deployment.md`.

## Roles and authorization

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Flow 1/3 subject-specific management additionally checks `ISubjectAccessService`. Flow 2 validates authenticated chat-session ownership and persisted subject consistency.

Public self-registration creates only `Student` accounts.

## Flow 1 indexing

```text
DocumentsController
  -> persist Document
  -> IDocumentIndexingQueue
  -> DocumentIndexingWorker
  -> IDocumentIndexingService
  -> PDF/DOCX/PPTX parser
  -> TextChunker
  -> ITextEmbeddingService
  -> DocumentChunk replacement / index status
```

PDF uses PdfPig; DOCX/PPTX use OpenXml. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

## Commands

```bash
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes \
  --context ApplicationDbContext \
  --project src/PRN222.RagAssistant \
  --startup-project src/PRN222.RagAssistant
docker compose config
```

Local Ollama:

```bash
docker compose --profile local-ai up -d --build
```

Cloud/hybrid providers:

```bash
docker compose up -d --build
```

Do not run `docker compose down -v` unless deleting local database/model volumes is explicitly intended.

## Documentation

Start with:

- `docs/README.md`
- `docs/project-status.md`
- `docs/infrastructure.md`
- `docs/ai-provider-backup.md`
- `docs/render-deployment.md`
- `docs/rag-demo-guide.md`
- `docs/member-contributions.md`

Project coordination documents use Member numbers only. See `docs/member-contributions.md` for ownership versus merged contribution accounting.
