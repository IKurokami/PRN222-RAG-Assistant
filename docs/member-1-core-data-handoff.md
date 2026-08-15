# Member 1 handoff - Core/Data/RBAC/Multi-subject/AI Providers/Documentation

> Updated for the AI-provider backup task based on `master` after PR #20.

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
- **AI provider selection/configuration**;
- **Ollama/Gemini/OpenAI adapters behind provider-neutral interfaces**;
- **API-key/env wiring and startup validation**;
- **embedding dimension/vector-space/re-index coordination**;
- all repository documentation.

## AI provider assignment

This task is explicitly assigned to **Member 1**.

Supported runtime choices:

```text
Ollama -> local/default
Gemini -> online Free Tier backup
OpenAI -> online paid optional
```

Selected models as of 2026-08-15:

```text
Ollama: qwen3:4b + qwen3-embedding:0.6b
Gemini: gemini-3.6-flash + gemini-embedding-2
OpenAI: gpt-5.6-luna + text-embedding-3-small
```

Gemini is the project's recommended **free online** path. OpenAI is not documented as free.

Provider-specific payloads remain in Infrastructure; Application keeps `ITextEmbeddingService` and `IChatCompletionService` provider-neutral.

## Secrets/configuration

API keys are supplied only through environment/configuration:

```text
GEMINI_API_KEY
OPENAI_API_KEY
```

No real key belongs in tracked files. The selected provider validates its own required settings at startup.

## Embedding invariant

Default:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

If embedding provider/model/dimensions change, Member 1 coordinates a complete corpus re-index before retrieval. Same-sized vectors from different models must not be treated as compatible.

## Existing multi-subject baseline

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

Admin manages users/roles and all Subjects. Subject Leaders manage only assigned Subjects.

Assignments use Identity claims:

```text
prn222:managed-subject -> Subject Guid
```

## Cross-workflow boundary

Flow 1 request behavior remains Member 2-owned. Indexing/chunking/worker behavior remains Member 3-owned. Member 1's provider work supplies the concrete `ITextEmbeddingService` used by that indexing pipeline.

Future Flow 2 RAG behavior remains Member 4-owned. Member 4 consumes `ITextEmbeddingService` and `IChatCompletionService`; Member 1 owns provider plumbing, not RAG retrieval/grounding semantics.

## Schema impact

The provider-backup task adds no EF model change and requires no migration.

A future Flow 2 subject ownership change may still require a migration; Member 1 coordinates it separately.

## Documentation responsibility

Member 1 exclusively edits README, AGENTS files, and `docs/*`.
