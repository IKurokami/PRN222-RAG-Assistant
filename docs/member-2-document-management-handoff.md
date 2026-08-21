# Member 2 handoff - Flow 1 Document/Chapter Management + Flow 3 Reporting

> Synchronized after PR #40 on 2026-08-21.

## Status

Member 2's established product responsibilities are complete:

- Flow 1 Document/Chapter request/business behavior;
- Flow 3 Report & Statistics behavior.

Representative original merged contribution: PR #5 and PR #12.

## Flow 1 behavior

Member 2 owns established request/business semantics for:

- subject-scoped Document list/filter;
- PDF/DOCX/PPTX upload validation;
- source-file/metadata persistence;
- subject-scoped Chapter CRUD;
- details/edit/delete/re-index;
- safe Chapter deletion by unassigning affected Documents;
- queue handoff through `IDocumentIndexingQueue`.

Writes require `ManageDocuments` plus concrete Subject authorization through `ISubjectAccessService`.

## Indexing/provider boundary

Flow 1 request code does not parse/chunk/embed or know which provider is selected.

```text
Controller
 -> IDocumentIndexingQueue
 -> indexing pipeline
 -> ITextEmbeddingService
```

Changing the embedding provider/model/dimension triggers corpus re-indexing but does not change Member 2's upload/CRUD/re-index request semantics.

## Flow 3 behavior

Member 2 owns the read-only reporting behavior:

- Chapter/Document totals and grouping;
- indexing status metrics;
- DocumentChunk totals;
- recent indexing failures;
- recently indexed documents;
- chat session/message/citation aggregate presentation;
- zero-state behavior.

## PR #40 reporting architecture update

The previous direct EF access in `Pages/Reports/Index.cshtml.cs` has been replaced by:

```text
Report PageModel
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

The PageModel still owns authorization and view-state mapping. The query service owns read-only EF aggregation.

PR #40 also closes the old follow-up about global Chat totals: ChatSession, ChatMessage, and MessageCitation counts are now scoped through the selected subject.

Member 2 retains reporting behavior ownership; the query-boundary refactor is cross-cutting architecture/integration.

## Documentation

Member 2 reports status/doc impacts to the documentation coordinator so canonical docs remain synchronized.

See `member-contributions.md` for contribution accounting. Project documentation uses Member numbers only.
