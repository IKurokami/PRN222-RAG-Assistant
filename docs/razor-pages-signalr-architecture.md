# Razor Pages + SignalR target architecture

> Canonical architecture and implementation-state note, 2026-08-21.
>
> PR #46 retains the completed PageModel/DbContext cleanup and implements issue #47 management realtime on its branch. PR #46 is **not merged**; this document distinguishes that branch state from the merged PR #42/#43 baseline and the required target.

## Decision

The application presentation layer will use **Razor Pages only** for HTTP UI and HTTP actions.

After the implementation migration:

- product pages live under `Pages/`;
- request handlers live in `PageModel` handler methods;
- navigation/forms use `asp-page`, `asp-page-handler`, route values, and Razor Page conventions;
- the legacy MVC presentation layer is removed;
- no product route depends on MVC controller routing;
- no duplicate Razor Page and MVC surface may exist for the same workflow.

SignalR is the one intentional non-Page presentation transport. It pushes authorized management changes for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles to connected browsers. It is not an MVC replacement and must not contain business CRUD logic.

## Target page map

```text
Pages/
  Account/
  Admin/
    Users/
      Index.cshtml
      Create.cshtml
      Edit.cshtml
    Subjects/
      Index.cshtml
      Create.cshtml
      Edit.cshtml
      Leaders.cshtml
  Subjects/
    Index.cshtml
  Chapters/
    Index.cshtml
    Create.cshtml
    Edit.cshtml
    Delete.cshtml
  Documents/
    Index.cshtml
    Upload.cshtml
    Details.cshtml
    Edit.cshtml
  Chat/
    Index.cshtml
  Evaluation/
    Index.cshtml
  Reports/
    Index.cshtml
```

Exact route templates may preserve existing public URLs where compatibility matters, but routing must resolve to Razor Pages rather than MVC actions.

## PageModel boundary

PageModels own HTTP concerns:

- model binding and validation;
- authorization checks;
- antiforgery-protected writes;
- redirects and page state;
- invoking Application-facing services.

PageModels should not become large data-access classes. Follow the existing Chat/Reports direction:

```text
Razor Page / PageModel
 -> Application-facing service or command/query boundary
 -> Infrastructure implementation
 -> ApplicationDbContext / external provider
```

Direct EF Core access in new PageModels should be avoided when a purpose-specific service boundary is practical.

## Flow 1 - Management pages and indexing

Target HTTP flow:

```text
management Razor Page handler
 -> validate subject/resource
 -> authorize the corresponding policy and concrete Subject
 -> persist the requested change through the application/infrastructure boundary
 -> enqueue Document.Id when indexing/re-indexing is required
 -> publish through IManagementRealtimeNotifier after commit succeeds
 -> return/redirect with subject context preserved
```

CRUD and other writes stay in Razor Page handlers. The realtime layer only fans out committed changes.

Background indexing remains separate from request handling:

```text
IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunk replacement + index status
 -> publish ManagementChanged(Document, IndexStatusChanged)
```

## Management SignalR scope

Authorized management pages use one hub:

```text
namespace PRN222.RagAssistant.Realtime
class ManagementHub
route /hubs/management
server event ManagementChanged
```

The Application-facing notifier is:

```csharp
Task PublishAsync(
    ManagementRealtimeEvent notification,
    CancellationToken cancellationToken = default);
```

`ManagementRealtimeEvent` is the common envelope:

```text
Resource: Document | Chapter | Subject | SubjectLeaderAssignments | User
Change:   Created | Updated | Deleted | IndexStatusChanged
          | AssignmentsChanged | RoleChanged
EntityId: Guid
SubjectId: Guid?
Status:   string?
```

Document index transitions retain their special status behavior: they use `Resource = Document`, `Change = IndexStatusChanged`, and carry the current `Status`.

### Scoped groups and subscriptions

The hub uses only these groups:

```text
subject:{guid:D}
admin:users
admin:subjects
subjects:catalog
```

Its subscription methods are:

```text
SubscribeToSubject(Guid subjectId)
SubscribeToAdminUsers()
SubscribeToAdminSubjects()
SubscribeToSubjectCatalog()
```

