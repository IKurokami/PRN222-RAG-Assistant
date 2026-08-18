# Member 4 - RAG Backend Handoff

> Updated after PR #30 merged on 2026-08-18.

## Status

**Backend baseline complete / merged.**

Member 4 owns the Flow 2 RAG backend. The remaining product-level MVC Chat/history/citation/evaluation work belongs to Member 5.

## What is merged

### Subject-scoped RAG pipeline

The backend now provides:

```text
question
  -> ITextEmbeddingService
  -> subject-scoped pgvector retrieval
  -> GroundedPromptBuilder
  -> IChatCompletionService
  -> citation marker parsing
  -> user/assistant message persistence
  -> MessageCitation persistence
```

Important behavior:

- chat session ownership is validated against the authenticated user;
- `ChatSession.SubjectId` is part of the persisted model;
- a caller-provided subject that conflicts with the session subject is rejected;
- the subject-aware session creation path selects/binds a subject before normal product use;
- retrieval can filter indexed documents by `Document.SubjectId`;
- conversation history is loaded before the current question is persisted, preventing the current question from appearing twice in the prompt;
- only citation markers actually referenced by the model are persisted/rendered;
- no-evidence responses return without fake citations;
- provider calls remain behind `ITextEmbeddingService` and `IChatCompletionService`.

## Main implementation areas

| Area | Purpose |
|---|---|
| `Application/Abstractions/IRagQueryService.cs` | Application-facing RAG/session contract |
| `Infrastructure/Rag/RagQueryService.cs` | Query, history, retrieval, completion and persistence orchestration |
| `Infrastructure/Rag/PgVectorDocumentChunkRetriever.cs` | pgvector similarity retrieval with subject filter support |
| `Infrastructure/Rag/GroundedPromptBuilder.cs` | Grounded system/user prompt composition |
| `Infrastructure/Rag/RagOptions.cs` | Retrieval/chat tuning |
| `Infrastructure/Rag/InternalTypes.cs` | Retriever/history internal models |
| `Infrastructure/Rag/Exceptions/*` | RAG/session exceptions |
| `Infrastructure/ServiceCollectionExtensions.cs` | RAG registration and fail-fast options validation |
| `Pages/RagDemo/*` | Internal authenticated development/demo surface; not the final product MVC Flow 2 UI |

## Configuration

RAG tuning is bound from the `Rag:` configuration section and validated at startup.

Key retrieval settings include:

- `TopK > 0`;
- `MinimumSimilarityScore` between 0 and 1;
- non-negative history count;
- positive citation excerpt length.

Member 4 does not select concrete AI providers. Provider wiring remains Member 1 infrastructure.

## Tests added/hardened

The merged backend test suite now covers real `RagQueryService` behavior, including:

- empty-question validation;
- session ownership validation;
- user/assistant message persistence;
- no-evidence behavior;
- citation marker parsing and persistence;
- current-question exclusion from loaded history;
- session subject passed to retrieval via `Mock.Verify`;
- conflicting subject rejection;
- failure paths where embedding/chat services throw without leaving conversation messages persisted;
- subject-aware session creation/reuse.

## Issue #27 contribution in PR #30

PR #30 also contains Member 4's merged contribution to the document ingestion/chunking remediation:

- deterministic bounded overlap;
- Unicode normalization and safer grapheme boundaries;
- configurable `ChunkingOptions` with startup validation;
- improved PDF multi-column reading order;
- PDF parser/chunker regression tests;
- DOCX blank-paragraph/page-number correction;
- additional DOCX/PPTX parser improvements and integration coverage.

This work is contribution credit for Member 4 even though Member 3 remains the maintenance owner for document indexing/ingestion.

## Follow-up technical debt

PDF is currently the primary real-world ingestion format being exercised most heavily.

Deferred follow-up items:

- deeper DOCX fixtures for complex list/table/layout cases;
- deeper PPTX fixtures for grouped shapes, tables and parent-group transform handling;
- further complex PDF table/side-note/rotated-text hardening.

These are follow-up improvements, not blockers for the merged PR #30 milestone.

## Handoff to Member 5

Member 5 should build the final MVC product experience on `IRagQueryService` rather than calling pgvector or provider APIs directly.

Member 5 owns:

- MVC Chat/session/history/citation controllers/views;
- subject-aware conversation navigation;
- user-facing citation presentation;
- evaluation tooling.

The internal `Pages/RagDemo` surface is only a development/demo aid and should not be treated as the final Flow 2 presentation architecture.

## Contribution accounting

Canonical contribution credit is tracked in `docs/member-contributions.md`.

Project documentation uses Member numbers only and must not add GitHub usernames.
