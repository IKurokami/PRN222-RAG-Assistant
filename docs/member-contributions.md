# Member contribution ledger

> Updated baseline: `master` after PR #43 on 2026-08-21.
>
> This file intentionally uses Member numbers only. Ownership and merged contribution credit are tracked separately.

## Accounting rule

- **Ownership** = scope a member is responsible for maintaining.
- **Contribution credit** = merged implementation/review/integration work actually delivered.
- Documentation-only target decisions do not count as implementation of the planned Razor Pages/SignalR migration.
- Keep PR numbers as auditable evidence.

## Member 1

Maintains/delivered major cross-cutting platform work including Core/Data/Identity/RBAC, multi-subject management, provider/runtime configuration, deployment integration, schema/contract coordination, and repository documentation synchronization.

Representative merged work includes PR #4, #17, #18, #21, #28, #37, #38 and #39.

## Member 2

Member 2 delivered the main request/business behavior for Flow 1 and the original Flow 3 reporting behavior:

- Document upload/list/details/edit/delete/re-index semantics;
- Chapter management behavior;
- Flow 1 validation/authorization behavior;
- Report & Statistics dashboard behavior/tests.

Representative merged work: PR #5 and PR #12.

PR #40 later refactored Report data access behind `IReportQueryService` and made Chat aggregates subject-scoped. Member 2 retains behavior ownership.

The planned Flow 1 Razor Pages + SignalR migration must preserve these semantics; this docs PR does not yet add implementation credit for that migration.

## Member 3

Member 3 delivered the cross-application UI/UX baseline in PR #19 and remains the maintenance owner for document indexing/ingestion.

SignalR status-notification integration is future implementation work and should be credited to the member(s) who actually deliver the merged code.

## Member 4

Member 4 delivered the merged Flow 2 RAG backend baseline in PR #30, including subject-scoped retrieval/session behavior, grounding/no-evidence logic, provider-neutral calls, message/citation persistence and backend tests.

Member 4 remains the maintenance owner for core Flow 2 RAG behavior.

## Member 5

Member 5 has merged product implementation credit for the original Flow 2 product layer:

### PR #34

- original Chat product UI/session/history/citations;
- 50-question Evaluation Suite and UI/service integration.

### PR #35

- full-screen Chat UX;
- SSE progress/typewriter experience;
- Markdown/code rendering;
- citation reader/source presentation;
- grounding/follow-up enhancements.

These entries remain historical contribution credit even though the repository presentation target is now Razor Pages only.

## Post-PR #40 presentation milestones

### PR #42

Migrated Chat HTTP presentation to Razor Pages while preserving the `/Chat` URL, RAG behavior and SSE handlers.

### PR #43

Introduced `IChatPageService` so Chat PageModel page/session data access no longer depends directly on `ApplicationDbContext`.

These are cross-cutting presentation architecture milestones. Ownership/credit should follow the actual merged authorship/review history rather than being inferred from nominal workflow ownership.

## Accepted presentation target (documentation only)

The repository now documents this required end state:

```text
HTTP UI/actions               -> Razor Pages only
Chat progress/result          -> SSE
Document Management realtime -> SignalR notifications
```

Remaining implementation work includes migrating Documents/Chapters, Evaluation, Admin users/subjects and Subject catalogue to Razor Pages, removing the legacy MVC presentation/routing, and adding the subject-scoped Document SignalR channel.

This documentation PR must **not** be recorded as completion of those code changes.

## Workflow contribution summary

| Workflow / area | Maintenance/assigned owner(s) | Merged contribution highlights |
|---|---|---|
| Core/Data/Identity/RBAC | Member 1 | Member 1 |
| Multi-subject/security integration | Member 1 | Member 1 |
| AI provider/deployment infrastructure | Member 1 | PR #21/#28/#37/#38/#39 |
| Flow 1 request/business behavior | Member 2 | Member 2 baseline |
| Flow 1 indexing/ingestion maintenance | Member 3 | existing merged contributors; Member 3 maintains |
| Cross-app UI/UX baseline | Member 3 | PR #19 |
| Flow 2 RAG backend | Member 4 | PR #30 + later integrated enhancements |
| Flow 2 product UI/evaluation | Member 5 | PR #34/#35 historical product layer |
| Chat Razor Pages architecture | cross-cutting | PR #42/#43 |
| Flow 3 Report & Statistics | Member 2 | Member 2 baseline + PR #40 integration |
| Repository docs/coordination | Member 1 | Member 1 |

## Current follow-up debt

- complete remaining Razor Pages presentation migration;
- remove legacy MVC presentation/routing after parity;
- implement Document SignalR create/update/delete/index-status notifications with subject authorization;
- preserve Chat SSE;
- deeper document-ingestion/RAG quality validation;
- hosted source-file durability beyond free ephemeral storage.

Update this ledger when future merges materially change ownership or contribution credit.
