# Member 1 handoff - Core/Data/RBAC/Multi-subject/AI Providers/Documentation

> Synchronized after PR #30 merged on 2026-08-18.

## Ownership

Member 1 owns:

- Domain/Data/Security baseline;
- Identity roles and policies;
- shared Application contracts and schema/migration coordination;
- Admin user/role behavior;
- Subject catalogue + Admin Subject behavior;
- Subject Leader assignment;
- subject-specific authorization service;
- cross-workflow subject-context integration;
- role/subject regression tests;
- AI provider selection/configuration;
- Ollama/Gemini/OpenAI/OpenRouter adapters behind provider-neutral interfaces;
- OpenRouter free chat-model routing/fallback;
- API-key/env wiring and startup validation;
- embedding dimension/vector-space/re-index coordination;
- repository-wide documentation synchronization.

## Actual merged contribution credit

Member 1 also delivered merged implementation outside nominal ownership:

- original end-to-end indexing pipeline in PR #9;
- document chunk preview, semantic chunking and PDF extraction improvements in PR #23;
- repeated cross-workflow integration/review/doc synchronization around Flow 1/2/3.

These contributions are credited to Member 1 even though Member 3 retains ongoing indexing maintenance ownership.

Canonical ledger: `docs/member-contributions.md`.

## AI provider infrastructure

Supported runtime choices:

```text
Ollama
Gemini
OpenRouter
OpenAI
```

Application/workflow code remains provider-neutral through:

```text
ITextEmbeddingService
IChatCompletionService
```

Backward-compatible selection:

```text
RAG_PROVIDER
```

Optional overrides:

```text
RAG_CHAT_PROVIDER
RAG_EMBEDDING_PROVIDER
```

No real API key belongs in tracked files.

## Embedding invariant

Embedding uses one semantic vector space per searchable corpus.

If embedding provider/model/dimension changes, Member 1 coordinates a complete corpus re-index before retrieval. Same-sized vectors from different models are not interchangeable.

Chat provider/model changes alone do not require re-indexing.

## Multi-subject baseline

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers
ManageSubjects
ManageDocuments
```

Admin manages users/roles and all Subjects. Subject Leaders manage assigned Subjects only.

Assignments use Identity claims:

```text
prn222:managed-subject -> Subject Guid
```

`ChatSession.SubjectId` is now persisted after PR #30. Member 1 remains migration/schema coordinator for future cross-workflow model changes.

## Cross-workflow boundary

- Member 2 owns Flow 1 request/business behavior and Flow 3 reporting behavior.
- Member 3 owns ongoing indexing/ingestion maintenance and the UI/UX baseline.
- Member 4 now owns a merged Flow 2 RAG backend baseline.
- Member 5 owns the remaining Flow 2 MVC/history/citation/evaluation product layer.

Member 1 owns provider plumbing and schema/contract coordination, not Member 4's retrieval/grounding semantics.

## Documentation responsibility

Member 1 exclusively edits README, AGENTS files and `docs/*` after reconciling merged changes from the whole team.

Project documentation uses Member numbers only and must not add GitHub usernames.
