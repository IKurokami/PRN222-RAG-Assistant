# Team workflow and ownership

> Provider-backup coordination baseline: work based on `master` after merged PR #20. Member 1 is the sole documentation owner.

## Current milestone

- Member 1 Core/Data/Identity/RBAC: complete.
- Member 1 multi-subject management/subject scoping: complete.
- **Member 1 AI provider foundation: implemented in this PR.**
- Member 2 Flow 1 request/business behavior: complete.
- Member 3 Flow 1 indexing: complete / merged through PR #9.
- Member 2 Flow 3 reporting behavior: complete / merged through PR #12.
- Member 3 cross-app UI/UX redesign: complete / merged through PR #19.
- Member 4 Flow 2 backend: pending.
- Member 5 Flow 2 MVC/history/citations/evaluation: pending.

## Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Owns:

- domain/data/security baseline;
- shared contracts and EF migration coordination;
- Identity configuration/seeding and RBAC rules;
- Admin/SubjectLeader/Student roles/policies;
- Subject management/assignment and `ISubjectAccessService`;
- cross-workflow subject-context integration;
- **AI provider selection/configuration**;
- **Gemini/OpenAI/Ollama concrete AI adapters behind Application interfaces**;
- **online API-key/env wiring**;
- **embedding dimension/vector-space/re-index compatibility rule**;
- provider regression tests;
- all README/AGENTS/docs edits.

The provider task is cross-cutting infrastructure. It does not move parsing/chunking/indexing workflow ownership from Member 3 or RAG business behavior from Member 4.

## Member 2 - Flow 1 request behavior + Flow 3 reporting

Member 2 retains established Chapter/Document request semantics and read-only reporting behavior.

Controllers/Pages must not call a concrete AI provider.

## Member 3 - indexing + UI/UX redesign

### Indexing - complete

Member 3 owns parsers, chunker, indexing service/worker, state transitions, chunk replacement, and startup recovery.

The pipeline consumes `ITextEmbeddingService`; Member 1 now owns which concrete provider implements that interface at startup.

No provider-specific worker is created.

### Cross-app UI/UX redesign - complete

Member 3 retains the PR #19 design system/presentation ownership. Provider-backup changes may update factual copy that previously said AI was always local, but this does not transfer visual ownership.

## Member 4 - Flow 2 backend

Pending responsibilities:

- subject-scoped RAG query design;
- question embeddings through `ITextEmbeddingService`;
- pgvector retrieval over only the selected subject;
- grounding/no-evidence behavior;
- completion through `IChatCompletionService`;
- session ownership validation;
- messages/citations persistence.

Do not call Ollama, Gemini, or OpenAI directly.

## Member 5 - Flow 2 MVC/evaluation

Owns Chat MVC actions/views, subject-aware session navigation/history/citations, and evaluation tooling.

Do not call provider APIs in controllers/views.

## Provider integration map

```text
RAG_PROVIDER
   |
   +--> Ollama [local/default, $0 provider fee]
   +--> Gemini [online Free Tier backup]
   \--> OpenAI [online paid optional]
             |
             +--> ITextEmbeddingService
             \--> IChatCompletionService
                        |
              +---------+---------+
              |                   |
         Flow 1 indexing     future Flow 2 RAG
         [Member 3]          [Member 4]
```

## Provider change procedure

If only the chat model changes, no existing document vectors need re-indexing.

If embedding provider/model/dimension changes:

1. Member 1 records the configuration change;
2. existing stored embeddings are treated as stale;
3. re-index the entire searchable corpus;
4. do not mix old/new vectors during retrieval;
5. validate indexing/retrieval before considering the provider switch complete.

## Secrets

Real API keys live only in local/deployment secret environments. `.env.example` contains blank placeholders.

Members must never put API keys into PR descriptions, screenshots, test fixtures using real credentials, browser JavaScript, tracked appsettings, logs, or docs.

## Documentation workflow

Members 2-5 report code/status/doc impacts to Member 1. Member 1 reconciles docs with actual code after integration.
