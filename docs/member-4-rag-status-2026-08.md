# Member 4 - RAG Backend Status (August 2026)

> Refreshed on 2026-08-21 after PR #42/#43 and the accepted Razor Pages-only presentation target.

## Current status

Flow 2 RAG backend is complete and the product Chat now uses Razor Pages.

```text
Razor Page Chat
 -> IChatPageService for page/session data
 -> IRagQueryService
 -> ITextEmbeddingService
 -> subject-scoped + dimension-compatible pgvector retrieval
 -> grounded prompt/history
 -> IChatCompletionService
 -> citation parsing/persistence
 -> SSE presentation events
```

## Merged milestones

### PR #30

Established the subject-aware RAG backend baseline, persistence, retrieval, grounding and tests.

### PR #34/#35

Integrated the original product Chat/Evaluation experience, strengthened grounding/follow-up behavior and added the SSE progress/typewriter UX.

### PR #37

Made retrieval safe during a dimension-changing embedding re-index by filtering stored vectors with `vector_dims(...)` before cosine distance.

### PR #42

Migrated Chat HTTP presentation to Razor Pages while preserving `/Chat` behavior and SSE.

### PR #43

Removed direct Chat PageModel DbContext usage for page/session data by introducing `IChatPageService`.

## Subject isolation

Normal product sessions are subject-aware. Retrieval filters indexed Documents by the validated subject. The product must not intentionally create a global-corpus path.

## Chat transport

The browser transport remains **Server-Sent Events (SSE)** from the Chat Razor Page streaming handler.

Document Management's planned SignalR channel is separate and must not replace Chat SSE.

## Provider neutrality

Core backend remains provider-neutral through:

```text
ITextEmbeddingService
IChatCompletionService
```

Current Render uses Gemini for Chat and OpenRouter for embeddings, but deployment choice must not leak into RAG workflow semantics.

## Presentation target debt

Chat is already aligned with the Razor Pages target. Evaluation and remaining legacy product/admin surfaces still need a follow-up code migration before the repository can remove MVC presentation/routing completely.

See `razor-pages-signalr-architecture.md`, `member-4-rag-backend-handoff.md`, and `project-status.md`.
