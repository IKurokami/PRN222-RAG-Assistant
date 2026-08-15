# Project status

> AI-provider backup update based on `master` after merged PR #20. Member 1 owns synchronization of this file.

## Workflows

| Workflow | Presentation | Status | Owner |
|---|---|---|---|
| Flow 1 - Document Management & Indexing | MVC | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC/provider integration |
| Flow 2 - RAG Q&A + Conversation Management | MVC | Pending | Member 4 backend + Member 5 UI/evaluation |
| Flow 3 - Report & Statistics | Razor Pages | Complete | Member 2 behavior; Member 1 subject/RBAC integration |

Conversation History is part of Flow 2.

## Platform/RBAC/provider state

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity | Complete | Member 1 |
| Admin/SubjectLeader/Student roles | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Subject catalogue/Admin Subject management | Complete / merged | Member 1 |
| Subject Leader assignment | Complete / merged | Member 1 |
| Subject-specific authorization service | Complete / merged | Member 1 |
| AI provider-neutral interfaces | Existing baseline | Member 1 contracts |
| Ollama local adapter | Complete; embedding existing + chat adapter added | Member 1 provider foundation around Member 3 indexing |
| Gemini online Free Tier adapter | Implemented in provider-backup PR | **Member 1** |
| Optional OpenAI paid adapter | Implemented in provider-backup PR | **Member 1** |
| Provider selection/env/API-key validation | Implemented in provider-backup PR | **Member 1** |
| Embedding dimension/re-index invariant | Implemented/documented | **Member 1** |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 RBAC rules |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | Member 3 |
| Documentation synchronization | Updated for provider backup | Member 1 |

## Online-free decision

The main online backup for development/demo is **Google Gemini Developer API Free Tier**:

```text
Chat:      gemini-3.6-flash
Embedding: gemini-embedding-2
```

As of 2026-08-15, Google's official pricing lists Standard Free Tier pricing as free of charge for input/output on `gemini-3.6-flash` and free of charge for Gemini Embedding 2 inputs. Free Tier remains rate-limited and has different data-use terms from paid tier.

OpenAI is retained only as an optional paid provider:

```text
Chat:      gpt-5.6-luna
Embedding: text-embedding-3-small
```

Do not describe OpenAI as the project's free backup.

## Provider selection

```text
RAG_PROVIDER=Ollama   # local/default
RAG_PROVIDER=Gemini   # online Free Tier backup
RAG_PROVIDER=OpenAI   # optional paid API
```

No automatic failover occurs.

## Embedding compatibility

Default dimension:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

If provider/model/dimension changes, re-index all documents before retrieval. Same vector length is not enough for cross-model compatibility.

## Multi-subject state

PRN222 remains seeded but is not the application-wide hard-coded scope.

```text
Subjects
  +--> Chapters (SubjectId)
  +--> Documents (SubjectId)
  +--> Subject Leader assignments (Identity claims)
  \--> future ChatSessions/RAG subject boundary [Flow 2 pending]
```

## Authorization state

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Subject-specific actions additionally use `ISubjectAccessService`.

## Flow 1 state

Flow 1 request behavior remains unchanged. Indexing now resolves its embedding implementation through `ITextEmbeddingService` from the selected provider.

Provider switching does not require different workers. A document still queues only `Document.Id`.

## Flow 3 state

Flow 3 remains provider-independent/read-only. It must not call AI providers.

Chat metrics remain global because Flow 2 is pending and current `ChatSession` has no SubjectId.

## Flow 2 remaining requirement

Member 4/5 must not implement global-corpus chat or concrete-provider coupling.

Required direction:

```text
selected subject
 -> ITextEmbeddingService
 -> same-subject pgvector retrieval
 -> IChatCompletionService
 -> same-subject citations/history
```

Any real EF model change remains coordinated by Member 1.

## Next project priority

The major unfinished product workflow remains **Flow 2**. Provider infrastructure is prepared so Member 4 can focus on subject-scoped RAG behavior instead of hard-coding Ollama/online APIs.

## Documentation ownership

Member 1 exclusively edits README, AGENTS files, and `docs/*`.
