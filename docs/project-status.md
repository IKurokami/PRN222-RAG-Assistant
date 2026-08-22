# Project status

> Verified against `master` on 2026-08-22 after PR #46, #53, #54, #55 and #56.

## Current runtime status

| Area | Current implementation | Status |
|---|---|---|
| Account/auth | Razor Pages + ASP.NET Core Identity | Complete |
| Documents/Chapters | Razor Pages + subject-authorized ManagementHub notifications | Complete after PR #46 |
| Admin users/subjects | Razor Pages + ManagementHub | Complete after PR #46 |
| Subject catalogue | Razor Pages + ManagementHub | Complete after PR #46 |
| Chat/history/citations | Razor Pages + SSE + `IChatPageService` | Complete |
| Evaluation | Razor Pages + `IEvaluationService` | Complete after PR #46 |
| Academic Reports | Razor Pages + `IReportQueryService` | Complete |
| VNPay billing/quota | Razor Pages + `IBillingService` / account-level quota | Complete after PR #53 |
| Billing analytics | Admin-only Razor Pages + `IBillingReportQueryService` | Complete after PR #55 |
| Legacy MVC product presentation | Removed | Complete after PR #46 |

## Presentation architecture

```text
HTTP UI/actions     -> Razor Pages
Chat updates        -> SSE
Management updates  -> authorized SignalR ManagementHub
Indexing            -> hosted background worker/services
```

PR #46 completed the Razor-Pages-only product/admin migration and added authorized management realtime for Documents, Chapters, Subjects, Subject Leader assignments, and Users/roles. Writes remain Razor Page handlers/application services and broadcasts occur only after successful persistence.

## Important merged milestones

- **PR #32** — Render Blueprint CD.
- **PR #33** — Render pgvector/runtime fixes and optional seed behavior.
- **PR #34/#35** — Chat/Evaluation product integration, SSE UX, grounding/follow-up improvements.
- **PR #37** — embedding dimensionality transition safety for pgvector retrieval.
- **PR #38** — PostgreSQL-persisted Data Protection keys and OpenRouter chat fallback update.
- **PR #39** — Render Chat switched to Gemini while embeddings remain OpenRouter.
- **PR #40** — Reports moved behind `IReportQueryService`; chat aggregates became subject-scoped.
- **PR #42** — Chat presentation migrated to Razor Pages.
- **PR #43** — Chat page/session data moved behind `IChatPageService`.
- **PR #46** — remaining MVC Controllers/Views migrated to Razor Pages; PageModel/DbContext cleanup; authorized ManagementHub added; release build verified with 196 passing tests.
- **PR #53** — VNPay billing and concurrency-safe account-level RAG quota management.
- **PR #54** — Chat 429/rate-limit errors handled separately from no-document/no-evidence cases.
- **PR #55** — Admin billing report analytics added and linked from academic reporting.
- **PR #56** — verified successful VNPay return can finalize payment when IPN is missing.

## Flow 1 - Documents, Chapters and management realtime

```text
Razor Page handler
 -> validate + authorize policy/concrete Subject
 -> purpose-specific application service
 -> persist change
 -> enqueue Document.Id when required
 -> publish ManagementChanged after commit
```

Background indexing remains separate:

```text
IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunks/index state
 -> ManagementChanged(Document, IndexStatusChanged, Status)
```

ManagementHub uses scoped server-authorized subscriptions. SignalR is fan-out only and is never a CRUD API.

## Flow 2 - Chat, RAG and Evaluation

```text
Chat Razor Page
 -> IChatPageService
 -> IRagQueryService
 -> subject + dimension constrained pgvector retrieval
 -> grounded generation
 -> messages/citations persistence
 -> SSE progress/result rendering
```

Chat preserves typed error behavior so provider/rate-limit failures are not misreported as missing documents. Evaluation remains backed by `IEvaluationService` and the packaged 50-question dataset.

## Flow 3 - Academic and billing reporting

Academic reports remain subject-scoped:

```text
Pages/Reports/Index.cshtml.cs
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

Billing analytics is a separate system-wide Admin-only read model:

```text
Pages/Reports/Billing.cshtml.cs
 -> IBillingReportQueryService
 -> BillingReportQueryService
 -> ApplicationDbContext
```

The split is intentional. Academic corpus/chat/citation metrics remain subject-scoped. Current VNPay checkout grants account-level quota, so revenue/quota metrics must not be attributed to whichever Subject is being viewed unless a future product change persists explicit subject attribution.

Persisted `Paid` orders are confirmed revenue. Billing reporting also surfaces Pending/Failed health, stale Pending orders, quota/package mix, payment-channel mix, short-term revenue activity and non-PII recent-order data.

## Billing/quota behavior

PR #53 added concurrency-safe quota reservation/activation and VNPay payment handling. Credentials are environment-only secrets.

PR #56 adds a verified-return fallback: if VNPay's successful return is cryptographically valid and the IPN callback is absent, the application can finalize persisted payment/quota state. Reporting continues to read persisted state rather than trusting transient request parameters.

## Provider/runtime status

Supported AI providers remain Ollama, Gemini, OpenAI and OpenRouter with independent chat/embedding selection.

Current Render configuration documented by the repository:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimension: 1024
```

Changing embedding provider/model/dimension requires complete corpus re-indexing. Chat-only provider/model changes do not.

## CI/CD

```text
pull request / push
 -> GitHub Actions CI
 -> build + tests
 -> EF model/migration validation
 -> Docker Compose validation
 -> PostgreSQL/pgvector checks

master checks pass
 -> Render checksPass auto deploy
```

## Remaining technical debt

- durable hosted storage for uploaded source files instead of free-instance ephemeral storage;
- deeper DOCX/PPTX/complex-PDF ingestion fixtures and RAG quality validation;
- preserve strict subject isolation and ManagementHub group authorization as management features evolve;
- preserve Chat SSE and typed provider/rate-limit error semantics;
- only add subject-level revenue reporting if checkout later persists meaningful subject attribution;
- keep canonical documentation synchronized whenever provider, billing, reporting, deployment or architecture semantics change.

Canonical architecture: `razor-pages-signalr-architecture.md`.
