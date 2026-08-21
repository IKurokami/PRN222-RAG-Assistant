# Infrastructure baseline and target

> Updated on 2026-08-21 after PR #42/#43.
>
> This document defines the accepted presentation target. Remaining runtime MVC surfaces are implementation debt for a follow-up code PR.

## Runtime stack

- ASP.NET Core .NET 10 host.
- Razor Pages as the sole target HTTP presentation model.
- ASP.NET Core SignalR for Document Management realtime notifications.
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
  Chat      -> SSE
  Documents -> SignalR notifications
```

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
subject-aware Document/Chapter Razor Page handler
 -> validate + authorize
 -> persist requested change
 -> IDocumentIndexingQueue when required
 -> publish Document realtime event
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
 -> publish DocumentIndexStatusChanged
```

The queue remains process-local. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

Parsers:

- PDF: PdfPig.
- DOCX/PPTX: OpenXml.

## Document SignalR transport

Recommended endpoint:

```text
/hubs/documents
```

Recommended server-to-client events:

```text
DocumentCreated
DocumentUpdated
DocumentDeleted
DocumentIndexStatusChanged
```

Recommended subject group shape:

```text
subject:{SubjectId}
```

Server-side authorization must validate access before a connection/subscription receives subject events.

SignalR is fan-out only. CRUD remains in Razor Page handlers:

```text
Razor Page POST handler
 -> antiforgery + validation + subject authorization
 -> write transaction succeeds
 -> realtime notifier / IHubContext
 -> authorized connected clients
```

The JavaScript client should enable automatic reconnect and use stable IDs so duplicate/out-of-order notifications can be tolerated.

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

Document SignalR work must not alter this contract.

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

Render web services support inbound WebSocket connections, so the target SignalR Document hub is compatible with the current service type. Connections can still close when an instance is replaced during deploy/platform maintenance, so client reconnect behavior is required.

The current demo is single-instance. If future scaling uses multiple instances, SignalR scale-out/shared realtime state must be reviewed explicitly.

## Storage boundary

Local Compose bind-mounts `./storage/uploads`. Free Render web-service storage remains ephemeral, so hosted source-file durability still requires a persistent disk or object storage.

## CI validation target

The implementation migration should keep existing build/test/EF/PostgreSQL/Docker checks and add regression coverage for:

- Razor Page handler authorization and subject scoping;
- preserved public routes where required;
- Document SignalR authorization/group isolation;
- create/update/delete notifications;
- indexing status notifications;
- Chat SSE remaining unchanged.

## Intentionally separated transports

```text
Chat realtime/progress        = SSE
Document Management realtime = SignalR
HTTP UI/actions               = Razor Pages
```

Do not collapse these into one transport merely for uniformity.
