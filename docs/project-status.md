# Project status

This snapshot reflects the latest `master` baseline after Flow 1 was migrated to MVC, plus the Admin/SubjectLeader RBAC extension on this branch.

When documentation disagrees with code, the latest merged `master` is the source of truth. Member 1 is the documentation owner and synchronizes these files after major merges.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - MVC Controllers + Views
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - MVC Controllers + Views
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages

Conversation History belongs to Flow 2 and is not counted as a separate workflow.

## Presentation allocation

```text
Flow 1 -> MVC                    [COMPLETE]
Flow 2 -> MVC                    [PENDING]
Flow 3 -> Razor Pages            [COMPLETE]
Auth/shell -> Razor Pages
Admin user management -> MVC     [COMPLETE on this branch]
```

## Role/access baseline

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers     -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Responsibility summary:

- **Admin:** account/role administration, academic-management override, reports.
- **Subject Leader:** PRN222 chapters/documents/re-index requests and reports; no user administration.
- **Student:** learning consumer; pending own Flow 2 sessions/history; no management permissions.

Canonical design: `docs/role-access-control.md`.

## Current project state

| Area | Owner | Status | Notes |
|---|---|---|---|
| Core domain/data/security | Member 1 | Complete baseline | Entities, EF Core configuration/migrations, Identity, pgvector wiring, shared contracts, architecture tests. |
| RBAC: Admin / Subject Leader / Student | Member 1 | Complete on this branch | Role catalogue, `ManageUsers`, Admin-or-SubjectLeader `ManageDocuments`, seeding/configuration. |
| Admin user/role management UI | Member 1 | Complete on this branch | MVC list/create/edit-role surface under `/admin/users`; last-Admin/self-demotion guards. |
| Role-aware shared navigation/UI | Member 1 | Complete on this branch | Admin/Subject Leader management navigation and role badges. |
| Repository documentation | Member 1 | Exclusive owner | README, AGENTS files, `docs/`, status/handoff synchronization after merges. |
| Chapter Management | Member 2 | Complete | MVC runtime PRN222 chapter CRUD; writes protected by `ManageDocuments`. |
| Document Management | Member 2 | Complete | MVC list/filter/upload/details/edit/delete/re-index, storage, validation, queue handoff. |
| Document parsing/chunking/indexing | Member 3 | Complete / merged through PR #9 | PDF/DOCX/PPTX parsing, chunking, embeddings, worker/service, state transitions. |
| Flow 3 Report & Statistics | Member 2 | Complete / merged through PR #12 | Read-only Razor Pages dashboard; now accessible to Admin or Subject Leader through policy. |
| RAG retrieval / grounded backend | Member 4 | Pending | Question embedding, pgvector retrieval, grounded generation, chat/citation persistence. |
| Flow 2 MVC presentation / history / citations | Member 5 | Pending | MVC chat/session UI, Conversation History, citations, evaluation integration. |
| Evaluation set | Member 5 | Pending | Human-authored 50-question ground-truth set. |

## Member 1 - RBAC + documentation ownership

Member 1 now owns the role feature end-to-end, including shared UI changes that expose role capabilities.

Primary files:

```text
src/PRN222.RagAssistant/Security/AppRoles.cs
src/PRN222.RagAssistant/Security/AppPolicies.cs
src/PRN222.RagAssistant/Infrastructure/Identity/IdentitySeeder.cs
src/PRN222.RagAssistant/Infrastructure/ServiceCollectionExtensions.cs
src/PRN222.RagAssistant/Controllers/AdminUsersController.cs
src/PRN222.RagAssistant/Models/Admin/AdminUserViewModels.cs
src/PRN222.RagAssistant/Views/AdminUsers/
src/PRN222.RagAssistant/Pages/Shared/_Layout.cshtml
```

Admin user management supports:

- list users and their current roles;
- create a user through `UserManager<ApplicationUser>`;
- assign `Admin`, `SubjectLeader`, or `Student`;
- prevent the signed-in Admin from removing their own Admin role;
- prevent demotion of the last Admin;
- anti-forgery validation on state-changing actions.

Hard-delete is intentionally not included because workflow rows reference users and account deletion would require a separate lifecycle/data-retention design.

No EF migration is required for this change because the existing ASP.NET Core Identity schema already stores roles and user-role membership.

## Flow 1 - complete MVC workflow

Flow 1 remains owned by Members 2 + 3 for business behavior/indexing. Member 1 owns global role-policy changes around it.

```text
Admin or Subject Leader
        |
        v
DocumentsController / ChaptersController
        |
        +--> validate / persist / manage
        |
        v
IDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
DocumentIndexingService
        |
        +--> parse
        +--> chunk
        +--> embed
        +--> persist DocumentChunk
        \--> Indexed / Failed
```

Students may view authenticated catalogue/details but cannot execute write actions.

## Flow 3 - complete Razor Pages workflow

Flow 3 remains under `Pages/Reports/` and is read-only.

`AppPolicies.ManageDocuments` now allows Admin or Subject Leader. The report implementation itself remains Member 2-owned and must not mutate workflow state, call Ollama, or run similarity retrieval.

## Flow 2 - remaining work

### Member 4 - RAG backend

Owns question embedding, pgvector retrieval, grounded/no-evidence behavior, chat completion, authenticated session ownership, and message/citation persistence.

Any new global role/policy requirement must be coordinated with Member 1 rather than introduced as feature-local role strings.

### Member 5 - MVC presentation/evaluation

Owns Chat MVC actions/views, Conversation History, citation rendering, and evaluation tooling.

Member 5 consumes the role model but does not implement user/role administration and does not edit repository documentation.

## Documentation process

**Only Member 1 edits repository documentation.** This includes:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

Members 2-5 put status changes, integration notes, and documentation requests in their PR description/handoff. Member 1 synchronizes docs against actual merged code.

This rule reduces conflicting status files and stale ownership claims across parallel branches.

## Validation requirements for the RBAC change

Automated coverage must verify:

- role catalogue contains Admin, SubjectLeader, Student;
- `ManageDocuments` allows Admin and Subject Leader, denies Student/anonymous;
- `ManageUsers` allows Admin only;
- Admin user-management controller is protected by `ManageUsers`;
- Admin user-management POST actions validate anti-forgery tokens;
- existing Flow 1 MVC authorization tests remain green;
- EF pending-model check remains clean;
- Docker Compose configuration remains valid with Admin seed variables.

## Required reading

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/role-access-control.md
docs/flow-1-mvc-migration.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```
