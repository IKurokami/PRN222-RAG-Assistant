# Flow 3 handoff - Report & Statistics

> Updated on 2026-08-21 for the richer academic/operational reporting dashboard.

## Status

Flow 3 is complete, read-only, subject-scoped, and remains a Razor Pages workflow under `Pages/Reports/`.

## Architecture

The PageModel does not access `ApplicationDbContext`/EF Core directly.

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

## Subject-scoped baseline metrics

The snapshot retains:

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

Chat aggregates remain explicitly constrained through `ChatSession.SubjectId`.

## Richer academic and operational metrics

The report now additionally exposes database-observable signals that have an explicit practical interpretation:

### Corpus/index health

- average chunks per indexed document;
- indexing readiness percentage in the UI;
- indexed-but-never-cited document count.

These help identify indexing backlog/failures, unusual chunking patterns, and sources that may be undiscoverable or simply unused.

### Learner/query activity

- user question count;
- assistant response count;
- average messages per session;
- active sessions in the last 7 and 30 days;
- daily user/assistant message trend for the last 7 days.

These describe actual usage and conversation depth rather than only corpus size.

### Evidence/citation usage

- assistant responses containing at least one citation;
- citation coverage percentage;
- average citations per assistant response;
- unique cited documents;
- cited indexed-document coverage;
- top cited documents with citation count, distinct sessions and distinct cited chunks;
- top cited chapters with cited-document breadth;
- top-three citation concentration;
- daily citation trend for the last 7 days.

These answer practical questions such as:

- Which documents are actually supporting answers?
- Which chapters are receiving the most retrieval demand?
- Is the indexed corpus being used broadly or narrowly?
- Are a few sources dominating most citations?
- Which indexed sources have never appeared in a persisted citation?

## Academic interpretation rule

Observable citation/usage statistics are **not** renamed into semantic RAG quality metrics.

In particular:

- citation coverage is not faithfulness;
- cited-source coverage is not context recall;
- citation count is not correctness;
- high source popularity is not proof of source quality.

Faithfulness, context precision/recall, answer relevance/correctness and similar semantic metrics require the Evaluation workflow with suitable ground truth and/or judge-based scoring.

The metric definitions, formulas, practical uses and limitations are documented in `report-statistics-metrics.md`.

## UI behavior

The Reports page now presents:

- summary KPI cards;
- actionable signal cards with a short “what this tells us” interpretation;
- seven-day activity bars for messages and citations;
- ranked cited-document table;
- ranked cited-chapter visualization;
- indexing health and chapter distribution;
- recent failures and recently indexed documents.

The implementation uses the existing Bootstrap/project design system and does not add a client-side charting dependency.

All navigation uses Razor Page routing.

## Provider boundary

Reports remain provider-independent. They do not perform embedding, retrieval, chat completion, LLM judging, or workflow mutation.

## Tests

Regression coverage verifies:

- PageModel depends on `IReportQueryService` rather than `ApplicationDbContext`;
- unknown Subject returns no snapshot;
- document/chat/citation statistics do not leak across subjects;
- indexed-but-never-cited and cited-source coverage calculations;
- cited/uncited assistant response coverage calculations;
- top cited document/chapter aggregation;
- seven-day activity aggregation;
- compatibility with the EF InMemory test setup used by report tests.

## Ownership / contribution

- Member 2 retains Flow 3 reporting behavior ownership.
- Member 1 owns cross-cutting subject/RBAC/shared-contract/documentation coordination.
- Cross-cutting reporting architecture/integration updates do not transfer Flow 3 behavior ownership.

Canonical contribution accounting: `member-contributions.md`.
