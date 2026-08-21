# Member 4 - RAG Backend Handoff

> Updated on 2026-08-21 after the Chat Razor Pages migration in PR #42/#43 and the accepted Razor Pages-only presentation target.

## Status

The RAG backend baseline is complete and integrated with the product Chat.

Member 4 remains the maintenance owner for core Flow 2 RAG behavior. Historical product UI/evaluation contribution remains credited separately in `member-contributions.md`.

## Current RAG pipeline

```text
subject-aware ChatSession
 -> IRagQueryService
 -> ITextEmbeddingService
 -> PgVectorDocumentChunkRetriever
 -> GroundedPromptBuilder
 -> IChatCompletionService
 -> citation marker parsing
 -> ChatMessage + MessageCitation persistence
 -> Razor Page Chat
```

## Core backend invariants

- session ownership is validated against the authenticated user;
- `ChatSession.SubjectId` carries product subject context;
- conflicting caller/session subject IDs are rejected;
- retrieval filters indexed Documents by SubjectId;
- only citation markers actually referenced by the answer are persisted/rendered;
- no-evidence behavior does not invent citations;
- workflow code remains provider-neutral;
- conversation history is loaded without duplicating the current question.

## Presentation integration

Chat is already under `Pages/Chat` after PR #42. PR #43 moved page/session data operations behind `IChatPageService` so the PageModel no longer needs direct `ApplicationDbContext` access for those responsibilities.

Evaluation's target presentation is Razor Pages under `Pages/Evaluation` while preserving `IEvaluationService` behavior.

The repository target is no MVC product presentation after the remaining migration is complete.

## Chat transport

Chat uses **Server-Sent Events (SSE)** for progress/result rendering.

This transport remains intentionally separate from the new Document Management SignalR requirement:

```text
Chat      -> Razor Pages + SSE
Documents -> Razor Pages + SignalR notifications
```

Do not move Chat to SignalR as part of the document realtime migration.

## Retrieval after PR #37

`PgVectorDocumentChunkRetriever` constrains candidates by both:

```text
Document.SubjectId
vector_dims(DocumentChunk.Embedding) == questionEmbedding.Length
```

The dimension filter prevents cosine-distance failures during a dimension-changing corpus re-index. Complete re-index is still required when embedding provider/model/dimension changes.

## Provider boundary

Member 4 backend code continues to consume:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete provider routing/configuration remains an Infrastructure responsibility.

## Remaining backend debt

Current follow-up items are refinements rather than missing baseline behavior:

- additional retrieval/grounding evaluation against real course documents;
- optional provider-native token streaming only if the shared provider contract is intentionally extended;
- preserve the `IChatPageService`/application boundary as Chat evolves;
- ingestion quality work under indexing ownership.

## Presentation migration rule

The follow-up Razor Pages migration for Evaluation/admin/Flow 1 must not alter RAG subject/session/provider invariants. Document SignalR is unrelated to RAG transport and must not become a global event channel for Chat data.

See `razor-pages-signalr-architecture.md` for the canonical presentation target.
