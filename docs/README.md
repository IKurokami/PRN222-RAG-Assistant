# Documentation index

> Canonical documentation target updated on 2026-08-21 after PR #42/#43.
>
> The target presentation architecture is Razor Pages only for HTTP UI/actions, with SSE retained for Chat and SignalR added for Document Management realtime notifications. The documentation PR does not implement the remaining runtime migration.

## Start here

| Document | Purpose |
|---|---|
| `../README.md` | User-facing target architecture and migration status |
| `razor-pages-signalr-architecture.md` | Canonical presentation migration + Document SignalR specification |
| `project-status.md` | Current runtime vs required target status |
| `infrastructure.md` | Target runtime boundaries, persistence, providers and realtime transports |
| `ai-provider-backup.md` | Provider selection, fallback and embedding compatibility |
| `render-deployment.md` | Current Render Blueprint/runtime configuration |
| `rag-demo-guide.md` | Flow 2/Document demo notes during the migration period |
| `role-access-control.md` | Roles, policies, Razor Page and SignalR authorization rules |
| `multi-subject-management.md` | Subject persistence and cross-flow scope |
| `member-contributions.md` | Ownership vs merged contribution accounting |
| `team-workflow.md` | Team integration and maintenance boundaries |

## Presentation checkpoint

Required end state:

```text
Flow 1: Razor Pages Documents/Chapters + background indexing + SignalR
Flow 2: Razor Pages Chat/history/citations + Evaluation
Flow 3: Razor Pages Reports -> IReportQueryService
Admin/Subjects: Razor Pages

Chat browser updates: SSE
Document browser updates: SignalR notifications
```

Current implementation checkpoint at this docs PR:

```text
Chat: Razor Pages after PR #42
Chat page data/session persistence: IChatPageService after PR #43
Reports/authentication: Razor Pages
Remaining legacy MVC product/admin surfaces: code migration still pending
```

Do not mark the Razor-Pages-only migration complete until the implementation PR removes the legacy presentation layer and conventional controller routing.

## Flow references

- `razor-pages-signalr-architecture.md` - canonical migration and realtime design.
- `flow-1-razor-pages-signalr.md` - Flow 1-specific Page/SignalR behavior.
- `flow-3-report-statistics-handoff.md` - Flow 3 query/report boundary.
- `member-4-rag-backend-handoff.md` - Flow 2 backend/integration handoff.
- `member-4-rag-status-2026-08.md` - August Flow 2 status snapshot.

## Historical documents

Handoff/contribution files may mention the technology used by older merged PRs as historical evidence. Historical implementation credit does not override the current target architecture.

`prn222-rag-assistant-documentation.txt` is the consolidated Vietnamese system reference. Canonical configuration values should still be checked against `.env.example`, `docker-compose.yml`, and `render.yaml`.

## Synchronization rule

When a PR changes architecture, workflow status, deployment/provider configuration, persistence, security, or ownership:

1. update the relevant canonical docs in the same PR when practical;
2. clearly separate **implemented runtime state** from **accepted target design**;
3. after the Razor Pages migration implementation lands, reconcile all canonical docs again and remove migration-pending warnings;
4. keep PR numbers as audit evidence;
5. do not place real credentials in documentation.
