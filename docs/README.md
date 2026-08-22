# Documentation index

> Canonical documentation target updated on 2026-08-22 after the VNPay billing/quota integration and the billing analytics reporting follow-up.
>
> The target presentation architecture is Razor Pages only for HTTP UI/actions, with SSE retained for Chat and authorized SignalR management realtime for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles. PR #46 contains the management realtime implementation on its branch but is **not merged**; branch implementation state must not be read as merged `master` state.

## Start here

| Document | Purpose |
|---|---|
| `../README.md` | User-facing target architecture and migration status |
| `razor-pages-signalr-architecture.md` | Canonical presentation migration + management SignalR specification |
| `project-status.md` | Current runtime vs required target status |
| `infrastructure.md` | Target runtime boundaries, persistence, providers and realtime transports |
| `ai-provider-backup.md` | Provider selection, fallback and embedding compatibility |
| `render-deployment.md` | Current Render Blueprint/runtime configuration |
| `rag-demo-guide.md` | Flow 2/Document demo notes during the migration period |
| `role-access-control.md` | Roles, policies, Razor Page and SignalR authorization rules |
| `multi-subject-management.md` | Subject persistence and cross-flow scope |
| `flow-3-report-statistics-handoff.md` | Academic report + Admin billing analytics query boundaries |
| `report-statistics-metrics.md` | Academic/RAG report metric definitions and limitations |
| `billing-report-statistics.md` | Payment/quota metric formulas, scope and interpretation |
| `member-contributions.md` | Ownership vs merged contribution accounting |
| `team-workflow.md` | Team integration and maintenance boundaries |

## Presentation checkpoint

Required end state:

```text
Flow 1: Razor Pages Documents/Chapters + background indexing + ManagementHub
Flow 2: Razor Pages Chat/history/citations + Evaluation
Flow 3: Razor Pages Reports
  academic Subject report -> IReportQueryService
  Admin billing analytics -> IBillingReportQueryService
Admin/Subjects: Razor Pages

Chat browser updates: SSE
Management browser updates: SignalR notifications
```

Merged baseline:

```text
Chat: Razor Pages after PR #42
Chat page data/session persistence: IChatPageService after PR #43
Academic Reports/authentication: Razor Pages
VNPay account-level quota purchases: PR #53
```

Billing analytics follow-up keeps the academic report subject-scoped while exposing system-wide payment/quota aggregates only to Admin. Current checkout purchases global account quota and normally leaves `PaymentOrder.SubjectId` null, so revenue must not be assigned to the Subject currently being viewed.

PR #46 branch implementation state (not merged):

```text
PageModel/DbContext cleanup: retained
Management realtime: Documents, Chapters, Subjects, Subject Leader assignments, Users/roles
Management hub: authorized scoped subscriptions with reconnect/reload fallback
```

PR #46 must not be described as merged, and the Razor-Pages-only migration is not complete on `master` until the implementation is merged and any remaining legacy presentation/routing is verified.

## Flow references

- `razor-pages-signalr-architecture.md` - canonical migration and management realtime design.
- `flow-1-razor-pages-signalr.md` - Flow 1-specific Page/SignalR behavior.
- `flow-3-report-statistics-handoff.md` - Flow 3 academic and billing report boundaries.
- `report-statistics-metrics.md` - academic/RAG metric dictionary.
- `billing-report-statistics.md` - billing/quota metric dictionary.
- `member-4-rag-backend-handoff.md` - Flow 2 backend/integration handoff.
- `member-4-rag-status-2026-08.md` - August Flow 2 status snapshot.

## Historical documents

Handoff/contribution files may mention the technology used by older merged PRs as historical evidence. Historical implementation credit does not override the current target architecture.

`prn222-rag-assistant-documentation.txt` is the consolidated Vietnamese system reference. Canonical configuration values should still be checked against `.env.example`, `docker-compose.yml`, and `render.yaml`.

## Synchronization rule

When a PR changes architecture, workflow status, deployment/provider configuration, persistence, security, reporting scope, or ownership:

1. update the relevant canonical docs in the same PR when practical;
2. clearly separate **implemented runtime state** from **accepted target design**;
3. keep subject-scoped academic metrics separate from system-wide billing metrics unless persisted product semantics explicitly support attribution;
4. after the Razor Pages migration implementation lands, reconcile all canonical docs again and remove migration-pending warnings;
5. keep PR numbers as audit evidence;
6. do not place real credentials in documentation.
