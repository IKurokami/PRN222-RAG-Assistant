# Member 1 handoff - Core/Data/RBAC/Multi-subject/AI Providers/Documentation

> Updated for OpenRouter free-model routing/fallback.

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
- **Ollama/Gemini/OpenAI/OpenRouter adapters behind provider-neutral interfaces**;
- **OpenRouter free chat-model routing/fallback**;
- **API-key/env wiring and startup validation**;
- **embedding dimension/vector-space/re-index coordination**;
- all repository documentation.

## AI provider assignment

This task is explicitly assigned to **Member 1**.

Supported runtime choices:

```text
Ollama     -> local/default
Gemini     -> direct online Free Tier path
OpenRouter -> online free-first routed/fallback path
OpenAI     -> online paid optional
```

Selected defaults as of 2026-08-15:

```text
Ollama: qwen3:4b + qwen3-embedding:0.6b
Gemini: gemini-3.6-flash + gemini-embedding-2
OpenRouter chat: google/gemma-4-26b-a4b-it:free -> nvidia/nemotron-3-ultra-550b-a55b:free -> openrouter/free
OpenRouter embedding: nvidia/llama-nemotron-embed-vl-1b-v2:free
OpenAI: gpt-5.6-luna + text-embedding-3-small
```

Gemini remains the stable direct free-online option; OpenRouter adds free-model fallback/rotation for chat. OpenAI is not documented as free.

Provider-specific payloads remain in Infrastructure; Application keeps `ITextEmbeddingService` and `IChatCompletionService` provider-neutral.

## Provider selection

Backward-compatible:

```text
RAG_PROVIDER
```

Optional overrides:

```text
RAG_CHAT_PROVIDER
RAG_EMBEDDING_PROVIDER
```

Blank overrides inherit `RAG_PROVIDER`, so existing deployments do not need to change.

## Secrets/configuration

API keys are supplied only through environment/configuration:

```text
GEMINI_API_KEY
OPENAI_API_KEY
OPENROUTER_API_KEY
```

No real key belongs in tracked files. Only selected providers validate their required settings at startup.

## Chat fallback vs embedding invariant

OpenRouter chat may send an ordered model list and let OpenRouter move to the next model on provider/model failure. Changing chat order/provider does not require corpus re-indexing.

Default embedding dimension:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

Embedding always uses one fixed model. If embedding provider/model/dimensions change, Member 1 coordinates a complete corpus re-index before retrieval. Same-sized vectors from different models must not be treated as compatible.

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

Future Flow 2 RAG behavior remains Member 4-owned. Member 4 consumes `ITextEmbeddingService` and `IChatCompletionService`; Member 1 owns provider plumbing/routing, not RAG retrieval/grounding semantics.

## Schema impact

The provider-routing task adds no EF model change and requires no migration.

A future Flow 2 subject ownership change may still require a migration; Member 1 coordinates it separately.

## Documentation responsibility

Member 1 exclusively edits README, AGENTS files, and `docs/*`.
