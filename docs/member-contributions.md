# Member contribution ledger

> Merged baseline verified against `master` on 2026-08-22. PR #46 is merged and is now part of auditable implementation history.

## Accounting rule

- **Ownership** = scope a member is responsible for maintaining.
- **Contribution credit** = merged implementation/review/integration work actually delivered.
- Keep PR numbers as auditable evidence.
- Historical product implementation remains valid contribution credit even when later architecture migrations replace its presentation layer.

## Member 1

Maintains/delivered major cross-cutting platform work including Core/Data/Identity/RBAC, multi-subject management, provider/runtime configuration, deployment integration, schema/contract coordination, and repository documentation synchronization.

Representative merged work includes PR #4, #17, #18, #21, #28, #37, #38 and #39.

## Member 2

Delivered the main Flow 1 request/business behavior and original Flow 3 reporting behavior: Document upload/list/details/edit/delete/re-index, Chapter management, Flow 1 validation/authorization, and Report & Statistics dashboard behavior/tests.

Representative merged work includes PR #5 and PR #12. PR #40 later moved Report data access behind `IReportQueryService` and made Chat aggregates subject-scoped.

## Member 3

Delivered the cross-application UI/UX baseline in PR #19 and remains the maintenance owner for document indexing/ingestion.

## Member 4

Delivered the merged Flow 2 RAG backend baseline in PR #30, including subject-scoped retrieval/session behavior, grounding/no-evidence logic, provider-neutral calls, message/citation persistence and backend tests. Member 4 remains the maintenance owner for core Flow 2 RAG behavior.

## Member 5

Has merged product implementation credit for the original Flow 2 layer:

- **PR #34** — original Chat product UI/session/history/citations and the 50-question Evaluation Suite integration.
- **PR #35** — full-screen Chat UX, SSE progress/typewriter behavior, Markdown/code rendering, citation/source presentation and grounding/follow-up improvements.

These remain historical contribution credit even though later PRs migrated presentation architecture.

## Cross-cutting presentation milestones

- **PR #42** — migrated Chat HTTP presentation to Razor Pages while preserving `/Chat`, RAG behavior and SSE handlers.
- **PR #43** — introduced `IChatPageService` so Chat PageModel page/session data no longer directly depends on `ApplicationDbContext`.
- **PR #46** — migrated the remaining MVC Controllers/Views to Razor Pages, removed direct `ApplicationDbContext` usage from PageModels via purpose-specific services, added authorized ManagementHub realtime for Documents, Chapters, Subjects, Subject Leader assignments and Users/roles, and verified the Release build/tests.

Credit for these cross-cutting milestones follows actual merged authorship/review history rather than nominal workflow ownership.

## Billing/reporting milestones

- **PR #53** — VNPay billing and concurrency-safe account-level RAG quota management.
- **PR #54** — distinct Chat 429/rate-limit handling.
- **PR #55** — Admin billing analytics integrated with the reporting area.
- **PR #56** — verified VNPay return fallback finalization when IPN is missing.

These are cross-cutting product/platform changes; contribution identity should follow the actual merged Git history.

## Workflow contribution summary

| Workflow / area | Maintenance/assigned owner(s) | Merged contribution highlights |
|---|---|---|
| Core/Data/Identity/RBAC | Member 1 | Member 1 baseline + later cross-cutting merges |
| Multi-subject/security integration | Member 1 | Member 1 baseline + PR #46 integration |
| AI provider/deployment infrastructure | Member 1 | PR #21/#28/#37/#38/#39 |
| Flow 1 request/business behavior | Member 2 | Member 2 baseline + PR #46 presentation migration |
| Flow 1 indexing/ingestion maintenance | Member 3 | existing merged contributors; Member 3 maintains |
| Cross-app UI/UX baseline | Member 3 | PR #19 + later presentation migration |
| Flow 2 RAG backend | Member 4 | PR #30 + later integrated enhancements |
| Flow 2 product UI/evaluation | Member 5 | PR #34/#35 historical layer + PR #42/#46 migrations |
| Chat Razor Pages architecture | cross-cutting | PR #42/#43 |
| Management realtime / Razor Pages completion | cross-cutting | PR #46 |
| Flow 3 academic Report & Statistics | Member 2 | Member 2 baseline + PR #40 integration |
| Billing/quota + billing analytics | cross-cutting | PR #53/#55/#56 |
| Repository docs/coordination | Member 1 | ongoing |

## Current follow-up debt

- deeper document-ingestion/RAG quality validation;
- durable hosted source-file storage beyond free ephemeral storage;
- keep ManagementHub authorization/group isolation and Chat SSE semantics covered by regression tests;
- keep billing/report documentation synchronized with persisted payment/quota semantics;
- update this ledger whenever future merges materially change ownership or contribution credit.
