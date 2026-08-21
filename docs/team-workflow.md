# Team workflow and ownership

> Updated on 2026-08-21 for the Razor Pages + SignalR target architecture. Ownership and merged contribution credit remain separate.

## Product milestone and presentation migration

The core product behaviors exist, but presentation architecture is undergoing a cleanup target:

```text
HTTP product/admin presentation -> Razor Pages only
Chat progress/realtime          -> SSE
Document Management realtime    -> SignalR notifications
```

Chat is already Razor Pages after PR #42/#43. Remaining legacy MVC product/admin surfaces must be migrated in a follow-up implementation PR before the presentation cleanup is complete.

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

The Flow 1 Razor Pages migration must preserve these semantics. SignalR only adds realtime fan-out after successful changes.

## Member 3 - indexing maintenance + cross-app UI baseline

Maintains:

- PDF/DOCX/PPTX parsers;
- chunking/indexing worker/service;
- indexing state transitions/startup recovery;
- cross-application UI/UX baseline.

For SignalR, indexing status transitions should publish document status notifications without moving indexing work into the realtime hub.

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

## Presentation migration work map

```text
Razor Pages migration
  Admin users/subjects
  Subject catalogue
  Documents/Chapters
  Evaluation

Document realtime
  Razor Page handler writes
    -> successful persistence
    -> realtime notifier / SignalR
    -> subject-scoped clients

Chat
  Razor Pages
    -> existing SSE progress/result contract
```

## Migration review rules

A code PR implementing this architecture should be reviewed for:

- functional parity with the replaced product/admin surfaces;
- no duplicate MVC + Razor Page product implementations left behind;
- server-side role/subject authorization;
- antiforgery on state-changing Page Handlers;
- PageModel/service boundaries instead of provider/pgvector logic in presentation;
- SignalR subject isolation and reconnect behavior;
- SignalR fan-out occurring only after successful writes;
- Chat SSE remaining unchanged;
- updated navigation/forms using Razor Page routing;
- build/test/EF/PostgreSQL/Docker checks.

## Provider change procedure

Chat-only changes do not require corpus re-indexing.

Embedding provider/model/dimension changes require a complete corpus re-index. PR #37 allows different dimensions to coexist temporarily during the transition but does not make different embedding semantic spaces compatible.

## Secrets

Real API keys live only in local/deployment secret environments. Do not put keys in PR descriptions, screenshots, browser JavaScript, tracked appsettings, logs or docs.

## Documentation workflow

After the implementation PR lands, reconcile canonical docs against actual source again and remove migration-pending warnings only when legacy MVC presentation/routing is actually gone.

Contribution accounting uses Member numbers and auditable PR numbers; see `member-contributions.md`.
