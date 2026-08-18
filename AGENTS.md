# Agent Instructions

## Scope

This file applies to the entire repository. Deeper `AGENTS.md` files add rules for their subtree.

Before changing workflow, ingestion, RAG or AI-provider code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/member-contributions.md
docs/role-access-control.md
docs/multi-subject-management.md
docs/infrastructure.md
docs/ai-provider-backup.md
```

## Current baseline

Synchronized after PR #30 merged and issue #27 closed on 2026-08-18.

- Main project: `src/PRN222.RagAssistant`
- Tests: `tests/PRN222.RagAssistant.Tests`
- Target: `net10.0`
- Host: ASP.NET Core MVC + Razor Pages
- Auth: ASP.NET Core Identity
- Roles: `Admin`, `SubjectLeader`, `Student`
- Policies: `ManageUsers`, `ManageSubjects`, `ManageDocuments`
- Database: PostgreSQL + pgvector
- AI runtime: provider-neutral; Ollama/Gemini/OpenAI/OpenRouter
- Source storage: `storage/uploads/`

Workflow state:

```text
Flow 1 -> MVC Controllers + Views [COMPLETE]
Flow 2 backend              [COMPLETE - Member 4]
Flow 2 MVC product UI       [PENDING  - Member 5]
Flow 3 -> Razor Pages       [COMPLETE]
Auth/shell -> Razor Pages   [COMPLETE]
Admin user management       [COMPLETE]
Admin subject management    [COMPLETE]
Cross-app UI/UX baseline    [COMPLETE - Member 3]
AI provider infrastructure  [COMPLETE - Member 1]
```

PRN222 remains the seeded demo subject. Do not treat PRN222 as the global hard-coded application scope.

## Contribution accounting rule

Repository documentation uses **Member numbers only**.

- Do not add GitHub usernames to README, AGENTS files or `docs/*`.
- Assigned ownership and actual implementation credit are separate concepts.
- Do not double-credit work to an owner when another member delivered the merged implementation.
- Use `docs/member-contributions.md` as the canonical contribution ledger.
- Keep PR numbers as auditable evidence.

Current examples:

- Member 1 receives implementation credit for PR #9 and PR #23 even though Member 3 owns indexing maintenance.
- Member 4 receives implementation credit for the issue #27 remediation in PR #30 even though Member 3 owns indexing maintenance.

## AI provider invariant

Application/workflow code consumes:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete provider selection belongs to Infrastructure.

Backward-compatible default:

```text
Rag:Provider / RAG_PROVIDER = Ollama | OpenAI | Gemini | OpenRouter
```

Optional purpose-specific overrides:

```text
Rag:ChatProvider / RAG_CHAT_PROVIDER
Rag:EmbeddingProvider / RAG_EMBEDDING_PROVIDER
```

Blank overrides inherit `RAG_PROVIDER`.

Do not add hidden application-level automatic local-to-cloud failover. Cloud use must remain explicit because it changes data egress, privacy and potentially cost.

Default embedding dimensions are 1024. Equal dimensions do not imply compatible vector spaces.

**Never mix embedding models/providers within one searchable corpus.** If embedding provider/model/dimension changes, re-index the whole corpus before retrieval. Chat-only provider/model changes do not require re-indexing.

## Multi-subject invariant

Subject is a first-class boundary.

- `Document.SubjectId` and `Chapter.SubjectId` determine content ownership.
- `ChatSession.SubjectId` determines chat/RAG subject context.
- Flow 1 and Flow 3 operate with concrete subject context.
- Flow 2 backend now carries subject context through session/retrieval/persistence.
- Admin may manage any existing subject.
- Subject Leader may manage only assigned subjects.
- Student may view active subjects but cannot manage them.
- Subject Leader assignment is stored as Identity claims.
- `ManageDocuments` is a coarse role policy; subject-specific operations additionally use `ISubjectAccessService`.
- UI visibility is never authorization.
- Do not reintroduce `SeedData.Prn222SubjectId` into active workflow code.

## Roles and policies

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Public self-registration may create only `Student` accounts.

## Team ownership

### Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Owns:

- Domain/Data/Security architecture;
- shared Application contracts/models and migration coordination;
- Identity/RBAC and Admin user/role management;
- Subject management/assignment and subject authorization;
- provider selection/configuration and concrete provider adapters;
- provider/env/embedding compatibility rules;
- repository documentation synchronization.

Member 1 also has contribution credit for merged implementation outside nominal ownership, documented in `docs/member-contributions.md`.

### Member 2 - Flow 1 request behavior + Flow 3 reporting

Owns established document/chapter request/business behavior and read-only reporting behavior.

Request handlers must not call concrete AI providers or perform embedding/retrieval work directly.

### Member 3 - indexing maintenance + UI/UX

Owns ongoing maintenance of:

- PDF/DOCX/PPTX parsers;
- `TextChunker`;
- indexing worker/service;
- indexing state transitions and coherent chunk replacement;
- startup recovery.

Member 3 also owns the completed cross-app UI/UX baseline from PR #19.

Contribution credit for PR #9/#23 belongs to Member 1 and PR #30 issue #27 remediation belongs to Member 4.

### Member 4 - Flow 2 backend - COMPLETE BASELINE

Owns:

- subject-scoped RAG query behavior;
- question embeddings through `ITextEmbeddingService`;
- pgvector retrieval constrained by subject;
- grounded prompt/no-evidence behavior;
- completion through `IChatCompletionService`;
- session ownership/subject validation;
- conversation history loading;
- message/citation persistence and citation marker parsing;
- backend configuration validation and tests.

Do not call concrete providers directly.

### Member 5 - Flow 2 MVC presentation/evaluation - PENDING

Owns:

- final MVC Chat/session/history/citation controllers/views;
- subject-aware conversation navigation;
- user-facing citation rendering;
- evaluation tooling.

The internal RAG demo Razor Page is not the final Member 5 product UI.

## Flow 1 rules

- MVC only: `DocumentsController`, `ChaptersController`, `Views/Documents`, `Views/Chapters`.
- Writes require `ManageDocuments` plus subject-specific manage permission.
- Upload persists file + `Document`, then queues `Document.Id`.
- Controllers do not parse/chunk/embed/query pgvector/call provider APIs.
- Indexing consumes `ITextEmbeddingService`.

### Issue #27 baseline

Issue #27 is closed after PR #30.

Merged hardening includes deterministic bounded overlap, Unicode normalization/grapheme safety, configurable chunking, improved PDF multi-column reading order, PDF regression tests, DOCX fake-page correction and additional parser/integration coverage.

PDF is the primary real-world format currently receiving the most testing.

Deferred follow-up debt:

- deeper DOCX complex-layout/list/table fixtures;
- deeper PPTX grouped-shape/table/group-transform fixtures;
- further complex PDF table/side-note/rotated-text hardening.

## Flow 2 rules

Product code must use subject-aware sessions and the RAG service contract.

```text
selected subject
    -> subject-aware ChatSession
    -> ITextEmbeddingService
    -> indexed Documents of that Subject only
    -> DocumentChunks
    -> grounded generation through IChatCompletionService
    -> citations/messages/history bound to the same subject context
```

Do not intentionally fall back to global-corpus retrieval in product paths.

## Flow 3 rules

- Razor Pages under `Pages/Reports/`.
- Reports are read-only and provider-independent.
- Require concrete subject context and subject-specific manage permission.
- Because `ChatSession.SubjectId` now exists, chat aggregate scoping should be audited when Member 5 completes Flow 2.

## UI/UX rules

- Reuse `wwwroot/css/design-tokens.css` and `wwwroot/css/components.css`.
- Preserve responsive/accessibility behavior.
- UI must not weaken server-side authorization.
- User-facing copy must not claim AI is always local when online providers are supported.

## EF Core rules

1. No navigation properties in domain entities; use scalar foreign keys.
2. Mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` types.
3. Keep `ApplicationDbContext` thin.
4. Use EF migrations for real schema changes.
5. Do not use `EnsureCreated` for application runtime schema.
6. Preserve explicit delete behaviors and architecture tests.

## Shared contracts

Current important contracts:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Provider-specific DTOs stay in Infrastructure, not Application.

## Infrastructure and hygiene

- Never commit `.env`, API keys, credentials, uploaded documents, DB dumps, logs, build output or runtime data.
- Default branch: `master`.
- Use focused branches/PRs.
- Local Ollama Compose uses the `local-ai` profile.
- Online provider runs must not require Ollama unless a selected contract uses Ollama.
- Run build/tests/pending-model/Docker/PostgreSQL validation before merge.
- Never run `docker compose down -v` unless explicitly requested.

## Documentation rule

**Member 1 is the sole documentation editor** for README, all AGENTS files and `docs/*`.

Members 2-5 communicate doc/status changes to Member 1.