Every subscription checks the authenticated user's applicable policy and concrete subject access on the server. A client-supplied subject ID is never sufficient authorization. Subject events are sent only to the affected subject group; user/role and subject-management events use the corresponding authorized admin/catalog groups.

### CRUD stays in Page Handlers

SignalR must **not** become the write API.

Required pattern:

```text
browser form/fetch
 -> Razor Page handler
 -> policy + subject authorization, validation and antiforgery
 -> write transaction commits
 -> IManagementRealtimeNotifier / ManagementHub broadcasts
 -> authorized connected management pages update
```

This keeps normal Razor Pages validation, antiforgery, ownership, policy and redirect semantics intact while SignalR handles fan-out only. No hub method creates, edits, deletes, re-indexes, assigns leaders, or changes roles.

### Client behavior

Management page markup opts in with `data-management-realtime`, `data-realtime-scope` values `subject`, `admin-users`, `admin-subjects`, or `subject-catalog`, and an optional `data-subject-id` for the subject scope.

The browser client should:

- connect after the page has a validated scope;
- invoke only the matching subscription method;
- enable automatic reconnect;
- handle `ManagementChanged` using stable resource/entity IDs;
- reload the authorized page/list when an event is insufficient, after reconnect, or when state may be stale;
- tolerate duplicate/out-of-order notifications by fetching fresh server state.

The browser SignalR client is a separate client-side dependency; the server is configured with `AddSignalR()` and `MapHub<ManagementHub>("/hubs/management")` in the implementation.

## Flow 2 transport remains SSE

Chat is already a Razor Page after PR #42, with PageModel data access moved behind `IChatPageService` in PR #43.

Chat keeps its current Server-Sent Events contract for progress/typewriter output. Do **not** move Chat to SignalR as part of the management realtime work.

Target separation:

```text
Chat                 -> Razor Pages + SSE
Management pages     -> Razor Pages + authorized ManagementHub
  Documents/Chapters -> subject-scoped SignalR groups
  Subjects/assignments -> authorized subject-admin/catalog groups
  Users/roles        -> authorized users-admin group
Evaluation           -> Razor Pages
Reports              -> Razor Pages
```

## Authorization

Existing roles/policies remain:

```text
Admin
SubjectLeader
Student

ManageUsers
ManageSubjects
ManageDocuments
```

Razor Page authorization must preserve current server-side rules. UI visibility is not authorization.

All management handlers and SignalR subscription methods must enforce the applicable policy and concrete subject access through `ISubjectAccessService` or the equivalent authorized application boundary.

## Program startup target

The implementation PR should converge on a startup model conceptually equivalent to:

```text
AddRazorPages()
AddSignalR()
...
MapHub<ManagementHub>("/hubs/management")
MapRazorPages()
```

MVC controller/view registration and conventional controller routing are migration debt and should be removed once all product surfaces have Razor Page equivalents.

## Render deployment

Render web services support WebSocket connections, so the existing web-service deployment model is compatible with the ManagementHub WebSocket endpoint. Clients must still reconnect because deploys/platform maintenance can replace an instance and close active connections; management pages use reload fallback when fresh state is required.

For the current single-instance demo, in-process SignalR fan-out is sufficient. If the application later scales to multiple instances, realtime scale-out/shared-state requirements must be reviewed rather than assumed.

## Migration acceptance criteria

The implementation is complete only when all of the following are true:

- all product HTTP UI/action surfaces are Razor Pages;
- legacy MVC presentation code and controller routes are removed;
- navigation/forms no longer target MVC actions;
- existing URLs either continue to work or have an explicit compatibility decision;
- Chat SSE behavior remains working and is not replaced by SignalR;
- authorized management pages receive `ManagementChanged` notifications for Document, Chapter, Subject, Subject Leader assignment, and User/role changes through `/hubs/management`;
- Document `IndexStatusChanged` notifications carry status and reach only authorized affected-subject clients;
- subscriptions use the four scoped groups and server-side policy/subject authorization;
- writes remain in Razor Page handlers and broadcasts occur only after commit;
- clients automatically reconnect and reload authorized state when an event is insufficient;
- authorization tests cover Razor Page handlers and SignalR subscriptions/realtime behavior;
- build/tests/EF/PostgreSQL/Docker CI remains green;
- canonical docs are reconciled after PR #46 is merged, without describing PR #46 as merged beforehand.
