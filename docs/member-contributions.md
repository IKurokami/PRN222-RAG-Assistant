# Member contribution ledger

> Baseline: `master` after PR #30 merged and issue #27 closed on 2026-08-18.
>
> This file intentionally uses **Member numbers only**. Repository documentation must not identify members by GitHub username.

## Accounting rule

Project ownership and contribution credit are tracked separately.

- **Ownership** means the member responsible for maintaining a scope going forward.
- **Contribution credit** means the member who actually delivered merged implementation/review/integration work.
- A contribution is not credited twice simply because another member owns that area.
- PR numbers are retained as auditable evidence of merged work.

## Member 1

### Core platform and coordination

Member 1 delivered and/or integrated the core repository foundation, including:

- application/runtime infrastructure, Docker Compose and PostgreSQL/pgvector baseline;
- Identity, EF Core entities/configurations/migrations and architecture conventions;
- shared Application contracts for indexing, embeddings, chat and RAG;
- repository CI, migration validation and integration guardrails;
- Admin/SubjectLeader/Student RBAC and policy architecture;
- multi-subject management, Subject Leader assignment and subject-specific authorization;
- repository-wide documentation synchronization and cross-member integration rules.

Representative merged work: PR #4, PR #17 and PR #18.

### AI provider infrastructure

Member 1 delivered the provider-neutral runtime and provider configuration layer:

- Ollama/Gemini/OpenAI/OpenRouter adapters behind shared interfaces;
- independent chat/embedding provider selection;
- OpenRouter free-chat fallback routing;
- embedding-dimension/vector-space re-index invariants;
- provider configuration, environment wiring and regression tests.

Representative merged work: PR #21 and PR #28.

### Additional implementation outside nominal ownership

Member 1 also contributed implementation inside scopes normally owned by other members. These are credited to Member 1 rather than double-counted under the nominal owner:

- initial end-to-end document parsing/chunking/embedding/background-indexing implementation merged in PR #9;
- document chunk preview, semantic chunk-boundary work and PDF extraction improvements merged in PR #23;
- repeated integration/review/documentation work required to keep Flow 1, Flow 2 and Flow 3 compatible.

Member 3 remains the maintenance owner of the indexing scope, but the merged implementation credit above belongs to Member 1.

## Member 2

Member 2 delivered the main request/business behavior for Flow 1 and the reporting implementation for Flow 3:

- document upload/list/details/edit/delete/re-index request flow;
- Chapter management behavior and Flow 1 request integration;
- validation and authorization around document management;
- Report & Statistics dashboard with document/chapter/indexing/chat aggregate metrics;
- Flow 3 authorization, zero-state handling and report regression tests.

Representative merged work: PR #5 and PR #12.

## Member 3

Member 3 delivered the current application-wide UI/UX baseline:

- landing-page redesign;
- shared visual design system and component styling;
- authentication/account presentation redesign;
- refreshed Subjects/Admin/Chapters/Documents/Reports screens;
- public Student registration presentation/integration;
- responsive visual polish and application-shell improvements.

Representative merged work: PR #19.

Member 3 remains the assigned owner for the document indexing/ingestion scope. For contribution accounting, however, the original merged indexing implementation from PR #9 is credited to Member 1 and the later issue #27 fixes from PR #30 are credited to Member 4.

## Member 4

Member 4 delivered the merged Flow 2 backend baseline and the latest document-chunking remediation:

### Flow 2 RAG backend

- `IRagQueryService` backend implementation;
- question embedding through the provider-neutral embedding contract;
- pgvector retrieval;
- subject-scoped chat session/retrieval behavior;
- grounded prompt construction and no-evidence behavior;
- completion through the provider-neutral chat contract;
- chat message/citation persistence;
- citation marker parsing so only referenced sources are persisted/rendered;
- session ownership validation;
- RAG option validation and service registration;
- failure-path, subject-scope, citation and persistence tests.

### Issue #27 follow-up

Member 4 also delivered the latest fixes for malformed document chunks merged in PR #30:

- bounded/deterministic overlap behavior;
- Unicode normalization and safer grapheme boundaries;
- configurable chunking options with startup validation;
- improved PDF two-column reading order and PDF regression tests;
- DOCX blank-paragraph/page-number correction;
- additional DOCX/PPTX parser improvements and integration coverage;
- remediation of the review findings carried forward from PR #29.

Representative merged work: PR #30.

## Member 5

At this baseline, no merged product implementation is credited to Member 5 yet.

Member 5 remains responsible for the remaining Flow 2 product presentation/evaluation scope:

- MVC Chat/session/history/citation UI;
- subject-aware conversation navigation;
- user-facing citation rendering;
- evaluation workflow/tooling and final Flow 2 product integration.

The internal RAG demo page is not a substitute for the final Member 5 MVC Flow 2 experience.

## Workflow contribution summary

| Workflow / area | Assigned owner(s) | Merged contribution credit |
|---|---|---|
| Core/Data/Identity/RBAC | Member 1 | Member 1 |
| Multi-subject/security integration | Member 1 | Member 1 |
| AI provider infrastructure | Member 1 | Member 1 |
| Flow 1 request/business behavior | Member 2 | Member 2 |
| Flow 1 indexing/ingestion maintenance | Member 3 | Member 1 baseline + Member 4 issue #27 fixes; Member 3 retains maintenance ownership |
| Cross-app UI/UX | Member 3 | Member 3 |
| Flow 2 RAG backend | Member 4 | Member 4 |
| Flow 2 MVC/evaluation | Member 5 | Pending |
| Flow 3 Report & Statistics | Member 2 | Member 2 + Member 1 subject/RBAC integration |
| Repository docs/coordination | Member 1 | Member 1 |

## Follow-up technical debt

The project currently validates the PDF path most heavily because PDF is the primary real-world ingestion format under active testing.

The following items are intentionally tracked as later follow-up work rather than blockers for the PR #30 merge:

- deeper DOCX regression fixtures for complex lists/tables/layout combinations;
- deeper PPTX regression fixtures for grouped shapes, tables and group transforms;
- continued hardening of complex PDF table/side-note/rotated-text layouts;
- final Member 5 MVC Flow 2 product UI and evaluation integration.

When these items are implemented, update this ledger using the same contribution-accounting rule.
