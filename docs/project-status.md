# Project status

> Updated on 2026-08-21 after PR #42/#43.
>
> This file distinguishes the **implemented runtime** from the **accepted target architecture**. The current docs PR changes documentation only.

## Target presentation architecture

All HTTP UI/actions must converge on Razor Pages.

```text
Flow 1 Documents/Chapters: Razor Pages + SignalR notifications
Flow 2 Chat:               Razor Pages + SSE
Flow 2 Evaluation:         Razor Pages
Flow 3 Reports:            Razor Pages
Admin users/subjects:      Razor Pages
Subject catalogue:         Razor Pages
```

SignalR is only the realtime fan-out mechanism for Document Management. It does not replace Razor Page handlers or Chat SSE.

## Migration status

| Area | Target | Runtime status at this docs PR |
|---|---|---|
| Account/auth | Razor Pages | Complete |
| Chat/history/citations | Razor Pages + SSE | Complete after PR #42/#43 |
| Reports | Razor Pages + `IReportQueryService` | Complete |
| Documents/Chapters | Razor Pages + SignalR | Implementation pending |
| Evaluation | Razor Pages | Implementation pending |
| Admin users/subjects | Razor Pages | Implementation pending |
| Subject catalogue | Razor Pages | Implementation pending |
| Legacy MVC presentation removal | Removed | Implementation pending |

The migration is **not complete** until the follow-up code PR removes the legacy MVC presentation layer and controller routing after Razor Page parity is verified.

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

## Flow 1 target

```text
Document/Chapter Razor Page handler
 -> validate + authorize concrete Subject
 -> persist change through application/infrastructure boundary
 -> enqueue Document.Id when required
 -> publish realtime event

Document indexing worker
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunks/index status
 -> publish DocumentIndexStatusChanged
```

Document SignalR events should include:

```text
DocumentCreated
DocumentUpdated
DocumentDeleted
DocumentIndexStatusChanged
```

Writes remain Razor Page handlers with antiforgery and server-side authorization. SignalR broadcasts only after successful persistence.

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

A documentation-only PR does not validate the planned migration by itself. The implementation PR must add/update tests for Razor Page authorization/handlers and SignalR subject subscription/realtime behavior.

## Remaining technical debt

Primary follow-up work:

- complete all remaining legacy MVC -> Razor Pages migrations;
- remove MVC presentation registration/routing once no product surface depends on it;
- add the Document SignalR hub/notifier/client and reconnect behavior;
- preserve Chat SSE as-is;
- add Razor Page/SignalR authorization regression tests;
- deeper DOCX/PPTX/complex-PDF fixtures;
- durable hosted storage for uploaded source files.

Canonical migration specification: `razor-pages-signalr-architecture.md`.
