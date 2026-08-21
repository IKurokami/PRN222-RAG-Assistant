# Agent Instructions

## Scope

This file applies to the entire repository. Deeper `AGENTS.md` files add rules for their subtree.

Before changing workflow, presentation, ingestion, RAG, reporting, deployment, or provider code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/README.md
docs/razor-pages-signalr-architecture.md
docs/project-status.md
docs/team-workflow.md
docs/member-contributions.md
docs/role-access-control.md
docs/multi-subject-management.md
docs/infrastructure.md
docs/ai-provider-backup.md
docs/render-deployment.md
```

## Architecture target

Documentation target accepted on 2026-08-21 after PR #42/#43.

- Main project: `src/PRN222.RagAssistant`
- Tests: `tests/PRN222.RagAssistant.Tests`
- Target: `net10.0`
- HTTP presentation target: **Razor Pages only**
- Chat realtime/progress transport: **SSE**
- Document Management realtime transport: **SignalR notifications**
- Auth: ASP.NET Core Identity
- Roles: `Admin`, `SubjectLeader`, `Student`
- Policies: `ManageUsers`, `ManageSubjects`, `ManageDocuments`
- Database: PostgreSQL + pgvector
- AI runtime: provider-neutral; Ollama/Gemini/OpenAI/OpenRouter
- Source storage: `storage/uploads/`

This is a target architecture rule, not a claim that every runtime surface has already been migrated. The remaining legacy MVC presentation layer is implementation debt for a follow-up code PR.

## Presentation rules

All product HTTP UI/actions must converge on Razor Pages.

Required page families:

```text
Pages/Account
Pages/Admin/Users
Pages/Admin/Subjects
Pages/Subjects
Pages/Chapters
Pages/Documents
Pages/Chat
Pages/Evaluation
Pages/Reports
```

Do not add new MVC product controllers/views. When migrating an existing surface, remove the old presentation path after parity is verified instead of keeping duplicate MVC + Razor Page implementations.

PageModels own HTTP concerns and should call purpose-specific Application-facing services where practical rather than growing direct EF/provider logic.

## Document SignalR rule

SignalR is allowed specifically for Document Management realtime fan-out.

Target pattern:

```text
Razor Page handler
 -> authorize + validate + persist
 -> commit succeeds
 -> realtime notifier / IHubContext
 -> subject-scoped SignalR clients
```

SignalR must not become the CRUD API. Create/edit/delete/re-index requests remain Razor Page handlers with normal antiforgery and server-side authorization.

Recommended events:

```text
DocumentCreated
DocumentUpdated
DocumentDeleted
DocumentIndexStatusChanged
```

Connections/groups must be authorized against concrete subject access. A client-supplied subject ID is never sufficient authorization.

## Chat transport rule

Chat remains Razor Pages + SSE. PR #42 migrated Chat to `Pages/Chat`; PR #43 moved its page data/session mutation boundary behind `IChatPageService`.

Do not replace Chat SSE with SignalR as part of the Document Management realtime work.

Grounded retrieval must preserve:

```text
selected Subject
 -> validated ChatSession
 -> ITextEmbeddingService
 -> subject + vector-dimension constrained retrieval
 -> GroundedPromptBuilder
 -> IChatCompletionService
 -> referenced citations only
 -> messages/citations bound to session
```

## Provider-neutral boundary

Application/workflow code consumes:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete providers belong in Infrastructure. `RAG_PROVIDER` remains the backward-compatible default; `RAG_CHAT_PROVIDER` and `RAG_EMBEDDING_PROVIDER` may override the two purposes independently.

Changing embedding provider/model/dimension requires a complete corpus re-index. PR #37 allows dimension-changing transition periods without pgvector dimension errors, but different semantic vector spaces remain incompatible.

Chat-only provider/model changes do not require re-indexing.

## Multi-subject invariant

Subject is a first-class workflow boundary.

- `Chapter.SubjectId`, `Document.SubjectId`, and `ChatSession.SubjectId` carry persisted scope.
- Flow 1 Razor Page handlers preserve subject context and authorize concrete resources.
- Document SignalR subscriptions use the same concrete subject boundary.
- Flow 2 session/retrieval/persistence stays subject-scoped.
- Flow 3 report snapshots stay subject-scoped.
- Admin may manage any existing subject.
- Subject Leader may manage only assigned subjects.
- Student may use active subjects but cannot manage academic content.
- UI visibility is never authorization.

## Flow 1 rules

Target presentation is Razor Pages under `Pages/Documents` and `Pages/Chapters`.

- Writes require `ManageDocuments` plus subject-specific manage permission.
- Upload persists file + `Document`, then queues `Document.Id`.
- Page handlers do not parse/chunk/embed/query pgvector/call provider APIs.
- Indexing consumes `ITextEmbeddingService`.
- Startup recovery handles persisted `Uploaded`/`Processing` documents.
- Document CRUD/index status changes publish subject-scoped SignalR notifications after persistence succeeds.

## Flow 2 rules

Target presentation is Razor Pages under `Pages/Chat` and `Pages/Evaluation`.

- Chat uses `IRagQueryService` and `IChatPageService` boundaries.
- Evaluation uses `IEvaluationService`.
- Chat browser progress remains SSE.
- Do not recreate obsolete parallel demo/product surfaces.

## Flow 3 rules

Flow 3 remains read-only Razor Pages under `Pages/Reports/` and does not access EF Core directly.

```text
PageModel
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

Reports require concrete subject context plus subject-specific manage permission. Metrics remain subject-scoped.

## Shared contracts

Important Application-facing contracts include:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `IChatPageService`
- `IEvaluationService`
- `IReportQueryService`
- `RagAnswer` / `RagCitation`
- `SubjectReportSnapshot`

Provider-specific DTOs and EF/PostgreSQL implementation details stay outside Application.

## Render/runtime

Current Render uses Gemini Chat and OpenRouter embeddings. ASP.NET Core Data Protection keys persist in PostgreSQL.

Render web services support WebSocket connections, so the target Document SignalR hub is compatible with the current deployment model. Clients must implement reconnect behavior because instance replacement can close active connections.

## Infrastructure and hygiene

- Never commit `.env`, API keys, credentials, uploaded documents, DB dumps, logs, build output or runtime data.
- Default branch: `master`.
- Use focused branches/PRs.
- Local Ollama Compose uses `local-ai` profile.
- Run build/tests/pending-model/Docker/PostgreSQL validation before merge.
- Never run `docker compose down -v` unless data deletion is explicitly requested.

## Documentation rule

Repository coordination documentation uses Member numbers only. Ownership and merged contribution credit are separate concepts; use `docs/member-contributions.md` as the canonical ledger.

After the implementation PR completes the Razor Pages migration, reconcile canonical docs again and remove migration-pending language.
