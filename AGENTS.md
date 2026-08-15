# Agent Instructions

## Scope

This file applies to the entire repository. Deeper `AGENTS.md` files add rules for their subtree.

Before changing workflow code, read:

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/role-access-control.md
docs/multi-subject-management.md
docs/infrastructure.md
```

## Current baseline

Documentation is synchronized with `master` after merged PR #19 (2026-08-15).

- Main project: `src/PRN222.RagAssistant`
- Tests: `tests/PRN222.RagAssistant.Tests`
- Target: `net10.0`
- Host: ASP.NET Core MVC + Razor Pages
- Auth: ASP.NET Core Identity
- Roles: `Admin`, `SubjectLeader`, `Student`
- Policies: `ManageUsers`, `ManageSubjects`, `ManageDocuments`
- Database: PostgreSQL + pgvector
- AI runtime: Ollama
- Source storage: `storage/uploads/`

Presentation/workflow state:

```text
Flow 1 -> MVC Controllers + Views [COMPLETE]
Flow 2 -> MVC Controllers + Views [PENDING]
Flow 3 -> Razor Pages             [COMPLETE]
Auth/shell -> Razor Pages         [COMPLETE]
Admin user management -> MVC      [COMPLETE]
Admin subject management -> MVC   [COMPLETE]
Cross-app UI/UX redesign          [COMPLETE - PR #19 - Member 3]
```

PRN222 remains the seeded demo subject. **Do not treat PRN222 as the application-wide hard-coded subject scope.**

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
- Do not create a parallel SubjectLeader-assignment table unless requirements outgrow the Identity-claim design.
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

Public self-registration is allowed only as `Student`. Never expose Admin/SubjectLeader selection on the public registration form.

Canonical details: `docs/role-access-control.md` and `docs/multi-subject-management.md`.

## Team ownership

### Member 1 - Core/Data/RBAC/multi-subject/docs

Member 1 owns:

- `Domain/Entities/`, `Domain/Enums/`, `Data/`, `Security/`;
- shared `Application/` contracts/models and schema/migration coordination;
- Identity setup/seeding and RBAC rules;
- Admin user/role management;
- Admin Subject management and Subject Leader assignment;
- `ISubjectAccessService` and subject-context authorization;
- cross-workflow subject-context wiring, including necessary Flow 1/Flow 3 controller/view changes;
- role/subject authorization tests;
- shared role-aware navigation authorization rules;
- **all repository documentation edits**.

Member 1 ownership of cross-cutting infrastructure does not transfer original Flow 1/Flow 3 business logic ownership from Member 2.

### Member 2 - Flow 1 request behavior + Flow 3 reporting behavior

Owns established document/chapter CRUD/upload/re-index request behavior and read-only reporting behavior.

Do not:

- redefine roles/policies;
- bypass `ISubjectAccessService`;
- reintroduce PRN222 hard-coding;
- edit repository docs independently;
- recreate Flow 1 Razor Pages.

The PR #19 visual redesign does not transfer Member 2 business logic ownership.

### Member 3 - indexing + completed UI/UX redesign

Member 3 owns the completed parser/chunker/embedding/indexing worker/service and startup recovery implementation.

Member 3 also owns the **completed cross-application UI/UX redesign merged in PR #19**, including the design system, landing/auth presentation, and visual refresh across existing Admin/Subject/Chapter/Document/Report screens. This task is complete and must not be listed as unassigned.

The UI/UX assignment is presentation ownership only. It does not transfer RBAC/business logic ownership, and it does not replace Member 5's future Flow 2 MVC/history/citation/evaluation ownership.

Canonical handoff: `docs/member-3-ui-ux-handoff.md`.

Indexing is document-ID driven. A document already carries `SubjectId`; do not create one indexing pipeline per subject.

### Member 4 - Flow 2 backend - PENDING

Before implementing retrieval, coordinate subject context with Member 1. Global-corpus retrieval is forbidden.

Flow 2 backend must ensure:

```text
selected subject
    -> only indexed Documents of that Subject
    -> only their DocumentChunks
    -> grounded answer/citations from that Subject
```

Chat persistence must acquire subject ownership before Flow 2 is considered complete. A schema/contract change may be required because current `ChatSession` has no `SubjectId`; Member 1 coordinates that change/migration.

Member 4 remains presentation-agnostic.

### Member 5 - Flow 2 MVC presentation/evaluation - PENDING

Owns future MVC chat/session/history/citation UI and evaluation tooling. Preserve subject selection/context in all Flow 2 routes/forms/history UI. Do not implement Flow 2 as Razor Pages and do not edit repository docs independently.

Member 5 should reuse the PR #19 design tokens/components instead of creating a parallel visual system.

## Flow 1 rules

- MVC only: `DocumentsController`, `ChaptersController`, `Views/Documents`, `Views/Chapters`.
- Read access requires authenticated subject visibility.
- Writes require `ManageDocuments` **and** subject-specific manage permission.
- Chapter validation is scoped to the same subject as the document.
- Upload persists file + `Document`, then queues `Document.Id`.
- Controllers do not parse/chunk/embed/query pgvector/call Ollama.
- Chapter deletion unassigns affected documents; it does not delete them.
- Search/status filters and filter-preserving delete/re-index redirects introduced with PR #19 must preserve authorization and subject boundaries.

## Flow 3 rules

- Razor Pages under `Pages/Reports/`.
- Requires a concrete `subjectId` and subject-specific manage permission.
- Chapter/document/index/chunk metrics are subject-scoped.
- Reports are read-only and never call Ollama or similarity retrieval.
- Chat metrics are temporarily global until Flow 2 adds subject-scoped chat persistence; keep this limitation explicit.
- PR #19 presentation changes do not alter reporting semantics.

## UI/UX rules after PR #19

- Reuse `wwwroot/css/design-tokens.css` and `wwwroot/css/components.css` before adding one-off styles.
- Bootstrap Icons are managed through `libman.json`; do not commit generated/vendor assets outside the intended LibMan pattern.
- Preserve responsive/accessibility behavior when editing redesigned screens.
- UI must not weaken server-side authorization.
- Public registration may create only `Student` accounts.
- Future Flow 2 MVC UI should visually integrate with the existing design system.

## EF Core rules

1. No navigation properties in domain entities; use scalar foreign keys.
2. Mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` types.
3. Keep `ApplicationDbContext` thin.
4. Use EF migrations for actual schema changes.
5. Do not use `EnsureCreated` for application runtime schema.
6. Preserve explicit delete behaviors and architecture tests.

Identity managed-subject assignments use the existing `AspNetUserClaims` schema, therefore this feature by itself does not require an EF migration.

## Shared contracts

Cross-member contracts live under `Application/`. Prefer additive changes and coordinate breaking/signature changes across producer/consumer code.

Current important contracts:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Before Flow 2 implementation, update the RAG/session boundary as needed so subject context cannot be omitted.

## Infrastructure and hygiene

- Never commit `.env`, credentials, uploaded documents, DB dumps, logs, build output, or runtime data.
- `storage/uploads/` is runtime data except `.gitkeep`.
- Do not add Redis/RabbitMQ/another vector DB/RAG framework without a concrete requirement.
- Default branch: `master`.
- Use focused branches/PRs.
- Run build/tests/pending-model/Docker validation before merge.
- Never run `docker compose down -v` unless explicitly requested.

## Documentation rule

**Member 1 is the sole documentation editor** for README, all AGENTS files, and `docs/*`.

Members 2-5 communicate doc/status changes to Member 1. After major merges, Member 1 reconciles documentation with actual `master` before new work proceeds.
