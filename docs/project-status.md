# Project status

> Synchronized on 2026-08-18 after PR #30 merged and issue #27 closed. Member 1 owns synchronization of this file.

## Workflow status

| Workflow | Presentation | Status | Ownership |
|---|---|---|---|
| Flow 1 - Document Management & Indexing | MVC | Complete | Member 2 request behavior + Member 3 indexing maintenance; Member 1 subject/RBAC/provider integration |
| Flow 2 - RAG Q&A + Conversation Management | MVC product UI | Backend complete / product UI pending | Member 4 backend complete; Member 5 MVC/evaluation pending |
| Flow 3 - Report & Statistics | Razor Pages | Complete | Member 2 behavior; Member 1 subject/RBAC integration |

Conversation History is part of Flow 2.

## Current merged milestone

### Member 1

Complete / active ownership:

- Core/Data/Identity/EF architecture;
- Admin/SubjectLeader/Student RBAC;
- Admin user/role management;
- multi-subject management and Subject Leader assignment;
- subject-specific authorization;
- provider-neutral AI runtime and provider configuration;
- Ollama/Gemini/OpenAI/OpenRouter adapters;
- independent chat/embedding provider selection;
- OpenRouter free-chat fallback;
- embedding compatibility/re-index rules;
- repository documentation and cross-member integration.

Member 1 also has merged implementation credit outside nominal ownership, including the initial indexing pipeline in PR #9 and document chunk preview/chunking/PDF work in PR #23. See `docs/member-contributions.md`.

### Member 2

Complete:

- Flow 1 Document/Chapter request/business behavior;
- upload/list/details/edit/delete/re-index behavior;
- Flow 3 Report & Statistics dashboard and tests.

### Member 3

Complete:

- cross-application UI/UX redesign and design system;
- public Student registration presentation/integration;
- ongoing maintenance ownership for document indexing/ingestion.

Contribution accounting does not double-credit PR #9 or PR #30 to Member 3; those merged implementations are recorded under the members who actually delivered them.

### Member 4

Backend baseline complete / merged through PR #30:

- subject-scoped RAG query pipeline;
- pgvector retrieval;
- grounded prompt/no-evidence flow;
- conversation history loading;
- chat message/citation persistence;
- citation-marker parsing;
- session ownership/subject validation;
- RAG configuration validation and backend tests;
- issue #27 chunking/parser remediation merged in PR #30.

### Member 5

Pending:

- final MVC Chat/session/history/citation experience;
- subject-aware product navigation;
- evaluation tooling and final Flow 2 presentation integration.

The internal RAG demo page is not the final Member 5 MVC product flow.

## Issue #27 status

Issue #27 is closed as completed after PR #30 merged.

The merged remediation includes:

- bounded overlap with deterministic forward progress;
- configurable `ChunkingOptions` with startup validation;
- Unicode normalization and safer grapheme boundaries;
- improved PDF two-column reading order and regression coverage;
- DOCX blank paragraphs no longer create fake page numbers;
- additional DOCX/PPTX parser and integration-test improvements.

PDF remains the primary real-world ingestion format currently being exercised most heavily.

## Follow-up ingestion debt

The following items are intentionally deferred and are **not** considered blockers for the completed PR #30 milestone:

- deeper DOCX fixtures for complex lists/tables/layouts;
- deeper PPTX fixtures for grouped shapes, tables and parent-group transforms;
- additional complex PDF table/side-note/rotated-text hardening.

These should be handled in focused follow-up work rather than reopening issue #27 solely for non-PDF coverage.

## Multi-subject / RAG state

`Subject` is a first-class workflow boundary.

```text
Subjects
  +--> Chapters (SubjectId)
  +--> Documents (SubjectId)
  +--> Subject Leader assignments
  \--> ChatSessions (SubjectId)
         \--> subject-scoped retrieval/messages/citations
```

Flow 2 backend now carries subject context. Product callers should create/use sessions through the subject-aware RAG service path and must not intentionally fall back to global-corpus retrieval.

## Authorization state

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Subject-specific actions additionally use `ISubjectAccessService`.

## AI provider state

Provider-neutral contracts remain:

```text
ITextEmbeddingService
IChatCompletionService
```

Supported runtime providers:

```text
Ollama
Gemini
OpenAI
OpenRouter
```

`RAG_PROVIDER` remains the backward-compatible default. `RAG_CHAT_PROVIDER` and `RAG_EMBEDDING_PROVIDER` may override the two purposes independently.

Do not mix embeddings from different models/providers in one searchable corpus. Changing embedding provider/model/dimension requires a complete re-index. Changing only chat provider/model/fallback order does not.

## Flow 3 note after subject-scoped chat

`ChatSession.SubjectId` now exists after PR #30. Existing Flow 3 report queries should be audited when Member 5 completes Flow 2 so chat metrics can be explicitly subject-scoped rather than relying on older global aggregate assumptions.

## Contribution accounting

Canonical contribution credit is maintained in:

- `docs/member-contributions.md`

Repository documentation uses **Member numbers only**. Do not add GitHub usernames to project documentation.

## Next project priority

The largest unfinished product milestone is **Member 5 Flow 2 MVC/evaluation** on top of the now-merged Member 4 backend.

## Documentation ownership

Member 1 exclusively edits README, AGENTS files, and `docs/*` after reconciling merged code from all members.
