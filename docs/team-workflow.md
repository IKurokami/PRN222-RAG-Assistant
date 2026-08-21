# Team workflow and ownership

> Updated on 2026-08-21 for the PR #46/issue #47 management realtime implementation branch. Ownership and merged contribution credit remain separate; PR #46 is not merged.

## Product milestone and presentation migration

The core product behaviors exist, and the presentation architecture is converging on:

```text
HTTP product/admin presentation -> Razor Pages only
Chat progress/realtime          -> SSE
Management realtime             -> authorized SignalR notifications
```

Chat is already Razor Pages after merged PR #42/#43. PR #46 retains the completed PageModel/DbContext cleanup and implements authorized management fan-out for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles on its branch. Do not describe that branch implementation as merged.

## Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Maintains:

- domain/data/security baseline;
- shared contracts and EF migration coordination;
- Identity/RBAC;
- Subject management/assignment and authorization;
- provider selection/configuration/adapters;
- deployment/provider secret/config coordination;
- embedding compatibility/re-index rules;
- repository documentation synchronization.

For the presentation migration, Member 1 coordinates architecture/security/documentation consistency, especially subject authorization and removal of stale architecture claims after code lands.

## Member 2 - Flow 1 request behavior + Flow 3 reporting

Maintains established behavior for:

- Chapter/Document request semantics;
- upload/list/details/edit/delete/re-index;
- validation/authorization around Flow 1 requests;
- read-only Report & Statistics behavior.

The Flow 1 Razor Pages migration must preserve these semantics. Management SignalR only adds authorized realtime fan-out after successful changes; it does not replace handlers or application-facing writes.

## Member 3 - indexing maintenance + cross-app UI baseline

Maintains:

- PDF/DOCX/PPTX parsers;
- chunking/indexing worker/service;
- indexing state transitions/startup recovery;
- cross-application UI/UX baseline.

For SignalR, indexing status transitions should publish Document `IndexStatusChanged` notifications through the management notifier without moving indexing work into the realtime hub.

## Member 4 - Flow 2 RAG backend maintenance

Maintains:

- subject-scoped RAG query behavior;
- retrieval/grounding/no-evidence semantics;
- message/citation persistence;
- session ownership/subject validation;
- backend configuration/tests.

Concrete providers remain outside the RAG workflow boundary.

## Member 5 - Flow 2 product presentation/evaluation

Historical product implementation credit remains tied to PR #34/#35. Chat was subsequently migrated to Razor Pages in PR #42 and its PageModel boundary improved in PR #43.

Target presentation:

- Chat stays Razor Pages + SSE;
- Evaluation migrates to Razor Pages;
- no parallel product MVC surface remains after the migration.

## Management realtime work map

```text
Razor Page handler writes
  -> policy + concrete-subject authorization
  -> successful persistence commit
  -> IManagementRealtimeNotifier
  -> ManagementHub / ManagementChanged
  -> authorized scoped clients
```

Managed resources:

```text
Document, Chapter, Subject, SubjectLeaderAssignments, User
```

ManagementHub subscriptions use `subject:{guid:D}`, `admin:users`, `admin:subjects`, and `subjects:catalog` through `SubscribeToSubject(Guid)`, `SubscribeToAdminUsers()`, `SubscribeToAdminSubjects()`, and `SubscribeToSubjectCatalog()`. Clients reconnect automatically and reload authorized state when an event is insufficient.

Chat remains:

```text
Razor Pages
  -> existing SSE progress/result contract
```

SignalR must never become a Chat transport.

## Migration review rules

A code PR implementing this architecture should be reviewed for:

- functional parity with the replaced product/admin surfaces;
- no duplicate MVC + Razor Page product implementations left behind;
- server-side role/policy and concrete-subject authorization;
- antiforgery on state-changing Page Handlers;
- PageModel/service boundaries instead of provider/pgvector logic in presentation;
- ManagementHub group isolation and authorized subscription methods;
- ManagementChanged fan-out occurring only after successful writes;
- Document index-status notifications retaining their status payload;
- automatic reconnect and reload fallback on stale/insufficient events;
- Chat SSE remaining unchanged and no SignalR Chat migration;
- updated navigation/forms using Razor Page routing;
- build/test/EF/PostgreSQL/Docker checks.

## Provider change procedure

Chat-only changes do not require corpus re-indexing.

Embedding provider/model/dimension changes require a complete corpus re-index. PR #37 allows different dimensions to coexist temporarily during the transition but does not make different embedding semantic spaces compatible.

## Secrets

Real API keys live only in local/deployment secret environments. Do not put keys in PR descriptions, screenshots, browser JavaScript, tracked appsettings, logs or docs.

## Documentation workflow

After PR #46 lands, reconcile canonical docs against actual source and remove migration-pending warnings only when legacy MVC presentation/routing is actually gone. Until then, keep branch implementation state separate from merged status.

Contribution accounting uses Member numbers and auditable PR numbers; see `member-contributions.md`.
