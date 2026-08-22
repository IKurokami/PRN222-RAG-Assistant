# Razor Pages + SignalR architecture

> Canonical architecture verified against merged `master` on 2026-08-22. PR #46 completed the Razor Pages migration and authorized management realtime implementation.

## Decision and current state

The application uses **Razor Pages** for product/admin HTTP UI and actions.

```text
HTTP product/admin presentation -> Razor Pages
Chat progress/result            -> SSE
Management realtime             -> SignalR ManagementHub
Background indexing             -> hosted services
```

The legacy MVC product presentation layer was removed in PR #46. SignalR is an intentional fan-out transport and does not contain CRUD/business write logic.

## Page map

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

## PageModel boundary

PageModels own HTTP concerns such as binding, validation, authorization, antiforgery, redirects and page state, then invoke purpose-specific application services.

```text
Razor Page / PageModel
 -> Application-facing service
 -> Infrastructure implementation
 -> ApplicationDbContext / external provider
```

PR #46 added regression protection against direct `ApplicationDbContext` injection into PageModels.

## Flow 1 - Management and indexing

```text
management Razor Page handler
 -> validate resource/subject
 -> authorize policy + concrete Subject
 -> persist through application service
 -> enqueue Document.Id when indexing is required
 -> publish ManagementChanged after commit succeeds
```

Background indexing remains separate:

```text
IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunk replacement + index status
 -> publish ManagementChanged(Document, IndexStatusChanged)
```

## ManagementHub

```text
namespace PRN222.RagAssistant.Realtime
ManagementHub
route: /hubs/management
server event: ManagementChanged
```

`ManagementRealtimeEvent` supports resources `Document`, `Chapter`, `Subject`, `SubjectLeaderAssignments`, and `User`, with changes `Created`, `Updated`, `Deleted`, `IndexStatusChanged`, `AssignmentsChanged`, and `RoleChanged`. Document index transitions include `Status`.

Scoped groups:

```text
subject:{guid:D}
admin:users
admin:subjects
subjects:catalog
```

Subscriptions:

```text
SubscribeToSubject(Guid subjectId)
SubscribeToAdminUsers()
SubscribeToAdminSubjects()
SubscribeToSubjectCatalog()
```

Every subscription enforces server-side policy and concrete subject access. A client-provided subject ID is never authorization by itself.

## Write/realtime separation

```text
browser form/fetch
 -> Razor Page handler
 -> antiforgery + validation + authorization
 -> write commits
 -> IManagementRealtimeNotifier
 -> ManagementHub broadcasts
 -> authorized connected clients refresh/update
```

No hub method creates, edits, deletes, re-indexes, assigns leaders or changes roles.

Clients use automatic reconnect and reload authorized server state when an event is insufficient, reordered, duplicated, or received after reconnect.

## Chat stays SSE

Chat is Razor Pages with page/session persistence behind `IChatPageService`, while RAG progress/result rendering remains Server-Sent Events.

```text
Chat                 -> Razor Pages + SSE
Management pages     -> Razor Pages + ManagementHub
Evaluation           -> Razor Pages
Reports              -> Razor Pages
```

Do not migrate Chat to SignalR merely for transport uniformity.

## Authorization

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers
ManageSubjects
ManageDocuments
```

Management handlers and hub subscriptions enforce applicable policies plus concrete subject access through `ISubjectAccessService` or an equivalent authorized application boundary. UI visibility is never authorization.

## Startup shape

The implemented host follows the Razor Pages + SignalR model:

```text
AddRazorPages()
AddSignalR()
...
MapHub<ManagementHub>("/hubs/management")
MapRazorPages()
```

Product MVC controllers/views and conventional product controller routing are no longer part of the current architecture.

## Render deployment

Render web services can carry the ManagementHub WebSocket endpoint. Clients reconnect because deployments/platform maintenance can replace an instance. The demo is currently single-instance; multi-instance SignalR scale-out would require an explicit shared backplane/state design.

## Maintained acceptance invariants

- product/admin HTTP surfaces remain Razor Pages;
- no duplicate MVC product surface is reintroduced;
- Chat remains SSE;
- ManagementHub fan-out is authorized and scoped;
- Document `IndexStatusChanged` includes status and reaches only authorized subject clients;
- writes stay in Razor Page handlers/application services and broadcasts happen only after commit;
- clients reconnect and reload server truth when needed;
- build/tests/EF/PostgreSQL/Docker checks remain green;
- PageModels stay behind purpose-specific service boundaries rather than direct DbContext access.
