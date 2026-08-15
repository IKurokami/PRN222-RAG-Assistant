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

Provider-backup work is based on `master` after merged PR #20 on 2026-08-15.

- Main project: `src/PRN222.RagAssistant`
- Tests: `tests/PRN222.RagAssistant.Tests`
- Target: `net10.0`
- Host: ASP.NET Core MVC + Razor Pages
- Auth: ASP.NET Core Identity
- Roles: `Admin`, `SubjectLeader`, `Student`
- Policies: `ManageUsers`, `ManageSubjects`, `ManageDocuments`
- Database: PostgreSQL + pgvector
- AI runtime: provider-neutral; Ollama local default, OpenAI/Gemini online backups
- Source storage: `storage/uploads/`

<<<<<<< Updated upstream
Presentation/workflow state:
=======
The demo is scoped to PRN222. Subject Leaders curate/upload course documents. Chapters are runtime-managed data. Students consume successfully indexed content through Flow 2 chat. Automatic FLM crawling is not authoritative ingestion.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - Razor Pages
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - **ASP.NET Core MVC**
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages

Conversation History belongs to Flow 2 and is not counted as Flow 3.

## Current merged milestone

Latest synchronized baseline after PR #12:

Latest synchronized baseline after PR #12 (Member 2 Flow 3 Report & Statistics). Main remaining product work is Flow 2 RAG Q&A, now fixed to ASP.NET Core MVC Controllers + Views.

PR #9 completed the Member 2 -> Member 3 Flow 1 handoff. PR #12 completed Member 2's independent Flow 3 reporting assignment. The main unfinished product work is now Flow 2, and its presentation model is fixed to ASP.NET Core MVC rather than Razor Pages.

When status is unclear, latest merged code on `master` is the source of truth; synchronize docs afterward.

## Team ownership and integration boundaries

### Member 1 - Core/Data Lead - COMPLETE BASELINE

Owns:

- `Domain/Entities/`
- `Domain/Enums/`
- `Data/`
- `Security/`
- cross-workflow `Application/` contracts/models
- schema/migration conventions
- architecture/convention tests
- coordination for genuine EF model/migration changes

Do not move later workflow business logic into Member 1 simply because shared contracts live under `Application/`.

### Member 2 - Document Management + Report & Statistics - COMPLETE CURRENT ASSIGNMENT

#### Flow 1 request/presentation - COMPLETE

Merged behavior includes:

- runtime PRN222 Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index request
- PDF/DOCX/PPTX upload validation
- 50 MB limit
- source-file persistence
- document metadata persistence
- PRN222 chapter validation
- `AppPolicies.ManageDocuments` server-side enforcement
- queue handoff after document persistence
- safe chapter deletion that unassigns referenced documents first

Flow 1 request handlers must not parse, chunk, embed, query pgvector, or call Ollama.

#### Flow 3 Report & Statistics - COMPLETE

PR #12 merged a read-only Subject Leader reporting dashboard under `Pages/Reports/`.

Implemented reporting includes:

- total PRN222 chapters/documents
- documents by indexing state
- documents by chapter/unassigned
- total PRN222 `DocumentChunk` count
- indexing completion percentage
- recent indexing failures / `IndexError`
- recently indexed documents with chunk counts/timestamps
- total chat sessions/messages/citations
- graceful empty/zero states before Flow 2 data exists

The Reports page uses `AppPolicies.ManageDocuments` and is server-side restricted to `SubjectLeader`.

Flow 3 must remain read-only and must not:

- enqueue/re-index documents as part of reporting
- alter parser/chunker/embedding/worker behavior
- mutate workflow data
- run pgvector similarity retrieval
- call Ollama
- duplicate Member 5 Conversation History UI
- add speculative analytics entities/migrations
- change shared contracts solely for dashboard convenience

If reporting exposes a genuine persistence gap, coordinate through Member 1.

Do not recreate `feature/report-statistics` as a competing implementation; Flow 3 is already merged.

