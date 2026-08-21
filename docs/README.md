# Documentation index

> Canonical documentation set synchronized with `master` after PR #40 on 2026-08-21.

This index exists to prevent older handoff/snapshot files from silently becoming the source of truth after later merges.

## Start here

| Document | Purpose |
|---|---|
| `../README.md` | User-facing architecture/status overview |
| `project-status.md` | Current merged milestone and remaining debt |
| `infrastructure.md` | Runtime architecture, persistence, providers and deployment boundaries |
| `ai-provider-backup.md` | Provider selection, fallback and embedding compatibility |
| `render-deployment.md` | Current Render Blueprint/runtime configuration |
| `rag-demo-guide.md` | Current Flow 2 Chat + Evaluation demo procedure |
| `role-access-control.md` | Roles, policies and resource authorization |
| `multi-subject-management.md` | Subject persistence and cross-flow scope |
| `member-contributions.md` | Ownership vs merged contribution accounting |
| `team-workflow.md` | Team integration and maintenance boundaries |

## Flow references

- `flow-1-mvc-migration.md` - current Flow 1 MVC boundary.
- `flow-3-report-statistics-handoff.md` - current Flow 3 query/report boundary.
- `member-4-rag-backend-handoff.md` - current Flow 2 backend/integration handoff.
- `member-4-rag-status-2026-08.md` - August status snapshot, refreshed post-PR #40.

## Current architecture checkpoint

```text
Flow 1: MVC Documents/Chapters + background indexing        COMPLETE
Flow 2: MVC Chat/history/citations + Evaluation + RAG       COMPLETE
Flow 3: Razor Pages Reports -> IReportQueryService           COMPLETE

Chat browser updates: SSE (not SignalR)
Render chat: Gemini
Render embeddings: OpenRouter / 1024 dimensions
Data Protection keys: PostgreSQL
Report chat metrics: subject-scoped
```

## Historical documents

Files named `handoff` or dated `status` may preserve context about ownership and previous milestones, but statements inside them must match the current baseline unless explicitly labeled historical/superseded.

`prn222-rag-assistant-documentation.txt` is a consolidated Vietnamese system reference; canonical configuration values should still be checked against `.env.example`, `docker-compose.yml`, and `render.yaml`.

## Synchronization rule

When a PR changes architecture, workflow status, deployment/provider configuration, persistence, security, or ownership:

1. update the relevant canonical docs in the same PR when practical;
2. if code merges first, create one reconciliation PR against current `master`;
3. prefer describing the behavior verified in source/config over repeating old PR plans;
4. keep PR numbers as audit evidence;
5. do not place real credentials in documentation.
