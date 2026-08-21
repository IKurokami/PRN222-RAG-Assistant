# Member 4 - RAG Backend Handoff

> Synchronized with the integrated Flow 2 product after PR #35/#37 on 2026-08-21.

## Status

**RAG backend baseline complete and integrated with the MVC product UI.**

Member 4 remains the maintenance owner for core Flow 2 RAG behavior. Member 5's MVC Chat/history/citations/evaluation product layer is also complete after PR #34/#35.

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
 -> MVC Chat
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

## PR #35 integrated grounding changes

The merged Flow 2 product integration strengthened backend behavior as well as presentation:

- stricter grounded prompting/anti-hallucination instructions;
- inline citation expectations near supported facts;
- longer citation excerpts for the reader experience;
- contextual query fallback for short follow-up questions when standalone retrieval finds no useful evidence.

These changes were delivered as part of Member 5's Flow 2 product integration; Member 4 remains the backend maintenance owner going forward.

## Retrieval after PR #37

`PgVectorDocumentChunkRetriever` now constrains candidates by both:

```text
Document.SubjectId
vector_dims(DocumentChunk.Embedding) == questionEmbedding.Length
```

The dimension filter prevents cosine-distance failures during a dimension-changing corpus re-index. Old-dimension rows remain temporarily excluded until re-indexed.

Complete re-index is still required when embedding provider/model/dimension changes. Same-dimension embeddings from different models are not semantically interchangeable.

## Product integration

The final product presentation is:

```text
Controllers/ChatController.cs
Views/Chat/
Controllers/EvaluationController.cs
Views/Evaluation/
```

The obsolete `Pages/RagDemo` surface was removed in PR #35.

### Chat transport

`ChatController.AskStream` returns `text/event-stream` and emits application-level events consumed by `fetch`/ReadableStream in the Chat view.

This is SSE, not SignalR. The controller currently awaits the RAG service result and then emits word-level `delta` events for the typewriter effect; this should not be described as provider-native streaming.

## Evaluation integration

`IEvaluationService` and `EvaluationController` support single-question and full 50-question evaluation. Evaluation resolves a matching active subject based on the dataset subject code.

## Provider boundary

Member 4 backend code must continue to consume:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete provider routing/configuration remains cross-cutting Infrastructure responsibility.

## Issue #27 contribution

PR #30 also contains Member 4's contribution to document chunking/parser remediation:

- deterministic bounded overlap;
- Unicode/grapheme safety;
- configurable chunking;
- improved PDF multi-column order;
- DOCX page-number correction;
- added parser/integration coverage.

Member 3 remains indexing/ingestion maintenance owner.

## Remaining backend debt

Current follow-up items are refinements rather than missing baseline behavior:

- additional retrieval/grounding evaluation against real course documents;
- optional provider-native streaming if the provider abstraction is intentionally extended;
- further refactors to isolate presentation data reads where useful;
- ingestion quality work tracked under indexing ownership.

## Contribution accounting

Use `member-contributions.md` for ownership versus merged contribution credit. Project documentation uses Member numbers only.
