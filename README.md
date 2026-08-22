# PRN222 RAG Assistant

ASP.NET Core RAG learning assistant built with .NET 10, Razor Pages, PostgreSQL/pgvector, ASP.NET Core Identity, background document indexing, provider-neutral AI services, SSE for Chat, and authorized SignalR realtime notifications for management surfaces.

> Documentation verified against `master` on 2026-08-22 after the Razor Pages/SignalR migration (PR #46), VNPay billing/quota integration (PR #53), Chat 429 handling (PR #54), billing analytics (PR #55), and VNPay return fallback fix (PR #56).

PRN222 is the seeded demo subject. Runtime workflows are multi-subject and must not treat PRN222 as a global hard-coded scope.

## Current presentation architecture

```text
HTTP UI + HTTP actions: Razor Pages
Chat realtime/progress: SSE
Management realtime: SignalR / ManagementHub
Background indexing: hosted worker/services
```

The MVC product presentation migration is complete on `master` after PR #46. CRUD and other writes remain Razor Page handlers/application services; SignalR is fan-out only.

## Current page map

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

## Flow 1 - Document Management & indexing

```text
Razor Page handler
  -> validate + authorize policy/concrete Subject
  -> application service / persistence
  -> IDocumentIndexingQueue when required
  -> publish ManagementChanged after commit
```

Background indexing:

```text
IDocumentIndexingQueue
  -> DocumentIndexingWorker
  -> IDocumentIndexingService
  -> PDF/DOCX/PPTX parser
  -> TextChunker
  -> ITextEmbeddingService
  -> DocumentChunk replacement / index status
  -> ManagementChanged(Document, IndexStatusChanged)
```

Management realtime uses `/hubs/management` and authorized scoped subscriptions for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles. Clients reconnect automatically and reload server state when needed.

## Flow 2 - Chat and Evaluation

Chat is Razor Pages and keeps **Server-Sent Events (SSE)** for progress/typewriter output:

```text
browser POST Razor Page handler
  -> text/event-stream
  -> tool_call / citations / delta / done / error
```

Chat page/session persistence is behind `IChatPageService`. Rate-limit failures are handled distinctly from no-document/no-evidence cases after PR #54.

RAG pipeline:

```text
selected Subject
  -> subject-aware ChatSession
  -> IRagQueryService
  -> ITextEmbeddingService
  -> pgvector retrieval constrained by SubjectId + embedding dimensions
  -> grounded prompt/history
  -> IChatCompletionService
  -> citation parsing
  -> ChatMessage + MessageCitation persistence
  -> Razor Page Chat UI + SSE
```

Evaluation is a Razor Pages surface backed by `IEvaluationService` and the packaged 50-question dataset under `Infrastructure/Data/evaluation_dataset_50.json`.

## Flow 3 - Reports & Statistics

Academic reporting remains subject-scoped:

```text
Reports Razor Page
  -> IReportQueryService
  -> ReportQueryService
  -> ApplicationDbContext
```

Admin billing analytics is intentionally separate and system-wide:

```text
Billing report Razor Page
  -> IBillingReportQueryService
  -> BillingReportQueryService
  -> ApplicationDbContext
```

Academic corpus/chat/citation metrics must not be mixed with account-level revenue/quota metrics unless the product later persists explicit subject attribution.

## Billing and quota

PR #53 added VNPay account-level quota purchases with concurrency-safe quota reservation/activation. VNPay credentials are server-side environment secrets.

PR #56 allows a cryptographically verified successful VNPay return to finalize an order when the IPN callback is missing; persisted payment state remains the reporting source of truth. PR #55 added Admin-only billing analytics based on persisted order/quota semantics.

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

Changing embedding provider/model/dimension requires a complete corpus re-index. Dimension filtering makes migrations safe but does not make different embedding semantic spaces compatible. Changing only the chat provider/model/fallback order does not require re-indexing.

Current Render split documented by the repository:

```text
Chat provider:      Gemini
Chat model:         gemini-3.6-flash
Embedding provider: OpenRouter
Embedding model:    nvidia/llama-nemotron-embed-vl-1b-v2:free
Embedding dims:     1024
```

## Render deployment

`render.yaml` defines the Docker web service and Render PostgreSQL deployment. ASP.NET Core Data Protection keys persist in PostgreSQL. Uploaded source files on the free web service remain ephemeral unless durable storage is added.

Required AI/payment secrets must be supplied through deployment environment variables; never commit real credentials.

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

Subject-specific operations additionally enforce concrete subject access. ManagementHub subscriptions enforce the same server-side boundary. UI visibility is never authorization.

## Verification commands

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
- `docs/razor-pages-signalr-architecture.md`
- `docs/infrastructure.md`
- `docs/role-access-control.md`
- `docs/multi-subject-management.md`
- `docs/ai-provider-backup.md`
- `docs/payment-integration-vnpay.md`
- `docs/report-statistics-metrics.md`
- `docs/billing-report-statistics.md`
- `docs/render-deployment.md`
- `docs/member-contributions.md`