### Member 3 - Document Indexing / Ingestion - COMPLETE

PR #9 merged:

- `DocumentParserFactory`
- PDF parser via PdfPig
- DOCX/PPTX parsers via OpenXml
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- single and ordered-batch `ITextEmbeddingService` support
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- coherent `DocumentChunk` replacement
- index state/error/timestamp persistence
- startup rehydration of `Uploaded`/`Processing` documents

Implemented state flow:
>>>>>>> Stashed changes

```text
Flow 1 -> MVC Controllers + Views [COMPLETE]
Flow 2 -> MVC Controllers + Views [PENDING]
Flow 3 -> Razor Pages             [COMPLETE]
Auth/shell -> Razor Pages         [COMPLETE]
Admin user management -> MVC      [COMPLETE]
Admin subject management -> MVC   [COMPLETE]
Cross-app UI/UX redesign          [COMPLETE - PR #19 - Member 3]
AI provider foundation            [IMPLEMENTED - Member 1]
```

PRN222 remains the seeded demo subject. Do not treat PRN222 as the application-wide hard-coded subject scope.

## AI provider invariant

Application/workflow code consumes:

```text
ITextEmbeddingService
IChatCompletionService
```

Concrete provider selection belongs to Infrastructure and is controlled by:

```text
Rag:Provider = Ollama | OpenAI | Gemini
```

Environment mapping uses `RAG_PROVIDER`. API keys are server-side secrets and must never be committed, logged, rendered to clients, or placed in tracked appsettings files.

Supported provider stacks:

```text
Ollama:
  chat      qwen3:4b
  embedding qwen3-embedding:0.6b

OpenAI:
  chat      gpt-5.6-luna
  embedding text-embedding-3-small

Gemini:
  chat      gemini-3.6-flash
  embedding gemini-embedding-2
```

Do not add silent automatic local-to-cloud failover. Cloud use must be an explicit operator choice because it changes data egress and API cost.

Default embedding dimensions are `1024`. Equal dimensions do not imply compatible vector spaces. Whenever the embedding provider, embedding model, or configured dimensions change, re-index the whole corpus before similarity retrieval. Never mix chunks produced by different embedding models in one searchable corpus.

Canonical details: `docs/ai-provider-backup.md`.

## Multi-subject invariant

Subject is a first-class boundary.

- `Document.SubjectId` and `Chapter.SubjectId` determine ownership.
- Flow 1 and Flow 3 operate with a concrete subject context.
- Admin may manage any existing subject.
- Subject Leader may manage only assigned subjects.
- Student may view active subjects but cannot manage them.
- Subject Leader assignment is stored as Identity user claims of type `AppClaimTypes.ManagedSubject`.
- `ManageDocuments` is a coarse role policy; subject-specific operations must additionally call `ISubjectAccessService`.
- UI visibility is never authorization.
- Do not reintroduce `SeedData.Prn222SubjectId` into workflow controllers/pages as active scope.
- Do not hard-delete Subjects while dependent workflow data exists; use `IsActive` lifecycle for now.

## Roles and policies

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Admin owns users, roles, subjects, Subject Leader assignments, and may perform subject-content operations as an override.

Subject Leader owns academic content only for assigned subjects.

If a user is changed away from `SubjectLeader`, their managed-subject claims must be cleared.

Public self-registration is allowed only as `Student`.

## Team ownership

### Member 1 - Core/Data/RBAC/multi-subject/provider foundation/docs

Member 1 owns:

- `Domain/Entities/`, `Domain/Enums/`, `Data/`, `Security/`;
- shared `Application/` contracts/models and schema/migration coordination;
- Identity setup/seeding and RBAC rules;
- Admin user/role management;
- Admin Subject management and Subject Leader assignment;
- `ISubjectAccessService` and subject-context authorization;
- cross-workflow subject-context wiring;
- role/subject authorization tests;
- AI provider selection/configuration and concrete provider adapters;
- API-key/environment wiring and embedding-dimension compatibility rules;
- provider-registration/adapter tests;
- all repository documentation edits.

