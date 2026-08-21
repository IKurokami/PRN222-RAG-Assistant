# Infrastructure baseline and target

> Updated on 2026-08-21 for the PR #46/issue #47 management realtime implementation branch.
>
> PR #46 retains the completed PageModel/DbContext cleanup and implements authorized management SignalR on its branch; it is **not merged**. Provider, runtime, storage, and deployment claims below remain unchanged unless explicitly noted.

## Runtime stack

- ASP.NET Core .NET 10 host.
- Razor Pages as the sole target HTTP presentation model.
- ASP.NET Core SignalR for authorized management realtime notifications.
- Server-Sent Events (SSE) for Chat progress/typewriter output.
- ASP.NET Core Identity.
- EF Core + PostgreSQL 17.
- pgvector for semantic retrieval.
- provider-neutral AI services with Ollama/Gemini/OpenAI/OpenRouter adapters.
- process-local document indexing queue + hosted worker.
- runtime source storage under `storage/uploads/`.
- Bootstrap, Bootstrap Icons and project design styles.

PRN222 is seeded demo data; runtime workflows are multi-subject.

## Presentation allocation target

```text
Razor Pages:
  Account/authentication
  Admin users/subjects
  Subject catalogue
  Documents/Chapters
  Chat
  Evaluation
  Reports

Realtime transports:
  Chat         -> SSE
  Management   -> authorized SignalR notifications

No product HTTP surface should remain dependent on MVC controllers/views after the implementation migration.

## Application boundaries

Important provider/presentation-safe contracts include:

```text
IDocumentIndexingQueue
IDocumentIndexingService
ITextEmbeddingService
IChatCompletionService
IRagQueryService
IChatPageService
IEvaluationService
IReportQueryService
```

Preferred presentation boundary:

```text
Razor Page / PageModel
 -> Application-facing service
 -> Infrastructure implementation
 -> persistence/provider detail
```

Chat and Reports already demonstrate this direction through `IChatPageService` and `IReportQueryService`.

## Flow 1 indexing

Target request path:

```text
subject-aware management Razor Page handler
 -> validate + authorize policy/concrete Subject
 -> persist requested change
 -> IDocumentIndexingQueue when required
 -> publish ManagementChanged after commit succeeds
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
 -> publish ManagementChanged(Document, IndexStatusChanged, Status)
```

The queue remains process-local. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

Parsers:

- PDF: PdfPig.
- DOCX/PPTX: OpenXml.

## Management SignalR transport

The authorized management hub is:

```text
namespace PRN222.RagAssistant.Realtime
ManagementHub
/hubs/management
server event: ManagementChanged
```

Application-facing code publishes through:

```csharp
Task PublishAsync(
    ManagementRealtimeEvent notification,
    CancellationToken cancellationToken = default);
```

The envelope resources are `Document`, `Chapter`, `Subject`, `SubjectLeaderAssignments`, and `User`. Changes are `Created`, `Updated`, `Deleted`, `IndexStatusChanged`, `AssignmentsChanged`, and `RoleChanged`; Document index-status events retain their `Status`.

Scoped groups are:

```text
subject:{guid:D}
admin:users
admin:subjects
subjects:catalog
```

Subscriptions are `SubscribeToSubject(Guid subjectId)`, `SubscribeToAdminUsers()`, `SubscribeToAdminSubjects()`, and `SubscribeToSubjectCatalog()`. The hub applies the same server-side policies and concrete-subject checks as the corresponding Razor Pages before adding a connection to a group.

SignalR is fan-out only. CRUD, indexing requests, subject changes, leader assignments, and user/role changes remain in Razor Page handlers/application-facing services:

```text
Razor Page handler
 -> antiforgery + validation + policy/subject authorization
 -> write transaction succeeds
 -> IManagementRealtimeNotifier / ManagementHub
 -> authorized connected clients
```

The JavaScript client opts in with `data-management-realtime`, `data-realtime-scope` (`subject`, `admin-users`, `admin-subjects`, or `subject-catalog`), and optional `data-subject-id`. It enables automatic reconnect and reloads authorized page state when `ManagementChanged` is insufficient or after a reconnect.

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

Retrieval filters indexed documents by `SubjectId` and by current vector dimensions before cosine distance.

## Chat transport

Chat remains **SSE, not SignalR**.

The Razor Page browser posts to the Chat streaming handler and consumes `text/event-stream` events such as:

```text
tool_call
citations
delta
done
error
```

Management realtime work must not alter this contract.

## Evaluation

Evaluation remains backed by `IEvaluationService` and the packaged 50-question dataset. The target UI is a Razor Page with Page Handlers for single-question/full-suite operations.

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

SignalR notifications are transient UI synchronization messages; PostgreSQL remains the source of truth.

## Flow 3 reporting

`ReportQueryService` produces `SubjectReportSnapshot` with subject-scoped Chapter/Document/index/chat metrics. The PageModel authorizes subject access before requesting the snapshot.

## Provider selection

Workflow code remains provider-neutral through:

```text
ITextEmbeddingService
IChatCompletionService
```

Changing embedding provider/model/dimension requires complete corpus re-indexing. Different dimensions may coexist temporarily during migration because retrieval filters by actual vector dimensions, but different semantic vector spaces are not interchangeable.

## Render CD and realtime compatibility

`render.yaml` defines the current Docker web service + PostgreSQL deployment.

Current Render AI runtime:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimension: 1024
```

Render web services support inbound WebSocket connections, so the target ManagementHub is compatible with the current service type. Connections can still close when an instance is replaced during deploy/platform maintenance, so client reconnect and reload fallback behavior is required.

The current demo is single-instance. If future scaling uses multiple instances, SignalR scale-out/shared realtime state must be reviewed explicitly.

## Storage boundary

Local Compose bind-mounts `./storage/uploads`. Free Render web-service storage remains ephemeral, so hosted source-file durability still requires a persistent disk or object storage.

## CI validation target

The implementation migration should keep existing build/test/EF/PostgreSQL/Docker checks and add regression coverage for:

- Razor Page handler authorization and subject scoping;
- preserved public routes where required;
- ManagementHub policy/group isolation;
- create/update/delete notifications for every management resource;
- Document index-status notifications and status payloads;
- assignment and role-change notifications;
- automatic reconnect/reload fallback;
- Chat SSE remaining unchanged and no SignalR Chat migration.

## Intentionally separated transports

```text
Chat realtime/progress        = SSE
Management realtime           = authorized SignalR
HTTP UI/actions               = Razor Pages
```

Do not collapse these into one transport merely for uniformity, and do not migrate Chat from SSE to SignalR.
