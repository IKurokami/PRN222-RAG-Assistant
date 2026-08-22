# Team workflow and ownership

> Updated on 2026-08-22 against merged `master`. PR #46 is merged and the Razor Pages + ManagementHub architecture is now the runtime baseline.

## Product architecture

```text
HTTP product/admin presentation -> Razor Pages
Chat progress/realtime          -> SSE
Management realtime             -> authorized SignalR ManagementHub
Document indexing               -> background worker/services
```

PR #46 completed the remaining MVC-to-Razor-Pages migration, removed direct DbContext usage from PageModels through purpose-specific services, and added management realtime for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles.

PR #53 added VNPay billing/account-level quota, PR #54 improved Chat 429 handling, PR #55 added Admin billing analytics, and PR #56 added verified VNPay return fallback finalization.

## Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Maintains domain/data/security baseline, shared contracts and EF coordination, Identity/RBAC, Subject management/authorization, provider/runtime configuration, deployment secrets/configuration, embedding compatibility rules, and repository documentation synchronization.

## Member 2 - Flow 1 request behavior + Flow 3 reporting

Maintains Chapter/Document request semantics, upload/list/details/edit/delete/re-index behavior, Flow 1 validation/authorization, and academic Report & Statistics behavior.

Management SignalR adds fan-out only after successful writes; it does not replace Razor Page handlers or application services. Academic reports stay subject-scoped while billing analytics stays in its separate Admin-only query boundary.

## Member 3 - indexing maintenance + cross-app UI baseline

Maintains PDF/DOCX/PPTX parsers, chunking/indexing worker/service, indexing transitions/startup recovery, and cross-app UI/UX baseline. Index state transitions publish Document `IndexStatusChanged` through the management notifier without moving indexing work into SignalR.

## Member 4 - Flow 2 RAG backend maintenance

Maintains subject-scoped RAG behavior, retrieval/grounding/no-evidence semantics, provider/rate-limit error semantics, message/citation persistence, session ownership/subject validation, and backend configuration/tests. Concrete providers remain outside the RAG workflow boundary.

## Member 5 - Flow 2 product presentation/evaluation

Historical product implementation credit remains tied to PR #34/#35. Chat was migrated to Razor Pages in PR #42 and its PageModel boundary improved in PR #43. Evaluation is now also part of the Razor Pages product surface after PR #46.

## Management realtime work map

```text
Razor Page handler writes
 -> policy + concrete-subject authorization
 -> persistence commit
 -> IManagementRealtimeNotifier
 -> ManagementHub / ManagementChanged
 -> authorized scoped clients
```

Managed resources include Document, Chapter, Subject, SubjectLeaderAssignments and User. Clients reconnect automatically and reload authorized state when an event is insufficient. Chat remains Razor Pages + SSE and must not move to SignalR merely for uniformity.

## Review rules

Review architectural changes for functional parity, no reintroduced MVC product surface, server-side authorization, antiforgery, PageModel/service boundaries, ManagementHub isolation, post-commit notifications, Chat SSE/error semantics, correct billing/payment persistence semantics, Admin-only billing analytics, updated routing/navigation, and green build/test/EF/PostgreSQL/Docker checks.

## Provider change procedure

Chat-only provider/model changes do not require corpus re-indexing. Embedding provider/model/dimension changes require a complete corpus re-index; dimensional compatibility does not imply semantic embedding-space compatibility.

## Secrets

Real API/payment keys live only in local/deployment secret environments. Never place credentials in PR descriptions, screenshots, browser JavaScript, tracked appsettings, logs or documentation.

## Documentation workflow

Any PR that materially changes architecture, provider/runtime behavior, billing/quota semantics, authorization, reports, deployment, or ownership should update the related canonical docs in the same change when practical. Contribution accounting uses auditable PR numbers; see `member-contributions.md`.
