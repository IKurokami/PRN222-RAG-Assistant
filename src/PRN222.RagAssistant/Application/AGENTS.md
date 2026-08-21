# Application-layer instructions

> Synchronized with `master` after PR #40 on 2026-08-21.

This subtree contains provider-neutral, presentation-safe cross-workflow contracts/models. Keep it independent from MVC/Razor runtime types, provider-specific payloads, and PostgreSQL implementation details.

## Current workflow state

1. Flow 1 - Document Management & Indexing - complete - MVC + background services.
2. Flow 2 RAG backend - complete.
3. Flow 2 MVC Chat/history/citations/evaluation - complete.
4. Flow 3 Report & Statistics - complete - Razor Pages behind `IReportQueryService`.
5. Provider-neutral AI runtime - complete.

## Provider-neutral boundary

Core provider contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

Infrastructure selects Ollama, Gemini, OpenAI, or OpenRouter implementations. Chat and embedding providers may be configured independently. OpenRouter may perform ordered chat-model fallback inside Infrastructure when OpenRouter chat is explicitly selected.

Do not:

- add provider-specific DTOs to Application;
- expose API keys through Application contracts;
- branch on provider names inside workflow services/models;
- implement provider/model routing in Application;
- assume equal embedding dimensions mean compatible vector spaces.

Changing embedding provider/model/dimension requires a complete corpus re-index. PR #37 only makes different-dimension transition periods safe at the retrieval layer; it does not make different embedding models interchangeable.

## Subject boundary

Persisted subject context includes:

```text
Chapter.SubjectId
Document.SubjectId
ChatSession.SubjectId
```

Do not add a product contract that silently drops subject context or intentionally falls back to global-corpus retrieval.

## Flow 1 boundary

```text
subject-aware MVC action
 -> persist Document/Chapter
 -> IDocumentIndexingQueue
 -> IDocumentIndexingService
 -> ITextEmbeddingService
```

Application contracts remain unaware of the selected concrete embedding provider.

## Flow 2 boundary

```text
MVC Chat/Evaluation
 -> IRagQueryService / IEvaluationService
 -> provider-neutral embedding + chat contracts
 -> subject-scoped persistence/retrieval
```

`RagAnswer` and `RagCitation` are presentation-safe result models. The MVC layer must not need pgvector/provider payload types.

The browser transport used by Chat is SSE, but SSE implementation details belong in Presentation (`ChatController`/View JavaScript), not Application contracts.

## Flow 3 boundary after PR #40

Flow 3 now has an explicit Application query contract:

```text
IReportQueryService
  -> Task<SubjectReportSnapshot?> GetSubjectReportAsync(...)
```

`SubjectReportSnapshot` and its report read models are presentation-safe. EF Core query implementation belongs in Infrastructure (`ReportQueryService`).

Do not move `ApplicationDbContext`, `DbSet`, EF expressions, or Npgsql types into the Razor Page or Application model contract.

## Shared contracts/models

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `IEvaluationService`
- `IReportQueryService`
- `RagAnswer` / `RagCitation`
- `SubjectReportSnapshot` and report read models

Prefer additive, purpose-specific contracts. Keep infrastructure payloads under Infrastructure.

## Dependency rules

- Application abstractions do not depend on MVC, Razor `PageModel`, `HttpContext`, provider-specific SDK/DTOs, EF Core query types, Npgsql, CSS, or JavaScript.
- Infrastructure implements provider adapters, pgvector retrieval, reporting queries and persistence details.
- Flow 1 controllers do not parse/chunk/embed/call providers.
- Flow 2 controllers call application services rather than providers/pgvector directly.
- Flow 3 PageModels call `IReportQueryService` rather than `ApplicationDbContext` directly.

## Documentation identity rule

Project documentation uses Member numbers only. Contribution credit is separate from ownership; use `docs/member-contributions.md`.
