# Project status

> Synchronized with `master` after PR #40 on 2026-08-21.

## Workflow status

| Workflow | Presentation | Status |
|---|---|---|
| Flow 1 - Document Management & Indexing | MVC + background worker | Complete |
| Flow 2 - RAG Q&A + Conversation Management | MVC Chat + Evaluation | Complete |
| Flow 3 - Report & Statistics | Razor Pages + query service | Complete |

Conversation History is part of Flow 2.

## Milestones since the previous documentation baseline

The previous canonical docs were mostly synchronized after PR #30. The following later merges materially changed the system and are now included in this baseline:

- **PR #32** - Render Blueprint CD.
- **PR #33** - Render pgvector type reload, runtime dependency fix and optional seed-account behavior.
- **PR #34** - Flow 2 MVC Chat/session/history/citation UI and 50-question Evaluation Suite.
- **PR #35** - full-screen Chat redesign, SSE progress/typewriter experience, Markdown/citation reader, stronger grounding and contextual follow-up retrieval; obsolete RagDemo removed.
- **PR #37** - Gemini embedding dimensionality fix and pgvector dimension-safe re-index transition.
- **PR #38** - PostgreSQL-persisted ASP.NET Core Data Protection keys and expanded OpenRouter chat fallback chain.
- **PR #39** - Render Chat switched to Gemini while embeddings remain OpenRouter.
- **PR #40** - Flow 3 reporting moved behind `IReportQueryService`; chat/report aggregates are subject-scoped.

## Flow 1

Complete MVC request flow plus process-local background indexing:

```text
Document/Chapter MVC
 -> IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> parser/chunker
 -> ITextEmbeddingService
 -> DocumentChunks / index state
```

Issue #27 remains closed. Deferred quality debt is deeper complex DOCX/PPTX/PDF layout coverage rather than a missing core workflow.

## Flow 2

Complete product path:

```text
MVC Chat
 -> subject-aware session
 -> IRagQueryService
 -> embedding
 -> subject + dimension constrained pgvector retrieval
 -> grounded generation
 -> citations/messages persistence
 -> SSE progress/result rendering
```

Also complete:

- chat-session history and deletion;
- subject switching;
- citation pills/reader;
- Markdown rendering and code-copy support;
- contextual follow-up retrieval fallback;
- 50-question evaluation UI/service integration.

The current chat transport is SSE over a fetch POST. No SignalR hub is part of this flow.

## Flow 3

Complete read-only subject-scoped reporting.

PR #40 architecture:

```text
Pages/Reports/Index.cshtml.cs
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

The snapshot contains subject-scoped Chapter/Document/index metrics plus subject-scoped ChatSession/ChatMessage/MessageCitation totals.

## Provider/runtime status

Supported providers remain Ollama, Gemini, OpenAI and OpenRouter with independent chat/embedding selection.

Current Render split:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimension: 1024
```

Local Docker can still select Ollama or other configured providers. OpenRouter chat retains its ordered fallback chain when OpenRouter chat is selected outside the Render override.

## Embedding transition behavior

Complete re-index remains required after any embedding provider/model/dimension change.

PR #37 prevents mixed-dimension transition failures by filtering stored vectors to `vector_dims(Embedding) == query dimensions` before cosine distance. Different-dimension old rows are temporarily excluded. Same-dimension vectors from different embedding models are still semantically incompatible and must not be intentionally mixed.

## Authentication/runtime durability

PR #38 adds `DataProtectionKeyDbContext` and persists ASP.NET Core Data Protection keys in PostgreSQL. This removes the former Render warning that every web-container restart necessarily invalidates the key ring.

Uploaded source files on the free Render web service remain ephemeral; PostgreSQL durability does not make `/app/storage/uploads` durable.

## CI/CD

```text
pull request / push
 -> GitHub Actions CI
 -> build + tests
 -> ApplicationDbContext model/migration validation
 -> Docker Compose validation
 -> real PostgreSQL/pgvector checks
 -> DataProtectionKeys schema check
 -> mixed-dimension pgvector smoke test

master checks pass
 -> Render checksPass auto deploy
```

## Remaining technical debt

Current follow-up items are quality/production-hardening tasks, not missing core workflows:

- deeper DOCX/PPTX/complex-PDF ingestion fixtures;
- durable object/disk storage for uploaded source files on hosted deployments;
- production-grade Render sizing/database plan if the demo becomes long-lived;
- optional future refactors that further isolate MVC Chat/Evaluation data reads from direct EF use;
- optional provider-native token streaming if desired; current SSE is application-level progress/typewriter output.

## Documentation ownership

Member 1 coordinates repository documentation synchronization. See `member-contributions.md` for actual merged contribution credit through PR #40.
