# Documentation index

> Canonical documentation verified against `master` on 2026-08-22 after PR #46, #53, #54, #55 and #56.

## Current architecture checkpoint

```text
HTTP product/admin UI + actions -> Razor Pages
Chat progress/result             -> SSE
Management realtime              -> authorized SignalR ManagementHub
Document indexing                -> hosted background worker/services
Academic reporting               -> subject-scoped IReportQueryService
Billing analytics                -> Admin-only IBillingReportQueryService
Billing/quota                    -> VNPay + account-level quota
```

PR #46 is merged: the remaining MVC product/admin surfaces were migrated to Razor Pages, direct `ApplicationDbContext` usage was removed from PageModels through purpose-specific services, legacy MVC Views were removed, and ManagementHub realtime was added for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles.

PR #53 added VNPay billing and concurrency-safe account-level RAG quota management. PR #54 distinguishes 429/rate-limit failures from no-document/no-evidence behavior. PR #55 added Admin billing analytics. PR #56 finalizes verified successful VNPay return flows when IPN is missing while keeping persisted order state authoritative.

## Start here

| Document | Purpose |
|---|---|
| `../README.md` | Current system overview, runtime architecture and setup |
| `project-status.md` | Merged runtime status and remaining technical debt |
| `razor-pages-signalr-architecture.md` | Implemented Razor Pages + ManagementHub architecture |
| `infrastructure.md` | Runtime boundaries, persistence, providers and realtime transports |
| `role-access-control.md` | Roles, policies, Razor Page and SignalR authorization |
| `multi-subject-management.md` | Subject persistence and cross-flow isolation |
| `agentic-rag.md` | Agentic RAG design/behavior notes |
| `ai-provider-backup.md` | Provider selection, fallback and embedding compatibility |
| `rag-demo-guide.md` | RAG/Chat demo and validation workflow |
| `payment-integration-vnpay.md` | VNPay checkout, return/IPN and quota integration |
| `report-statistics-metrics.md` | Academic/RAG metric definitions and limitations |
| `billing-report-statistics.md` | Payment/quota metric formulas and interpretation |
| `flow-3-report-statistics-handoff.md` | Academic + billing report query boundaries |
| `render-deployment.md` | Render Blueprint/runtime configuration |
| `member-contributions.md` | Ownership and merged contribution accounting |
| `team-workflow.md` | Team integration and maintenance boundaries |

## Flow references

- `flow-1-razor-pages-signalr.md` — Flow 1 Razor Page + ManagementHub behavior.
- `flow-3-report-statistics-handoff.md` — Flow 3 academic/billing reporting boundaries.
- `member-1-core-data-handoff.md` — Core/data/security handoff.
- `member-2-document-management-handoff.md` — Flow 1 request/business behavior handoff.
- `member-3-document-indexing-handoff.md` — ingestion/indexing handoff.
- `member-3-ui-ux-handoff.md` — UI/UX handoff.
- `member-4-rag-backend-handoff.md` — Flow 2 RAG backend handoff.
- `member-4-rag-status-2026-08.md` — August Flow 2 status snapshot.

## Historical documents

Handoff/status files are historical evidence and may intentionally describe earlier implementation stages. They should not override `README.md`, `project-status.md`, `infrastructure.md`, or `razor-pages-signalr-architecture.md` when determining current runtime state.

`prn222-rag-assistant-documentation.txt` is a consolidated Vietnamese reference; verify changing configuration values against `.env.example`, `docker-compose.yml`, `render.yaml`, and the current source.

## Synchronization rules

When a PR changes architecture, workflow status, deployment/provider configuration, persistence, security, billing, reporting scope, or ownership:

1. update the relevant canonical docs in the same PR when practical;
2. describe merged runtime state separately from future design ideas;
3. keep Chat SSE separate from management SignalR;
4. keep subject-scoped academic metrics separate from system-wide billing metrics unless persisted product semantics support attribution;
5. treat persisted payment state as reporting truth and never document secrets;
6. keep PR numbers as audit evidence;
7. mark historical handoffs as historical instead of rewriting their original context.
