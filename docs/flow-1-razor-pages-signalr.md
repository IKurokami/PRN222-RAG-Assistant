# Flow 1 Razor Pages + SignalR architecture

> Documentation-only target, 2026-08-21. Runtime migration is pending a follow-up code PR.

## Target presentation

Flow 1 HTTP UI/actions use Razor Pages only:

```text
Pages/Chapters/
  Index.cshtml
  Create.cshtml
  Edit.cshtml
  Delete.cshtml

Pages/Documents/
  Index.cshtml
  Upload.cshtml
  Details.cshtml
  Edit.cshtml
```

Existing public route shapes may be preserved through Razor Page route templates where useful, but product traffic must resolve to PageModels rather than MVC actions after migration.

## Multi-subject behavior

Flow 1 remains fully subject-scoped.

- Every list/detail/write is evaluated in a concrete Subject context.
- Admin can manage any existing Subject.
- Subject Leader can manage only assigned Subjects.
- Student cannot manage academic content.
- Posted route/form values never replace server-side authorization of the persisted resource.

## Request/indexing boundary

```text
Razor Page handler
 -> validate subject/chapter/file
 -> authorize concrete Subject
 -> persist Document/Chapter change
 -> enqueue Document.Id when indexing is required
 -> publish realtime event after persistence succeeds
 -> redirect/render preserving subject context

background indexing
 -> parse PDF/DOCX/PPTX
 -> TextChunker
 -> ITextEmbeddingService
 -> replace DocumentChunk rows / update status
 -> publish DocumentIndexStatusChanged
```

PageModels do not parse/chunk/embed/call providers/query pgvector.

## SignalR realtime behavior

The Documents page uses SignalR to synchronize connected authorized browsers.

Recommended hub:

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

Recommended group:

```text
subject:{SubjectId}
```

### Write path

SignalR is not the write API.

```text
browser form/fetch
 -> Razor Page POST handler
 -> antiforgery + validation + authorization
 -> database/file/indexing operation
 -> successful commit
 -> IHubContext / notifier broadcast
```

This keeps Razor Pages as the single HTTP presentation model and uses SignalR only for realtime fan-out.

### Client update strategy

On an event, the Documents page may:

- insert/update/remove the matching row using a stable Document ID;
- refresh the affected row/read model from the server;
- reload the current list when event data is intentionally minimal.

The client should enable automatic reconnect and tolerate duplicate/out-of-order notifications.

## Re-index after embedding changes

Changing embedding provider/model/dimension still requires a complete corpus re-index.

PR #37 makes a dimension-changing transition safer by excluding stored vectors whose dimensions do not match the current query embedding before cosine distance. This prevents transition-time dimension errors but does not make different embedding models semantically compatible.

## Chapter deletion

Deleting a Chapter continues to preserve its Documents by clearing affected `ChapterId` values within the same Subject according to the existing business rule.

## Acceptance criteria for the implementation PR

- Flow 1 pages exist under `Pages/Documents` and `Pages/Chapters`.
- All current upload/list/details/edit/delete/re-index and Chapter CRUD behavior is preserved.
- Subject authorization remains server-side.
- Legacy MVC presentation for Flow 1 is removed after parity verification.
- Document create/update/delete changes broadcast through SignalR.
- Indexing status changes broadcast through the same document realtime channel.
- SignalR subscriptions are subject-authorized.
- Existing indexing services/background worker remain provider-neutral.
- CI/tests remain green.
