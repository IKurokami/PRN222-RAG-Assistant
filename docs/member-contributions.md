# Member contribution ledger

> Baseline: `master` after PR #40 on 2026-08-21.
>
> This file intentionally uses Member numbers only. Ownership and merged contribution credit are tracked separately.

## Accounting rule

- **Ownership** = scope a member is responsible for maintaining.
- **Contribution credit** = merged implementation/review/integration work actually delivered.
- Do not double-credit implementation merely because another member owns that area.
- Keep PR numbers as auditable evidence.

## Member 1

### Core platform and coordination

Member 1 owns/delivered major cross-cutting platform work including:

- Core/Data/Identity/EF architecture;
- CI/migration/schema validation;
- Admin/SubjectLeader/Student RBAC;
- multi-subject management and subject authorization;
- shared Application contract/schema coordination;
- provider/runtime configuration and adapters;
- deployment/configuration integration;
- repository documentation synchronization.

Representative merged work includes PR #4, #17, #18, #21 and #28.

### Later provider/runtime integration

Cross-cutting provider/infrastructure work also includes:

- PR #37 - Gemini embedding dimensionality and pgvector transition compatibility;
- PR #38 - PostgreSQL-persisted Data Protection keys + OpenRouter Chat fallback update;
- PR #39 - Render Chat switched to Gemini while preserving OpenRouter embeddings.

### Additional implementation outside nominal ownership

The existing ledger continues to credit:

- original end-to-end indexing implementation in PR #9;
- document chunk preview/chunking/PDF work in PR #23;
- repeated cross-workflow integration and documentation reconciliation.

Member 3 remains indexing maintenance owner even where implementation credit belongs elsewhere.

## Member 2

Member 2 delivered the main request/business behavior for Flow 1 and the original Flow 3 reporting behavior:

- Document upload/list/details/edit/delete/re-index request flow;
- Chapter management behavior;
- Flow 1 validation/authorization behavior;
- Report & Statistics dashboard behavior and report tests.

Representative merged work: PR #5 and PR #12.

PR #40 later refactored the report data-access architecture and subject-scoped Chat aggregates. Member 2 retains Flow 3 behavior ownership; the PR #40 query-boundary work is treated as cross-cutting architecture/integration under the shared-contract coordination scope rather than a transfer of Flow 3 ownership.

## Member 3

Member 3 delivered the cross-application UI/UX baseline in PR #19, including the application shell, shared design system, authentication presentation and refreshed workflow screens.

Member 3 remains the maintenance owner for document indexing/ingestion.

Implementation credit for the original PR #9/#23 indexing work and PR #30 issue #27 remediation remains assigned to the members who delivered those merges rather than being double-counted under maintenance ownership.

## Member 4

Member 4 delivered the merged Flow 2 RAG backend baseline in PR #30:

- `IRagQueryService` behavior;
- subject-scoped retrieval/session behavior;
- grounded prompt/no-evidence handling;
- provider-neutral embedding/chat calls;
- message/citation persistence;
- citation marker parsing;
- backend validation/tests.

PR #30 also credits Member 4 with the issue #27 parser/chunker remediation while Member 3 remains the indexing maintenance owner.

Member 4 remains the maintenance owner for core Flow 2 RAG backend behavior.

## Member 5

Member 5 now has merged product implementation credit for Flow 2.

### PR #34

Delivered the MVC product layer and evaluation integration:

- Chat MVC UI;
- subject-aware session/history handling;
- user-facing citations;
- 50-question Evaluation Suite and UI/service integration.

### PR #35

Delivered the major Chat/RAG product enhancement:

- full-screen modern Chat UI;
- SSE progress/typewriter experience;
- Markdown/code rendering;
- citation reader/source presentation;
- stronger grounding and inline citation behavior;
- contextual follow-up retrieval fallback;
- removal of obsolete RagDemo presentation.

These merged changes close the old documentation state that listed Member 5 as pending.

## Workflow contribution summary

| Workflow / area | Maintenance/assigned owner(s) | Merged contribution highlights |
|---|---|---|
| Core/Data/Identity/RBAC | Member 1 | Member 1 |
| Multi-subject/security integration | Member 1 | Member 1 |
| AI provider/deployment infrastructure | Member 1 | Member 1; PR #21/#28/#37/#38/#39 |
| Flow 1 request/business behavior | Member 2 | Member 2 |
| Flow 1 indexing/ingestion maintenance | Member 3 | Member 1 baseline/PR #23 + Member 4 issue #27 fixes; Member 3 maintains |
| Cross-app UI/UX baseline | Member 3 | Member 3 / PR #19 |
| Flow 2 RAG backend | Member 4 | Member 4 / PR #30; PR #35 integrated enhancements by Member 5 |
| Flow 2 MVC/evaluation | Member 5 | Member 5 / PR #34/#35 |
| Flow 3 Report & Statistics | Member 2 | Member 2 baseline + cross-cutting PR #40 integration |
| Repository docs/coordination | Member 1 | Member 1 |

## Current follow-up debt

No core Member 5 product milestone remains pending. Current follow-up work is primarily:

- deeper complex document-ingestion fixtures;
- RAG/evaluation quality validation against larger real corpora;
- hosted source-file durability beyond free ephemeral storage;
- optional future streaming/refactoring enhancements.

Update this ledger when future merges materially change ownership or contribution credit.
