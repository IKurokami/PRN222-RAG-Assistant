# Agent Instructions

## Scope

This file applies to the entire repository. Deeper `AGENTS.md` files add rules for their subtree.

Before changing workflow or AI-provider code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/role-access-control.md
docs/multi-subject-management.md
docs/infrastructure.md
docs/ai-provider-backup.md
```

## Current baseline

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

```text
Flow 1 -> MVC [COMPLETE]
Flow 2 -> MVC [PENDING]
Flow 3 -> Razor Pages [COMPLETE]
Cross-app UI/UX redesign -> COMPLETE / Member 3
AI provider routing/fallback foundation -> Member 1
```

PRN222 remains the seeded demo subject. Do not treat it as the application-wide hard-coded subject scope.

## AI provider invariant

Application/workflow code consumes only:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete selection belongs to Infrastructure.

Backward-compatible default:

```text
Rag:Provider / RAG_PROVIDER = Ollama | OpenAI | Gemini | OpenRouter
```

Optional independent overrides:

```text
Rag:ChatProvider / RAG_CHAT_PROVIDER
Rag:EmbeddingProvider / RAG_EMBEDDING_PROVIDER
```

Each override inherits `RAG_PROVIDER` when blank. Do not duplicate provider selection inside MVC, Razor Pages, indexing, or Flow 2 business code.

### Chat fallback

OpenRouter chat may intentionally use an ordered `models` chain. The default chain is:

```text
google/gemma-4-26b-a4b-it:free
 -> nvidia/nemotron-3-ultra-550b-a55b:free
 -> openrouter/free
```

This is allowed because chat fallback does not mutate the stored embedding vector space. OpenRouter provider/model failover remains inside the Infrastructure adapter.

Do not implement hidden automatic switching from an explicitly selected non-OpenRouter provider to another cloud provider. Cloud use must still be an operator choice because it changes data egress/cost/privacy behavior.

### Embedding invariant

Default dimensions are `1024`.

Embeddings must use exactly one configured model for a searchable corpus. **Do not add an embedding-model rotation list.** Equal dimensions do not imply compatible vector spaces.

Whenever any of these changes, re-index the whole corpus before similarity retrieval:

```text
RAG_EMBEDDING_PROVIDER
RAG_PROVIDER (when embedding override is blank)
*_EMBEDDING_MODEL
RAG_EMBEDDING_DIMENSIONS
```

API keys are server-side secrets and must never be committed, logged, rendered to clients, or placed in tracked appsettings files.

Canonical details: `docs/ai-provider-backup.md`.

## Multi-subject invariant

- `Document.SubjectId` and `Chapter.SubjectId` determine ownership.
- Flow 1 and Flow 3 operate with a concrete subject context.
- Admin may manage any subject.
- Subject Leader may manage only assigned subjects.
- Student may view active subjects but cannot manage them.
- `ManageDocuments` is only a coarse role gate; use `ISubjectAccessService` for subject-specific operations.
- UI visibility is never authorization.
- Do not reintroduce a hard-coded PRN222 subject boundary.

## Team ownership

### Member 1 - Core/Data/RBAC/multi-subject/provider/docs

Owns Domain/Data/Security/shared contracts/schema coordination, Identity/RBAC, subject administration/authorization, AI provider selection/configuration/adapters, OpenRouter fallback plumbing, environment/secret wiring, provider tests, embedding compatibility/re-index coordination, and all repository documentation.

### Member 2 - Flow 1 request behavior + Flow 3 reporting

Owns established document/chapter request semantics and reporting behavior. Do not put provider HTTP calls in MVC/Razor request code.

### Member 3 - indexing + completed UI/UX redesign

Owns parser/chunker/indexing worker/service/startup recovery and the completed PR #19 UI/UX redesign. Indexing consumes `ITextEmbeddingService`; it does not branch by provider.

### Member 4 - Flow 2 backend - PENDING

Must implement subject-scoped retrieval/grounding/persistence using `ITextEmbeddingService` and `IChatCompletionService`. Do not call Ollama/OpenAI/Gemini/OpenRouter directly.

### Member 5 - Flow 2 MVC/evaluation - PENDING

Owns future chat/session/history/citation UI and evaluation tooling. Provider APIs never belong in controllers/views.

## Flow rules

Flow 1 remains MVC. Upload persists file + Document and queues `Document.Id`; controllers do not parse/chunk/embed/query pgvector/call AI providers. Provider changes do not alter request semantics.

Flow 3 remains Razor Pages, subject-scoped, read-only, and provider-independent.

Flow 2 must use the selected subject as the retrieval/citation/session boundary.

## EF Core rules

1. No navigation properties in domain entities; use scalar foreign keys.
2. Mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` types.
3. Keep `ApplicationDbContext` thin.
4. Use EF migrations for actual schema changes.
5. Do not use `EnsureCreated` for runtime schema.
6. Preserve explicit delete behaviors and architecture tests.

Provider routing/fallback requires no EF migration. `DocumentChunk.Embedding` remains provider-neutral vector storage.

## Infrastructure and hygiene

- Never commit `.env`, API keys, credentials, uploaded documents, DB dumps, logs, build output, or runtime data.
- Default branch: `master`.
- Use focused branches/PRs.
- Ollama Compose uses the `local-ai` profile.
- Online-only provider runs must not require Ollama.
- Run build/tests/pending-model/Docker validation before merge.
- Never run `docker compose down -v` unless explicitly requested.

## Documentation rule

**Member 1 is the sole documentation editor** for README, all AGENTS files, and `docs/*`. Members 2-5 report doc/status impacts to Member 1.
