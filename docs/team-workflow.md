# Team workflow and ownership

> Post-PR #30 coordination baseline. Member 1 is the sole documentation owner.

## Current milestone

- Member 1 Core/Data/Identity/RBAC/multi-subject/provider infrastructure: complete.
- Member 2 Flow 1 request/business behavior: complete.
- Member 2 Flow 3 reporting behavior: complete.
- Member 3 cross-app UI/UX redesign: complete.
- Member 3 remains the maintenance owner for Flow 1 indexing/ingestion.
- Member 4 Flow 2 RAG backend: complete / merged through PR #30.
- Member 4 also delivered the latest issue #27 parser/chunker fixes in PR #30.
- Member 5 Flow 2 MVC/history/citations/evaluation: pending.

Contribution credit is tracked separately from nominal ownership in `docs/member-contributions.md`.

## Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Owns:

- domain/data/security baseline;
- shared contracts and EF migration coordination;
- Identity configuration/seeding and RBAC rules;
- Admin/SubjectLeader/Student roles/policies;
- Subject management/assignment and `ISubjectAccessService`;
- cross-workflow subject-context integration;
- AI provider selection/configuration;
- Gemini/OpenAI/Ollama/OpenRouter adapters behind shared interfaces;
- OpenRouter free chat-model routing/fallback;
- API-key/env wiring;
- embedding dimension/vector-space/re-index compatibility rules;
- provider regression tests;
- all README/AGENTS/docs edits.

Actual merged contribution also includes the original indexing implementation from PR #9 and chunk-preview/chunking/PDF work from PR #23. These contributions are credited to Member 1 even though Member 3 remains the indexing maintenance owner.

## Member 2 - Flow 1 request behavior + Flow 3 reporting

Complete responsibilities:

- Chapter/Document request semantics;
- upload/list/details/edit/delete/re-index behavior;
- validation and authorization around Flow 1 request handling;
- read-only Report & Statistics behavior and regression tests.

Controllers/Pages must not call concrete AI providers.

## Member 3 - indexing maintenance + UI/UX redesign

### Indexing ownership

Member 3 remains responsible for maintaining:

- PDF/DOCX/PPTX parsers;
- `TextChunker`;
- indexing worker/service;
- indexing state transitions;
- coherent chunk replacement;
- startup recovery.

Contribution accounting is separate: the original merged indexing implementation in PR #9 is credited to Member 1, while the issue #27 remediation in PR #30 is credited to Member 4.

### Cross-app UI/UX redesign

Member 3 delivered and owns the PR #19 visual baseline, including the shared design system, application shell, authentication presentation and refreshed workflow screens.

## Member 4 - Flow 2 backend - COMPLETE BASELINE

Merged responsibilities now include:

- subject-scoped RAG query behavior;
- question embeddings through `ITextEmbeddingService`;
- pgvector retrieval constrained by subject context;
- grounded prompt/no-evidence behavior;
- completion through `IChatCompletionService`;
- session ownership/subject validation;
- messages/citations persistence;
- citation-marker parsing;
- backend configuration validation and tests.

Member 4 must remain provider-neutral and must not call Ollama/Gemini/OpenAI/OpenRouter directly.

PR #30 also contains Member 4's merged issue #27 remediation for chunk overlap, Unicode safety, PDF layout handling and parser/chunker regression coverage.

## Member 5 - Flow 2 MVC/evaluation - PENDING

Owns the remaining product layer:

- MVC Chat/session/history/citation actions and views;
- subject-aware conversation navigation;
- user-facing citation rendering;
- evaluation tooling and final Flow 2 product integration.

The internal RAG demo page is a development surface and does not replace the final Member 5 MVC implementation.

## Flow integration map

```text
                   Member 1 provider infrastructure
                  /                               \
        ITextEmbeddingService                IChatCompletionService
                 |                                  |
       +---------+-----------+             +--------+--------+
       |                     |             |                 |
Flow 1 indexing        Flow 2 query embedding       Flow 2 generation
[Member 3 owner]           [Member 4]                 [Member 4]
       |                     |                           |
       +---- DocumentChunks -+---- subject-scoped ------+
                                      |
                                 Member 5 MVC UI
                                    [pending]
```

## Issue #27 / ingestion follow-up

PR #30 is merged and issue #27 is closed.

PDF is the primary real-world format currently being tested most heavily. The following are deferred follow-up debt rather than blockers for that milestone:

- deeper DOCX complex-layout fixtures;
- deeper PPTX grouped-shape/table/group-transform fixtures;
- continued hardening for complex PDF table/side-note/rotated-text cases.

## Provider change procedure

If only chat provider/model/fallback order changes, no document vectors need re-indexing.

If embedding provider/model/dimension changes:

1. Member 1 records the configuration change;
2. existing stored embeddings are treated as stale;
3. re-index the entire searchable corpus;
4. do not mix old/new vectors during retrieval;
5. validate indexing/retrieval before considering the provider switch complete.

## Secrets

Real API keys live only in local/deployment secret environments. Do not put keys in PR descriptions, screenshots, tests using real credentials, browser JavaScript, tracked appsettings, logs or docs.

## Documentation and contribution workflow

Members 2-5 report code/status/doc impacts to Member 1. Member 1 reconciles docs with actual merged code.

Contribution accounting rules:

- use Member numbers only;
- do not put GitHub usernames in project documentation;
- distinguish assigned ownership from actual merged contribution;
- do not double-credit work to an owner when another member delivered the merged implementation;
- record auditable PR numbers in `docs/member-contributions.md`.
