# Member 2 handoff - Flow 1 Document/Chapter Management + Flow 3 Reporting

> Updated on 2026-08-21 for the Razor Pages + SignalR target architecture.

## Ownership

Member 2 retains established product/business behavior ownership for:

- Flow 1 Document/Chapter request semantics;
- Flow 3 Report & Statistics behavior.

Representative original merged contribution remains PR #5 and PR #12. Presentation migration does not rewrite historical contribution credit.

## Flow 1 target presentation

Flow 1 HTTP UI/actions must migrate to Razor Pages under:

```text
Pages/Documents/
Pages/Chapters/
```

Required behavior to preserve:

- subject-scoped Document list/filter;
- PDF/DOCX/PPTX upload validation;
- source-file/metadata persistence;
- subject-scoped Chapter CRUD;
- details/edit/delete/re-index;
- safe Chapter deletion by unassigning affected Documents;
- queue handoff through `IDocumentIndexingQueue`.

Writes require `ManageDocuments` plus concrete Subject authorization through `ISubjectAccessService`.

## SignalR requirement

Document Management adds realtime browser synchronization without moving writes out of Razor Pages.

```text
Razor Page handler
 -> authorize + validate + persist
 -> successful commit
 -> publish SignalR notification
```

Recommended events:

```text
DocumentCreated
DocumentUpdated
DocumentDeleted
DocumentIndexStatusChanged
```

Connections are subject-scoped and server-authorized. SignalR is fan-out only; it must not own CRUD business semantics.

## Indexing/provider boundary

```text
Razor Page handler
 -> IDocumentIndexingQueue
 -> indexing pipeline
 -> ITextEmbeddingService
```

Changing the embedding provider/model/dimension triggers corpus re-indexing but does not change Member 2's upload/CRUD/re-index semantics.

## Flow 3 behavior

Member 2 retains read-only reporting behavior ownership:

- Chapter/Document totals and grouping;
- indexing status metrics;
- DocumentChunk totals;
- recent failures/recently indexed documents;
- subject-scoped chat session/message/citation aggregate presentation;
- zero-state behavior.

Current architecture remains:

```text
Report PageModel
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

## Migration note

This documentation update does not count as the Flow 1 implementation migration. The follow-up code PR must preserve the behavior above while removing the legacy MVC presentation once Razor Page parity is verified.

See `razor-pages-signalr-architecture.md`, `flow-1-razor-pages-signalr.md`, and `member-contributions.md`.
