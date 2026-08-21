# Member 1 handoff - Core/Data/RBAC/Multi-subject/AI Providers/Documentation

> Synchronized with `master` after PR #40 on 2026-08-21.

## Ownership

Member 1 owns the cross-cutting platform/integration scope:

- Domain/Data/Security baseline;
- Identity roles and policies;
- shared Application contracts and schema/migration coordination;
- Admin user/role behavior;
- Subject catalogue + Admin Subject behavior;
- Subject Leader assignment;
- subject-specific authorization service;
- cross-workflow subject-context integration;
- AI provider selection/configuration;
- Ollama/Gemini/OpenAI/OpenRouter adapters;
- API-key/env/startup validation;
- embedding migration compatibility rules;
- Data Protection persistence coordination;
- deployment/configuration integration;
- repository-wide documentation synchronization.

## Provider/runtime milestones

Representative merged provider/infrastructure work includes:

- PR #21 - provider-neutral Ollama/Gemini/OpenAI runtime;
- PR #28 - OpenRouter routing and independent chat/embedding provider selection;
- PR #37 - Gemini output dimensionality + pgvector dimension-safe transition;
- PR #38 - PostgreSQL Data Protection key persistence + expanded OpenRouter Chat fallback;
- PR #39 - Render Chat override to Gemini while preserving OpenRouter embeddings.

Current Render provider split:

```text
Chat:      Gemini / gemini-3.6-flash
Embedding: OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimensions: 1024
```

## Data Protection

PR #38 adds `DataProtectionKeyDbContext` and stores ASP.NET Core Data Protection keys in PostgreSQL. This is now part of the runtime durability baseline and should not be reverted to filesystem-only key storage for Render.

## Embedding migration rule

Changing embedding provider/model/dimension requires complete corpus re-indexing.

PR #37 allows different-dimension rows to coexist temporarily while a full re-index is in progress because retrieval filters by `vector_dims` before cosine distance. This is migration safety, not semantic compatibility between embedding models.

## Multi-subject baseline

```text
Subject
  -> Chapters
  -> Documents
  -> ChatSessions
  -> Reports
```

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

Admin manages all subjects. Subject Leaders manage assigned subjects. Public registration creates Students only.

## Flow 3 integration after PR #40

PR #40 added a shared Application-facing report contract:

```text
IReportQueryService
SubjectReportSnapshot
```

Infrastructure implements it through `ReportQueryService`; the Razor Page no longer reads `ApplicationDbContext` directly. Chat/report totals are subject-scoped.

This is cross-cutting architecture/integration under Member 1's coordination scope; Member 2 retains Flow 3 behavior ownership.

## Actual merged contribution outside nominal ownership

The canonical ledger continues to credit implementation based on merged work rather than nominal ownership. See `member-contributions.md` for PR #9, #23, #30, #34/#35 and later cross-cutting integrations.

## Cross-workflow boundary

- Member 2: Flow 1 request/business behavior + Flow 3 reporting behavior.
- Member 3: indexing/ingestion maintenance + cross-app UI baseline.
- Member 4: Flow 2 RAG backend maintenance.
- Member 5: completed Flow 2 MVC Chat/history/citation/evaluation product layer.
- Member 1: shared contracts/schema/security/provider/deployment/docs coordination.

## Documentation responsibility

Member 1 coordinates README, AGENTS files and `docs/*` against actual merged code/config. Project documentation uses Member numbers only and does not identify members by GitHub username.
