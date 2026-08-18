# Member 2 handoff - Flow 1 Document/Chapter Management + Flow 3 Reporting

> Synchronized after PR #30 merged on 2026-08-18.

## Status

Member 2's merged implementation responsibilities are complete:

- Flow 1 Document/Chapter request/business behavior;
- Flow 3 Report & Statistics behavior.

Representative merged contribution: PR #5 and PR #12.

## Flow 1 behavior

Member 2 owns established request/business semantics for:

- subject-scoped Document list/filter;
- PDF/DOCX/PPTX upload validation, 50 MB maximum;
- source-file/metadata persistence;
- subject-scoped Chapter CRUD;
- details/edit/delete/re-index;
- safe Chapter deletion by unassigning affected Documents;
- queue handoff through `IDocumentIndexingQueue`.

## Authorization

Writes require `AppPolicies.ManageDocuments` plus concrete Subject authorization through `ISubjectAccessService`.

## Indexing/provider boundary

Flow 1 request code does not parse/chunk/embed or know which provider is selected.

```text
Controller
 -> IDocumentIndexingQueue
 -> indexing pipeline [Member 3 maintenance owner]
 -> ITextEmbeddingService [Member 1 provider infrastructure]
```

Actual implementation credit for the original indexing pipeline in PR #9 belongs to Member 1; the issue #27 remediation in PR #30 belongs to Member 4. Member 3 retains maintenance ownership.

Changing embedding provider/model requires corpus re-indexing, but does not change Member 2's upload/CRUD/re-index request semantics.

## Flow 3 contribution

Member 2 delivered the read-only Report & Statistics dashboard behavior, including:

- Chapter/Document totals and grouping;
- indexing status metrics;
- DocumentChunk totals;
- recent indexing failures;
- recently indexed documents;
- safe zero states and report regression tests.

`ChatSession.SubjectId` now exists after PR #30. Existing report-side chat aggregates should be audited when Member 5 completes Flow 2 so chat metrics can use explicit subject scoping.

## Documentation

Member 2 reports status/doc impacts to Member 1 rather than independently editing coordination docs.

Canonical contribution accounting: `docs/member-contributions.md`.

Project documentation uses Member numbers only and must not add GitHub usernames.
