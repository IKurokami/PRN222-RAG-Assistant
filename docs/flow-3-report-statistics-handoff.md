# Flow 3 handoff - Report & Statistics

> Synchronized after PR #40 on 2026-08-21.

## Status

Flow 3 is complete, read-only, subject-scoped, and remains a Razor Pages workflow under `Pages/Reports/`.

## Architecture after PR #40

The PageModel no longer accesses `ApplicationDbContext`/EF Core directly.

```text
Pages/Reports/Index.cshtml.cs
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

Application provides presentation-safe report read models through `SubjectReportSnapshot`.

`Program.cs` registers the report boundary with `AddReporting()`.

## Authorization

Reports require:

1. coarse `ManageDocuments` policy; and
2. `ISubjectAccessService.CanManageSubjectAsync` for the concrete subject.

The query service is not the authorization boundary; the Razor Page validates access before requesting the report.

## Subject-scoped metrics

The snapshot contains:

- total Chapters;
- total Documents;
- unassigned Documents;
- Documents by Chapter;
- Uploaded/Processing/Indexed/Failed counts;
- total DocumentChunks;
- recent indexing failures;
- recently indexed Documents and chunk counts;
- total ChatSessions for the subject;
- total ChatMessages belonging to those sessions;
- total MessageCitations belonging to those messages.

PR #40 closes the previous follow-up item that chat totals were global/transitional. Chat aggregates are now explicitly constrained through `ChatSession.SubjectId`.

## Provider boundary

Reports remain provider-independent. They do not perform embedding, retrieval, chat completion, or workflow mutation.

## Tests

PR #40 added regression coverage for:

- PageModel depending on `IReportQueryService` rather than `ApplicationDbContext`;
- unknown Subject returning no snapshot;
- document/chat statistics not leaking across subjects;
- compatibility with the EF InMemory test setup used by the report tests.

## Ownership / contribution

- Member 2 retains Flow 3 reporting behavior ownership.
- Member 1 owns cross-cutting subject/RBAC/shared-contract/documentation coordination.
- PR #40 is a cross-cutting reporting architecture/integration update and does not transfer Flow 3 behavior ownership.

Canonical contribution accounting: `member-contributions.md`.
