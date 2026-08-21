# Razor Pages + SignalR target architecture

> Documentation-only architecture decision, 2026-08-21.
>
> This document defines the required end state. The documentation PR does **not** migrate runtime source code. A follow-up implementation PR must make the code match this target before the migration is considered complete.

## Decision

The application presentation layer will use **Razor Pages only** for HTTP UI and HTTP actions.

After the implementation migration:

- product pages live under `Pages/`;
- request handlers live in `PageModel` handler methods;
- navigation/forms use `asp-page`, `asp-page-handler`, route values, and Razor Page conventions;
- the legacy MVC presentation layer is removed;
- no product route depends on MVC controller routing;
- no duplicate Razor Page and MVC surface may exist for the same workflow.

SignalR is the one intentional non-Page presentation transport. It is used only to push realtime Document Management changes to connected browsers. It is not an MVC replacement and must not contain business CRUD logic.

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

## Flow 1 - Document Management

Target HTTP flow:

```text
Documents/Chapters Razor Page handler
 -> validate subject/resource/file
 -> authorize concrete Subject
 -> persist requested change through the application/infrastructure boundary
 -> enqueue Document.Id when indexing/re-indexing is required
 -> publish a realtime document event
 -> return/redirect with subject context preserved
```

Background indexing remains separate from request handling:

```text
IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunk replacement + index status
 -> publish realtime status event
```

## Document SignalR scope

SignalR is required for the Document Management page so browsers viewing the same authorized subject can reflect changes without manual refresh.

Recommended hub route:

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

The implementation may use a compact common envelope, but every event must carry enough stable identity to update or invalidate the affected UI row safely.

### Subject-scoped groups

Connections should be grouped by subject, for example:

```text
subject:{SubjectId}
```

Joining/subscribing must be authorized server-side. The client cannot gain access to another subject merely by supplying a different `SubjectId`.

### CRUD stays in Page Handlers

SignalR must **not** become the write API.

Required pattern:

```text
browser form/fetch
 -> Razor Page handler
 -> authorization + validation + write
 -> commit succeeds
 -> IHubContext / realtime notifier broadcasts event
 -> connected Document pages update
```

This keeps normal Razor Pages validation, antiforgery, ownership, and redirect semantics intact while SignalR handles fan-out only.

### Client behavior

The Document page should:

- connect after the page has a validated subject context;
- subscribe to document events;
- update/remove the affected row when safe;
- fall back to reloading the list or fetching a fresh partial/read model when an event is insufficient;
- enable automatic reconnect;
- tolerate duplicate/out-of-order notifications by using stable IDs and fresh server state when needed.

The browser SignalR client is a separate client-side dependency; the server is configured with `AddSignalR()` and `MapHub(...)` in the implementation PR.

## Flow 2 transport remains SSE

Chat is already a Razor Page after PR #42, with PageModel data access moved behind `IChatPageService` in PR #43.

Chat keeps its current Server-Sent Events contract for progress/typewriter output. Do **not** move Chat to SignalR as part of the Document Management realtime work.

Target separation:

```text
Chat               -> Razor Pages + SSE
Document Management -> Razor Pages + SignalR notifications
Evaluation         -> Razor Pages
Reports            -> Razor Pages
Admin/Subjects     -> Razor Pages
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

Document/Chapter handlers and SignalR subscription logic must continue to enforce concrete subject access through `ISubjectAccessService` or the equivalent authorized application boundary.

## Program startup target

The implementation PR should converge on a startup model conceptually equivalent to:

```text
AddRazorPages()
AddSignalR()
...
MapHub<DocumentHub>("/hubs/documents")
MapRazorPages()
```

MVC controller/view registration and conventional controller routing are migration debt and should be removed once all product surfaces have Razor Page equivalents.

## Render deployment

Render web services support WebSocket connections, so the existing web-service deployment model is compatible with SignalR WebSockets. Clients must still reconnect because deploys/platform maintenance can replace an instance and close active connections.

For the current single-instance demo, in-process SignalR fan-out is sufficient. If the application later scales to multiple instances, realtime scale-out/shared-state requirements must be reviewed rather than assumed.

## Migration acceptance criteria

The code migration is complete only when all of the following are true:

- all product HTTP UI/action surfaces are Razor Pages;
- legacy MVC presentation code and controller routes are removed;
- navigation/forms no longer target MVC actions;
- existing URLs either continue to work or have an explicit compatibility decision;
- Chat SSE behavior remains working;
- Document create/update/delete events appear on other connected authorized Document pages through SignalR;
- indexing status updates can be pushed through the same Document realtime channel;
- authorization tests cover Razor Page handlers and SignalR subject subscriptions;
- build/tests/EF/PostgreSQL/Docker CI remains green;
- canonical docs are reconciled again after the implementation PR.
