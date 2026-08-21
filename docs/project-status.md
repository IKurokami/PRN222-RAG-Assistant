# Project status

> Updated on 2026-08-21 for the PR #46/issue #47 implementation branch.
>
> This file distinguishes the **merged `master` state** from the **PR #46 branch state**. PR #46 is **not merged**; branch implementation notes are not merged contribution claims.

## Target presentation architecture

All HTTP UI/actions must converge on Razor Pages.

```text
Flow 1 Documents/Chapters: Razor Pages + authorized ManagementHub
Flow 2 Chat:               Razor Pages + SSE
Flow 2 Evaluation:         Razor Pages
Flow 3 Reports:            Razor Pages
Admin users/subjects:      Razor Pages
Subject catalogue:         Razor Pages
```

SignalR is the authorized management fan-out mechanism for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles. It does not replace Razor Page handlers or Chat SSE.

## Migration status

| Area | Target | Runtime status |
|---|---|---|
| Account/auth | Razor Pages | Complete |
| Chat/history/citations | Razor Pages + SSE | Complete after PR #42/#43 |
| Reports | Razor Pages + `IReportQueryService` | Complete |
| Documents/Chapters | Razor Pages + ManagementHub | PR #46 branch implementation; not merged |
| Evaluation | Razor Pages | Implementation pending |
| Admin users/subjects | Razor Pages + ManagementHub | PR #46 branch implementation; not merged |
| Subject catalogue | Razor Pages + ManagementHub | PR #46 branch implementation; not merged |
| Legacy MVC presentation removal | Removed | Verify on PR #46 branch; merge pending |

The migration is **not complete on `master`** until the follow-up code PR merges and any remaining legacy MVC presentation/controller routing is verified after Razor Page parity.

## Relevant merged milestones

- **PR #32** - Render Blueprint CD.
- **PR #33** - Render pgvector/runtime fixes and optional seed behavior.
- **PR #34/#35** - original product Chat/Evaluation integration, SSE UX, grounding/follow-up improvements.
- **PR #37** - Gemini embedding dimensionality fix and pgvector dimension-safe re-index transition.
- **PR #38** - PostgreSQL-persisted Data Protection keys and OpenRouter chat fallback update.
- **PR #39** - Render Chat switched to Gemini while embeddings remain OpenRouter.
- **PR #40** - Reports moved behind `IReportQueryService`; report chat aggregates became subject-scoped.
- **PR #42** - Chat product presentation migrated to Razor Pages.
- **PR #43** - Chat PageModel direct DbContext usage replaced by `IChatPageService`.

## PR #46 implementation state (branch only)

The branch retains the completed PageModel/DbContext cleanup and implements issue #47 authorized management realtime for:

```text
Documents
Chapters
Subjects
Subject Leader assignments
Users/roles
```

The implementation uses Razor Page handlers for writes, server-side policy and concrete-subject authorization, post-commit `IManagementRealtimeNotifier` broadcasts, scoped ManagementHub groups, automatic reconnect, and reload fallback. This is implementation state only; PR #46 is not merged.

## Flow 1 target and branch behavior

```text
management Razor Page handler
 -> validate + authorize policy/concrete Subject
 -> persist change
 -> enqueue Document.Id when required
 -> commit succeeds
 -> publish ManagementChanged through ManagementHub
```

Document indexing remains separate:

```text
Document indexing worker
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunks/index status
 -> publish (Document, IndexStatusChanged, Status)
```

The common management event envelope carries `Resource`, `Change`, `EntityId`, optional `SubjectId`, and optional `Status`. Resources are Document, Chapter, Subject, SubjectLeaderAssignments, and User; changes are Created, Updated, Deleted, IndexStatusChanged, AssignmentsChanged, and RoleChanged.

Writes remain Razor Page handlers with antiforgery and server-side authorization. ManagementHub broadcasts only after successful persistence.

## Flow 2

Chat is now Razor Pages and keeps SSE for progress/result rendering.

```text
Chat Razor Page
 -> IChatPageService for page/session data
 -> IRagQueryService for RAG
 -> subject + dimension constrained pgvector retrieval
 -> grounded generation
 -> citations/messages persistence
 -> SSE progress/result rendering
```

Evaluation must migrate to Razor Pages in the follow-up presentation PR while preserving `IEvaluationService` behavior and the 50-question dataset.

## Flow 3

Reports remain:

```text
Pages/Reports/Index.cshtml.cs
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

All metrics remain subject-scoped.

## Provider/runtime status

Supported providers remain Ollama, Gemini, OpenAI and OpenRouter with independent chat/embedding selection.

Current Render split:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimension: 1024
```

Changing any embedding provider/model/dimension still requires complete corpus re-indexing.

## CI/CD

```text
pull request / push
 -> GitHub Actions CI
 -> build + tests
 -> ApplicationDbContext model/migration validation
 -> Docker Compose validation
 -> PostgreSQL/pgvector checks

master checks pass
 -> Render checksPass auto deploy
```

The PR #46 branch includes the management realtime implementation and must add/update regression coverage for Razor Page authorization/handlers and ManagementHub subscription/realtime behavior before merge. This branch status does not claim PR #46 is merged.

## Remaining technical debt

Primary follow-up work:

- merge PR #46 after review and validation, then reconcile canonical docs against the merged source;
- complete/remove any remaining legacy MVC presentation registration/routing after parity;
- preserve authorized ManagementHub group isolation and post-commit fan-out;
- preserve Chat SSE as-is and prohibit a SignalR Chat migration;
- add/retain Razor Page and management realtime authorization regression tests;
- deeper DOCX/PPTX/complex-PDF fixtures;
- durable hosted storage for uploaded source files.

Canonical migration specification: `razor-pages-signalr-architecture.md`.
