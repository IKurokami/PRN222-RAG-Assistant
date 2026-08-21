# Team workflow and ownership

> Synchronized after PR #40 on 2026-08-21. Ownership and merged contribution credit remain separate.

## Current milestone

All three core product workflows are implemented:

```text
Flow 1 - Document Management & Indexing       COMPLETE
Flow 2 - RAG Chat/History/Citations/Evaluation COMPLETE
Flow 3 - Report & Statistics                   COMPLETE
```

Cross-cutting provider, multi-subject/RBAC, UI baseline, Render CD and documentation infrastructure are also implemented for the current demo scope.

## Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Maintains:

- domain/data/security baseline;
- shared contracts and EF migration coordination;
- Identity/RBAC;
- Subject management/assignment and authorization;
- provider selection/configuration/adapters;
- deployment/provider secret/config coordination;
- embedding compatibility/re-index rules;
- repository documentation synchronization.

## Member 2 - Flow 1 request behavior + Flow 3 reporting

Maintains established behavior for:

- Chapter/Document request semantics;
- upload/list/details/edit/delete/re-index;
- validation/authorization around Flow 1 requests;
- read-only Report & Statistics behavior.

PR #40 refactored Flow 3 data access behind `IReportQueryService`; this does not transfer report behavior ownership.

## Member 3 - indexing maintenance + cross-app UI baseline

Maintains:

- PDF/DOCX/PPTX parsers;
- chunking/indexing worker/service;
- indexing state transitions/startup recovery;
- cross-application UI/UX baseline from PR #19.

See `member-contributions.md` for actual credit on PR #9/#23/#30.

## Member 4 - Flow 2 RAG backend maintenance

Maintains:

- subject-scoped RAG query behavior;
- retrieval/grounding/no-evidence semantics;
- message/citation persistence;
- session ownership/subject validation;
- backend configuration/tests.

Concrete providers remain outside the RAG workflow boundary.

## Member 5 - Flow 2 MVC/evaluation - COMPLETE

PR #34/#35 completed the product layer:

- MVC Chat/session/history/citations;
- subject-aware navigation;
- citation reader/Markdown presentation;
- SSE progress/typewriter UX;
- 50-question Evaluation workflow/UI;
- integrated grounding/follow-up improvements.

There is no longer a pending Member 5 core milestone.

## Flow integration map

```text
                    provider infrastructure
                  /                       \
        ITextEmbeddingService        IChatCompletionService
                 |                           |
        +--------+---------+          +------+------+
        |                  |          |             |
 Flow 1 indexing     Flow 2 retrieval + grounding/generation
        |                  |                |
        +------ DocumentChunks --------------+
                           |
                    MVC Chat / Evaluation

Flow 3 Reports
  -> IReportQueryService
  -> subject-scoped read model
```

## Provider change procedure

### Chat-only change

No corpus re-index is required.

### Embedding provider/model/dimension change

1. record the new configuration;
2. treat existing vectors as stale semantic data;
3. initiate complete corpus re-index;
4. if dimensions differ, PR #37 allows old/new dimensions to coexist temporarily because retrieval filters `vector_dims`;
5. do not interpret that dimension filter as same-model/semantic compatibility;
6. consider the migration complete only when the intended corpus is re-indexed and retrieval validated.

## Secrets

Real API keys live only in local/deployment secret environments. Do not put keys in PR descriptions, screenshots, browser JavaScript, tracked appsettings, logs or docs.

## Documentation workflow

When merged code changes architecture/status/configuration:

- report the impact to the documentation coordinator;
- reconcile canonical docs against actual `master` code/config;
- preserve PR numbers as audit evidence;
- avoid keeping old “pending” statements once the feature is merged;
- label historical snapshots clearly if they intentionally preserve old state.

Project documentation uses Member numbers only.
