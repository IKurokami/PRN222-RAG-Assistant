# Application-layer instructions

> Updated on 2026-08-21 for the PR #46/issue #47 management realtime implementation branch. PR #46 is not merged; this guidance distinguishes branch implementation state from merged `master`.

This subtree contains provider-neutral, presentation-safe cross-workflow contracts/models. Keep it independent from Razor runtime types, SignalR hub types, provider-specific payloads, and PostgreSQL implementation details.

## Target workflow presentation

1. Flow 1 - Document/Chapter Management & Indexing - Razor Pages + background services + management realtime notifications.
2. Flow 2 RAG backend - complete and provider-neutral.
3. Flow 2 Chat/history/citations - Razor Pages + SSE.
4. Flow 2 Evaluation - target Razor Pages.
5. Flow 3 Report & Statistics - Razor Pages behind `IReportQueryService`.
6. Admin/Subject catalogue - target Razor Pages.

The remaining runtime MVC surfaces are implementation debt on merged `master`; PR #46 retains the PageModel/DbContext cleanup on its unmerged branch. Do not add new Application contracts that assume MVC controllers/views.

## Provider-neutral boundary

Core provider contracts:

```text
ITextEmbeddingService
IChatCompletionService
```

Infrastructure selects Ollama, Gemini, OpenAI, or OpenRouter implementations. Chat and embedding providers may be configured independently.

Do not:

- add provider-specific DTOs to Application;
- expose API keys through Application contracts;
- branch on provider names inside workflow services/models;
- implement provider/model routing in Application;
- assume equal embedding dimensions mean compatible vector spaces.

Changing embedding provider/model/dimension requires a complete corpus re-index.

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
subject-aware Razor Page handler
 -> application-facing Document/Chapter behavior boundary
 -> persist requested change
 -> IDocumentIndexingQueue when required
 -> IManagementRealtimeNotifier after the commit succeeds
```

Indexing remains:

```text
IDocumentIndexingQueue
 -> IDocumentIndexingService
 -> ITextEmbeddingService
```

## Management realtime contract

Application owns the provider-neutral notifier boundary:

```csharp
public interface IManagementRealtimeNotifier
{
    Task PublishAsync(
        ManagementRealtimeEvent notification,
        CancellationToken cancellationToken = default);
}

public record ManagementRealtimeEvent(
    ManagementResource Resource,
    ManagementChange Change,
    Guid EntityId,
    Guid? SubjectId = null,
    string? Status = null);
```

`ManagementResource` values are `Document`, `Chapter`, `Subject`, `SubjectLeaderAssignments`, and `User`. `ManagementChange` values are `Created`, `Updated`, `Deleted`, `IndexStatusChanged`, `AssignmentsChanged`, and `RoleChanged`.

Publish only after the requested write is durably committed. The SignalR implementation belongs outside Application and maps this contract to the authorized `ManagementHub`; it must not expose a write operation.

Application code must not depend on SignalR `Hub`, `IHubContext`, JavaScript client types, or PageModel.

## Flow 2 boundary

```text
Chat Razor Page
 -> IChatPageService / IRagQueryService
 -> provider-neutral embedding + chat contracts
 -> subject-scoped persistence/retrieval

Evaluation Razor Page target
 -> IEvaluationService
```

`RagAnswer` and `RagCitation` are presentation-safe result models.

Chat transport is SSE, but SSE response details stay in Presentation rather than Application contracts.

## Flow 3 boundary

```text
IReportQueryService
  -> Task<SubjectReportSnapshot?> GetSubjectReportAsync(...)
```

`SubjectReportSnapshot` and report read models are presentation-safe. EF Core query implementation belongs in Infrastructure.

## Shared contracts/models

- `IDocumentIndexingQueue`
- `IManagementRealtimeNotifier`
- `ManagementRealtimeEvent` and its management resource/change values
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `IChatPageService`
- `IEvaluationService`
- `IReportQueryService`
- `RagAnswer` / `RagCitation`
- `SubjectReportSnapshot` and report read models

Prefer additive, purpose-specific contracts. Keep infrastructure payloads under Infrastructure.

## Dependency rules

- Application abstractions do not depend on MVC, Razor `PageModel`, `HttpContext`, SignalR hub types, provider-specific SDK/DTOs, EF Core query types, Npgsql, CSS, or JavaScript.
- Infrastructure implements provider adapters, pgvector retrieval, reporting queries and persistence details.
- PageModels call application-facing services instead of provider/pgvector implementations directly.
- SignalR fan-out does not become a business-write path.

## Documentation identity rule

Project documentation uses Member numbers only. Contribution credit is separate from ownership; use `docs/member-contributions.md`.
