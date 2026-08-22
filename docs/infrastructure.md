# Infrastructure baseline

> Verified against merged `master` on 2026-08-22 after the Razor Pages/ManagementHub migration and billing/reporting updates.

## Runtime stack

- ASP.NET Core .NET 10 host.
- Razor Pages for product/admin HTTP presentation.
- ASP.NET Core SignalR for authorized management realtime notifications.
- Server-Sent Events (SSE) for Chat progress/typewriter output.
- ASP.NET Core Identity.
- EF Core + PostgreSQL 17.
- pgvector for semantic retrieval.
- provider-neutral AI services with Ollama/Gemini/OpenAI/OpenRouter adapters.
- process-local document indexing queue + hosted worker.
- runtime source storage under `storage/uploads/`.
- VNPay billing with persisted payment orders and account-level RAG quota.

PRN222 is seeded demo data; runtime workflows are multi-subject.

## Presentation allocation

```text
Razor Pages:
  Account/authentication
  Admin users/subjects
  Subject catalogue
  Documents/Chapters
  Chat
  Evaluation
  Reports/Billing

Realtime:
  Chat       -> SSE
  Management -> authorized SignalR ManagementHub
```

PR #46 removed the remaining MVC product presentation and completed this architecture on `master`.

## Application boundaries

Important contracts include:

```text
IDocumentIndexingQueue
IDocumentIndexingService
ITextEmbeddingService
IChatCompletionService
IRagQueryService
IChatPageService
IEvaluationService
IReportQueryService
IBillingService
IBillingReportQueryService
IManagementRealtimeNotifier
```

Preferred boundary:

```text
Razor Page / PageModel
 -> Application-facing service
 -> Infrastructure implementation
 -> persistence/provider detail
```

PageModels should not inject `ApplicationDbContext` directly; PR #46 added regression coverage for this rule.

## Flow 1 indexing

```text
subject-aware Razor Page handler
 -> validate + authorize policy/concrete Subject
 -> persist requested change
 -> IDocumentIndexingQueue when required
 -> publish ManagementChanged after commit
```

Background path:

```text
IDocumentIndexingQueue
 -> InMemoryDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> PDF/DOCX/PPTX parser
 -> TextChunker
 -> ITextEmbeddingService
 -> replace DocumentChunk rows / persist status
 -> ManagementChanged(Document, IndexStatusChanged, Status)
```

The queue remains process-local. Startup recovery re-enqueues persisted incomplete indexing work. PDF uses PdfPig; DOCX/PPTX use OpenXml.

## Management SignalR

```text
ManagementHub
/hubs/management
server event: ManagementChanged
```

Resources: `Document`, `Chapter`, `Subject`, `SubjectLeaderAssignments`, `User`.

Changes: `Created`, `Updated`, `Deleted`, `IndexStatusChanged`, `AssignmentsChanged`, `RoleChanged`.

Scoped groups:

```text
subject:{guid:D}
admin:users
admin:subjects
subjects:catalog
```

Subscriptions enforce server-side policy and concrete-subject authorization. SignalR is fan-out only; write transactions remain in Razor Page handlers/application services and notifications are emitted only after successful persistence.

## Flow 2 RAG and Chat transport

```text
subject-aware ChatSession
 -> IRagQueryService
 -> ITextEmbeddingService
 -> PgVectorDocumentChunkRetriever
 -> GroundedPromptBuilder
 -> IChatCompletionService
 -> citation parsing
 -> ChatMessage + MessageCitation persistence
```

Retrieval filters by `SubjectId` and current vector dimensions before cosine distance.

Chat remains **SSE, not SignalR**, with events such as `tool_call`, `citations`, `delta`, `done`, and `error`. PR #54 preserves distinct provider/rate-limit errors instead of collapsing them into no-document results.

## Evaluation

Evaluation is a Razor Pages surface backed by `IEvaluationService` and the packaged 50-question dataset.

## PostgreSQL system of record

PostgreSQL persists Subjects/Chapters, Documents/index state, DocumentChunks/embeddings, Identity data, ChatSessions/Messages/Citations, Data Protection keys, payment orders and quota state.

SignalR events and VNPay request parameters are transient; persisted database state remains authoritative.

## Reporting

Academic reporting:

```text
IReportQueryService -> SubjectReportSnapshot
```

is subject-scoped.

Billing reporting:

```text
IBillingReportQueryService -> Admin system-wide billing snapshot
```

is intentionally separate. Persisted `Paid` orders drive confirmed revenue/quota-sale metrics; current account-level quota purchases are not attributed to the currently viewed Subject.

## Billing/payment boundary

PR #53 introduced VNPay billing and concurrency-safe quota management. VNPay secrets must come from server-side environment configuration.

PR #56 allows a verified successful VNPay return to finalize payment when IPN is missing. The fallback updates persisted state; reporting never treats a transient return query alone as revenue truth.

## Provider selection

Workflow code remains provider-neutral through `ITextEmbeddingService` and `IChatCompletionService`.

Changing embedding provider/model/dimension requires complete corpus re-indexing. Dimension filtering supports safe transition but does not make different embedding semantic spaces interchangeable.

Current Render configuration documented by the repository:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimension: 1024
```

## Render/CD and storage

`render.yaml` defines the Docker web service + PostgreSQL deployment. Render can carry WebSocket connections for ManagementHub; clients must reconnect across instance replacement.

The demo is currently single-instance. Multi-instance SignalR requires explicit scale-out/shared-state design.

Local Compose bind-mounts `./storage/uploads`. Free Render web-service storage is ephemeral, so durable hosted uploads still require a persistent disk or object storage.

## CI invariants

Keep regression coverage for Razor Page authorization/subject scoping, PageModel service boundaries, ManagementHub group isolation, post-commit notifications, Chat SSE/error semantics, billing concurrency/payment finalization, billing-report authorization/semantics, EF migrations, PostgreSQL/pgvector and Docker configuration.