Member 1 provider ownership is cross-cutting infrastructure. It does not transfer Member 3's indexing workflow ownership or Member 4's future Flow 2 RAG behavior.

### Member 2 - Flow 1 request behavior + Flow 3 reporting behavior

Owns established document/chapter CRUD/upload/re-index request behavior and read-only reporting behavior.

Do not put provider HTTP calls in MVC/Razor request code.

### Member 3 - indexing + completed UI/UX redesign

Member 3 owns the parser/chunker/indexing worker/service pipeline and startup recovery. The indexing pipeline now consumes whichever `ITextEmbeddingService` Member 1's provider configuration selects.

Member 3 also owns the completed cross-application UI/UX redesign merged in PR #19.

Indexing remains document-ID driven. Do not create one indexing pipeline per subject or provider.

### Member 4 - Flow 2 backend - PENDING

Flow 2 must remain provider-neutral and subject-scoped.

```text
selected subject
    -> provider-selected question embedding
    -> only indexed Documents of that Subject
    -> only their DocumentChunks
    -> provider-selected grounded generation
    -> citations/messages bound to that Subject
```

Member 4 consumes `ITextEmbeddingService` and `IChatCompletionService`; do not call Ollama/OpenAI/Gemini directly.

### Member 5 - Flow 2 MVC presentation/evaluation - PENDING

Owns future MVC chat/session/history/citation UI and evaluation tooling. Do not call provider APIs from controllers/views.

## Flow 1 rules

- MVC only: `DocumentsController`, `ChaptersController`, `Views/Documents`, `Views/Chapters`.
- Writes require `ManageDocuments` and subject-specific manage permission.
- Upload persists file + `Document`, then queues `Document.Id`.
- Controllers do not parse/chunk/embed/query pgvector/call AI providers.
- Provider changes do not change Flow 1 request semantics.
- If the embedding provider/model/dimension changes, all indexed documents need re-indexing.

## Flow 3 rules

- Razor Pages under `Pages/Reports/`.
- Requires concrete `subjectId` and subject-specific manage permission.
- Reports are read-only.
- Reports do not call AI providers or similarity retrieval.
- Chat metrics remain temporarily global until Flow 2 adds subject-scoped chat persistence.

## UI/UX rules after PR #19

- Reuse `wwwroot/css/design-tokens.css` and `wwwroot/css/components.css`.
- Preserve responsive/accessibility behavior.
- UI must not weaken server-side authorization.
- Public registration may create only `Student` accounts.
- User-facing privacy/product copy must not claim AI is always local when OpenAI/Gemini mode is supported.

## EF Core rules

1. No navigation properties in domain entities; use scalar foreign keys.
2. Mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` types.
3. Keep `ApplicationDbContext` thin.
4. Use EF migrations for actual schema changes.
5. Do not use `EnsureCreated` for application runtime schema.
6. Preserve explicit delete behaviors and architecture tests.

The provider-backup implementation does not require an EF migration. `DocumentChunk.Embedding` remains provider-neutral storage.

## Shared contracts

Cross-member contracts live under `Application/`.

Current important contracts:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Provider-specific request/response DTOs stay in Infrastructure, not Application.

## Infrastructure and hygiene

- Never commit `.env`, API keys, credentials, uploaded documents, DB dumps, logs, build output, or runtime data.
- Default branch: `master`.
- Use focused branches/PRs.
- Local Ollama Compose uses the `local-ai` profile.
- Online provider runs must not require the Ollama container.
- Run build/tests/pending-model/Docker validation before merge.
- Never run `docker compose down -v` unless explicitly requested.

## Documentation rule

**Member 1 is the sole documentation editor** for README, all AGENTS files, and `docs/*`.

Members 2-5 communicate doc/status changes to Member 1.
