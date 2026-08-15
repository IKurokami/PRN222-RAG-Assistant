# Project status

> Provider-routing/fallback update on 2026-08-15. Member 1 owns synchronization of this file.

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
| Ollama local adapter | Complete | Member 1 provider foundation around Member 3 indexing |
| Gemini direct online Free Tier adapter | Complete | **Member 1** |
| Optional OpenAI paid adapter | Complete | **Member 1** |
| OpenRouter adapter | Implemented in current provider-routing PR | **Member 1** |
| Independent chat/embedding provider selection | Implemented in current provider-routing PR | **Member 1** |
| OpenRouter ordered free chat fallback | Implemented in current provider-routing PR | **Member 1** |
| Embedding dimension/re-index invariant | Implemented/documented; no embedding-model rotation | **Member 1** |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 RBAC rules |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | Member 3 |
| Documentation synchronization | Updated for provider routing/fallback | Member 1 |

## Online-free decision

The direct online Free Tier path remains Google Gemini:

```text
Chat:      gemini-3.6-flash
Embedding: gemini-embedding-2
```

OpenRouter is added to improve free-chat resilience:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

OpenRouter uses its model fallback mechanism to move to the next configured chat model on errors such as rate limits or downtime. Free availability/rate limits are external constraints and can change.

Recommended development/demo hybrid:

```text
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=Gemini
```

This lets chat fall back without changing the corpus embedding vector space.

OpenAI remains only an optional paid provider:

```text
Chat:      gpt-5.6-luna
Embedding: text-embedding-3-small
```

Do not describe OpenAI as the project's free backup.

## Provider selection

```text
RAG_PROVIDER=Ollama        # legacy/default for both contracts
RAG_CHAT_PROVIDER=         # optional override
RAG_EMBEDDING_PROVIDER=    # optional override
```

All three settings accept `Ollama`, `Gemini`, `OpenAI`, or `OpenRouter`; blank overrides inherit `RAG_PROVIDER`.

There is no hidden cross-provider cloud failover. OpenRouter internal fallback happens only when OpenRouter is explicitly selected.

## Embedding compatibility

Default dimension:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

If embedding provider/model/dimension changes, re-index all documents before retrieval. Same vector length is not enough for cross-model compatibility. Do not rotate embedding models.

Changing only chat provider/model/fallback order does not require re-indexing.

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

Flow 1 request behavior remains unchanged. Indexing resolves its embedding implementation through `ITextEmbeddingService` from the selected embedding provider.

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

The major unfinished product workflow remains **Flow 2**. Provider infrastructure now supports stable local/direct-cloud operation plus explicit OpenRouter free-chat fallback without forcing Member 4 to hard-code provider behavior.

## Documentation ownership

Member 1 exclusively edits README, AGENTS files, and `docs/*`.
