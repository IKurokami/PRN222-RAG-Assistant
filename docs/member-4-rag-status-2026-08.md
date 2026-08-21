# Member 4 - RAG Backend Status (August 2026)

> Refreshed on 2026-08-21 after PR #40. This replaces the old pre-merge branch/design snapshot that referenced `Member4/Flow-2-backend` and obsolete `RagDemo` integration.

## Current status

Flow 2 RAG backend is complete and integrated into the product MVC Chat/Evaluation experience.

```text
MVC Chat
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

Established the backend baseline:

- subject-aware `ChatSession.SubjectId`;
- session ownership/subject validation;
- pgvector retrieval;
- grounded prompt and no-evidence path;
- message/citation persistence;
- citation-marker parsing;
- backend tests;
- issue #27 chunk/parser remediation.

### PR #34

Integrated the backend into the product MVC Chat/session/history/citation UI and Evaluation workflow.

### PR #35

Strengthened the integrated Chat/RAG experience:

- stricter grounding;
- contextual follow-up query fallback;
- inline citation behavior;
- longer citation excerpts;
- SSE progress/typewriter UI;
- removal of obsolete `Pages/RagDemo`.

### PR #37

Made retrieval safe during a dimension-changing embedding re-index by filtering stored vectors with `vector_dims(...)` before cosine distance.

## Current integration contract

Presentation uses `IRagQueryService` and presentation-safe `RagAnswer`/`RagCitation` models. Provider-specific APIs and pgvector query details stay behind Infrastructure boundaries.

## Subject isolation

Normal product sessions are subject-aware. Retrieval filters indexed Documents by the validated subject. The product should not intentionally create a global-corpus path.

The MVC Chat controller may still surface legacy null-subject sessions for compatibility, but new product sessions are created with a concrete `SubjectId` and the RAG service validates subject consistency.

## Chat transport

The current browser transport is **Server-Sent Events (SSE)** from `POST /Chat/AskStream`.

It is not SignalR. Current SSE is application-level progress/typewriter output after/around the existing RAG call, not a claim of provider-native token streaming.

## Provider neutrality

Core backend remains provider-neutral through:

```text
ITextEmbeddingService
IChatCompletionService
```

Current Render happens to use Gemini for Chat and OpenRouter for embeddings, but that deployment choice must not leak into RAG workflow semantics.

## Current known debt

- evaluate retrieval/grounding quality against larger real-world course corpora;
- preserve strict subject/citation regression coverage as Chat evolves;
- optionally design provider-native streaming only if the shared provider contract is intentionally expanded;
- continue ingestion quality work under indexing maintenance.

For detailed architecture, see `member-4-rag-backend-handoff.md`, `infrastructure.md`, and `project-status.md`.
