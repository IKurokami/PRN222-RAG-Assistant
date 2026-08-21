# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, background document indexing, provider-neutral AI services, SSE for Chat, and SignalR as the target realtime channel for Document Management.

> Documentation target updated on 2026-08-21 after PR #42/#43.
>
> **Important:** this documentation PR defines the required presentation end state only. It does not migrate the remaining runtime MVC code. A follow-up implementation PR must remove the legacy MVC presentation layer before the codebase can be called Razor-Pages-only.

PRN222 is the seeded demo subject. Runtime workflows remain multi-subject and must not treat PRN222 as a global hard-coded scope.

## Presentation decision

The required end state is:

```text
HTTP UI + HTTP actions: Razor Pages only
Chat realtime/progress: SSE
Document Management realtime notifications: SignalR
Background indexing: hosted worker/services
```

After the implementation migration there must be no duplicate MVC/Razor Page product surfaces and no conventional controller routing for product UI.

SignalR is an intentional realtime transport, not an alternative HTTP presentation framework. Document writes remain Razor Page handlers; successful create/update/delete/index-state changes are broadcast to authorized connected browsers.

Canonical migration specification: `docs/razor-pages-signalr-architecture.md`.

## Migration status

| Area | Target presentation | Runtime status at this docs PR |
|---|---|---|
| Account/authentication | Razor Pages | Already Razor Pages |
| Flow 1 - Documents/Chapters | Razor Pages + SignalR notifications | Code migration pending |
| Flow 2 - Chat/history/citations | Razor Pages + SSE | Razor Pages after PR #42/#43 |
| Flow 2 - Evaluation | Razor Pages | Code migration pending |
| Flow 3 - Reports | Razor Pages + query service | Already Razor Pages |
| Admin users/subjects | Razor Pages | Code migration pending |
| Subject catalogue | Razor Pages | Code migration pending |

The documentation target does not count as implementation completion.

## Target page map

```text
Pages/
  Account/
  Admin/Users/
  Admin/Subjects/
  Subjects/
  Chapters/
  Documents/
  Chat/
  Evaluation/
  Reports/
```

Public URLs may be preserved with Razor Page route templates where compatibility is useful.

## Flow 1 - Document Management & indexing

Target request flow:

```text
Document/Chapter Razor Page handler
  -> validate subject/resource/file
  -> authorize concrete Subject
  -> application/infrastructure write boundary
  -> IDocumentIndexingQueue when required
  -> publish Document realtime event
```

Background indexing remains:

```text
IDocumentIndexingQueue
  -> DocumentIndexingWorker
  -> IDocumentIndexingService
  -> PDF/DOCX/PPTX parser
  -> TextChunker
  -> ITextEmbeddingService
  -> DocumentChunk replacement / index status
  -> publish DocumentIndexStatusChanged
```

### SignalR contract

Recommended hub route:

```text
/hubs/documents
```

Recommended events:

```text
DocumentCreated
DocumentUpdated
DocumentDeleted
DocumentIndexStatusChanged
```

Connections must be subject-scoped and server-authorized. SignalR does not perform CRUD; Page Handlers perform antiforgery-protected writes and publish events only after successful persistence.

## Flow 2 - Chat

Chat is already a Razor Page after PR #42. PR #43 moved its page-data/session persistence boundary behind `IChatPageService`.

The Chat UI continues to use **Server-Sent Events (SSE)** for progress/typewriter output:

```text
browser fetch(POST Razor Page handler)
  -> text/event-stream
  -> tool_call / citations / delta / done / error
```

Do not replace Chat SSE with SignalR as part of the Document Management realtime migration.

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
  -> Razor Page Chat UI
```

## Evaluation

Evaluation remains backed by `IEvaluationService` and the packaged 50-question dataset under:

```text
Infrastructure/Data/evaluation_dataset_50.json
```

The target presentation is `Pages/Evaluation/Index.cshtml` with Razor Page handlers for single-question/full-suite actions.

## Flow 3 reporting

Reports already follow the desired presentation/application boundary:

```text
Reports Razor Page
  -> IReportQueryService
  -> ReportQueryService
  -> ApplicationDbContext
```

Document, indexing, chat-session, chat-message, and citation metrics remain scoped to the selected `SubjectId`.

## AI runtime

Workflow code consumes provider-neutral contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

Supported providers:

```text
Ollama
Gemini
OpenAI
OpenRouter
```

Changing embedding provider/model/dimension requires a complete corpus re-index. PR #37 keeps dimension-changing transitions safe by filtering stored vectors by `vector_dims(...)` before cosine distance; it does not make different embedding semantic spaces compatible.

Changing only the chat provider/model/fallback order does not require re-indexing.

Canonical provider notes: `docs/ai-provider-backup.md`.

## Render deployment

`render.yaml` defines the Docker web service and Render PostgreSQL deployment.

Current Render AI split:

```text
Chat provider:      Gemini
Chat model:         gemini-3.6-flash
Embedding provider: OpenRouter
Embedding model:    nvidia/llama-nemotron-embed-vl-1b-v2:free
Embedding dims:     1024
```

Render needs server-side AI secrets:

```text
Rag__Gemini__ApiKey
Rag__OpenRouter__ApiKey
```

ASP.NET Core Data Protection keys persist in PostgreSQL. Uploaded source files on the free web service remain ephemeral.

Render web services support WebSocket connections, so the target SignalR Document hub fits the current web-service model. Clients must use reconnect behavior because instance replacement during deploy/maintenance can close active connections.

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

Subject-specific operations additionally enforce concrete subject access. SignalR subscriptions must enforce the same subject boundary server-side. UI visibility is never authorization.

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
- `docs/razor-pages-signalr-architecture.md`
- `docs/project-status.md`
- `docs/infrastructure.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/ai-provider-backup.md`
- `docs/render-deployment.md`
- `docs/member-contributions.md`
