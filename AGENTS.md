# Agent Instructions

## Scope

This file applies to the entire repository. Deeper `AGENTS.md` files add rules for their subtree.

Before changing workflow, ingestion, RAG, reporting, deployment, or provider code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/README.md
docs/project-status.md
docs/team-workflow.md
docs/member-contributions.md
docs/role-access-control.md
docs/multi-subject-management.md
docs/infrastructure.md
docs/ai-provider-backup.md
docs/render-deployment.md
```

## Current baseline

Synchronized with `master` after PR #40 on 2026-08-21.

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
Flow 1 MVC Documents/Chapters             [COMPLETE]
Flow 1 background indexing                [COMPLETE]
Flow 2 RAG backend                        [COMPLETE]
Flow 2 MVC Chat/history/citations          [COMPLETE]
Flow 2 MVC Evaluation                     [COMPLETE]
Flow 3 Razor Pages Reports                [COMPLETE]
Admin users/subjects                      [COMPLETE]
Render CI/CD demo deployment              [COMPLETE]
```

The obsolete internal `Pages/RagDemo` implementation was removed in PR #35. Do not recreate it as a parallel product UI.

## Documentation identity rule

Repository documentation uses **Member numbers only**.

- Do not add GitHub usernames to README, AGENTS files or `docs/*`.
- Assigned ownership and merged contribution credit are separate concepts.
- Use `docs/member-contributions.md` as the canonical ledger.
- Keep PR numbers as auditable evidence.

## Provider-neutral boundary

Application/workflow code consumes:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete providers belong in Infrastructure. `RAG_PROVIDER` remains the backward-compatible default; `RAG_CHAT_PROVIDER` and `RAG_EMBEDDING_PROVIDER` may override the two purposes independently.

Do not add hidden application-level local-to-cloud failover. Cloud use must remain explicit because it changes data egress, privacy and potentially cost.

### Embedding invariant

Changing embedding provider/model/dimension requires a complete corpus re-index.

PR #37 allows a **dimension-changing re-index** to proceed without pgvector dimension errors: retrieval filters `DocumentChunk.Embedding` with `vector_dims(...)` matching the current query vector before cosine distance. Different-dimension stale rows are temporarily ignored until re-indexed.

Do not misread that compatibility filter as permission to mix semantic vector spaces. Two different embedding models with the same dimensions remain incompatible and cannot be distinguished by `vector_dims` alone.

Chat-only provider/model changes do not require re-indexing.

## Render runtime

Current Render provider split:

```text
Rag__Provider=OpenRouter
Rag__ChatProvider=Gemini
Rag__EmbeddingProvider=OpenRouter
Rag__EmbeddingDimensions=1024
Gemini chat model=gemini-3.6-flash
OpenRouter embedding=nvidia/llama-nemotron-embed-vl-1b-v2:free
```

Both `Rag__Gemini__ApiKey` and `Rag__OpenRouter__ApiKey` are deployment secrets.

ASP.NET Core Data Protection keys are persisted in PostgreSQL through `DataProtectionKeyDbContext`. Do not reintroduce filesystem-only key storage for Render.

## Multi-subject invariant

Subject is a first-class workflow boundary.

- `Chapter.SubjectId`, `Document.SubjectId`, and `ChatSession.SubjectId` carry persisted scope.
- Flow 1 controllers preserve subject context and authorize concrete resources.
- Flow 2 session/retrieval/persistence stays subject-scoped.
- Flow 3 report snapshots, including chat aggregates, are subject-scoped after PR #40.
- Admin may manage any existing subject.
- Subject Leader may manage only assigned subjects.
- Student may use active subjects but cannot manage academic content.
- `ManageDocuments` is a coarse role policy; Flow 1/3 additionally use `ISubjectAccessService`.
- UI visibility is never authorization.
- Do not reintroduce `SeedData.Prn222SubjectId` into active workflow code.

## Flow 1 rules

- MVC only: `DocumentsController`, `ChaptersController`, `Views/Documents`, `Views/Chapters`.
- Writes require `ManageDocuments` plus subject-specific manage permission.
- Upload persists file + `Document`, then queues `Document.Id`.
- Controllers do not parse/chunk/embed/query pgvector/call provider APIs.
- Indexing consumes `ITextEmbeddingService`.
- Startup recovery handles persisted `Uploaded`/`Processing` documents.

## Flow 2 rules

Product presentation is MVC:

```text
Controllers/ChatController.cs
Views/Chat/
Controllers/EvaluationController.cs
Views/Evaluation/
```

Chat controllers consume `IRagQueryService`; they must not query pgvector or call concrete providers directly.

The current Chat UI uses **SSE over a POST fetch** (`/Chat/AskStream`) for progress/result events. There is no SignalR hub in the current chat implementation. Keep documentation and architecture claims consistent with that fact.

Grounded retrieval must preserve:

```text
selected Subject
 -> validated ChatSession
 -> ITextEmbeddingService
 -> subject + vector-dimension constrained retrieval
 -> GroundedPromptBuilder
 -> IChatCompletionService
 -> referenced citations only
 -> messages/citations bound to session
```

## Flow 3 rules

Flow 3 remains read-only Razor Pages under `Pages/Reports/`, but presentation does not access EF Core directly.

Required boundary after PR #40:

```text
PageModel
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

Reports require concrete subject context plus subject-specific manage permission. Document/index/chat metrics must stay scoped to that subject.

## Shared contracts

Important Application-facing contracts include:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `IEvaluationService`
- `IReportQueryService`
- `RagAnswer` / `RagCitation`
- `SubjectReportSnapshot`

Provider-specific DTOs and EF/PostgreSQL implementation details stay outside Application.

## Data Protection / EF Core

The solution has both `ApplicationDbContext` and `DataProtectionKeyDbContext`.

- Application schema changes use normal EF migrations.
- CI model/migration checks explicitly target `ApplicationDbContext` where required.
- The `DataProtectionKeys` table is validated in PostgreSQL CI.
- Do not use `EnsureCreated` for runtime application schema.

## UI/UX rules

- Reuse the existing design system unless Flow 2's specialized full-screen chat layout requires its current patterns.
- Preserve responsive/accessibility behavior.
- User-facing copy must not claim AI is always local when online providers are supported.
- UI must not weaken server-side authorization.

## Infrastructure and hygiene

- Never commit `.env`, API keys, credentials, uploaded documents, DB dumps, logs, build output or runtime data.
- Default branch: `master`.
- Use focused branches/PRs.
- Local Ollama Compose uses `local-ai` profile.
- Online provider runs must not require Ollama unless a selected contract uses Ollama.
- Run build/tests/pending-model/Docker/PostgreSQL validation before merge.
- Never run `docker compose down -v` unless data deletion is explicitly requested.

## Documentation rule

**Member 1 is the sole documentation editor** for README, all AGENTS files and `docs/*`.

Members 2-5 report documentation/status impacts to Member 1. Member 1 reconciles the canonical documentation set against actual merged code/configuration.
