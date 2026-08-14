# Member 1 - Core/Data/RBAC/documentation handoff

## Current status

Member 1's Core/Data baseline is complete and now includes full ownership of the application role model, Admin user/role management, role-aware shared UI, authorization regression tests, and all repository documentation.

Workflow presentation allocation remains:

1. **Flow 1 - Document Management & Indexing** - complete - MVC Controllers + Views
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - MVC Controllers + Views
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

Admin user management is a separate cross-workflow identity surface implemented with MVC.

## Member 1 owned scope

Member 1 owns:

- core domain entities/enums;
- EF Core configurations;
- migration baseline and future schema coordination;
- PostgreSQL/pgvector integration;
- ASP.NET Core Identity integration;
- `Admin`, `SubjectLeader`, `Student` roles;
- `ManageUsers` and `ManageDocuments` authorization policies;
- Identity role/demo-user seeding;
- Admin account/role-management MVC UI;
- role-aware shared navigation/badges;
- cross-workflow role/policy regression tests;
- shared `Application/` abstractions/models;
- architecture/convention tests;
- **all edits to README, AGENTS files, and `docs/`.**

## Role responsibility model

### Admin

- manage users and role assignment;
- access `/admin/users`;
- create application accounts;
- assign `Admin`, `SubjectLeader`, or `Student`;
- use academic-management actions protected by `ManageDocuments` when operational override is needed;
- view reports.

Safety constraints:

- cannot remove own Admin role;
- cannot demote the last Admin;
- cannot expose public/self-service elevated role selection;
- no hard-delete user UI while workflow data references users.

### Subject Leader

- manage PRN222 chapters;
- upload/edit/delete/re-index documents;
- curate authoritative learning material;
- view Report & Statistics;
- cannot manage users/roles.

### Student

- authenticated learning consumer;
- can view available document catalogue/details;
- pending Flow 2: own chat sessions/history/citations;
- cannot manage academic content or users.

Canonical design: `docs/role-access-control.md`.

## Policy contract

```text
AppPolicies.ManageUsers     -> Admin
AppPolicies.ManageDocuments -> Admin OR SubjectLeader
```

Members 2-5 consume these policies; they do not redefine role names or add parallel role-management code. Any new global authorization requirement comes back through Member 1.

## Admin MVC implementation

Owned files:

```text
Controllers/AdminUsersController.cs
Models/Admin/AdminUserViewModels.cs
Views/AdminUsers/Index.cshtml
Views/AdminUsers/Create.cshtml
Views/AdminUsers/Edit.cshtml
```

Routes:

```text
GET  /admin/users
GET  /admin/users/create
POST /admin/users/create
GET  /admin/users/{id}/role
POST /admin/users/{id}/role
```

The controller uses `UserManager<ApplicationUser>` and existing Identity persistence. No new domain entity or EF migration is required.

## Shared UI ownership

Member 1 owns role-aware changes in `Pages/Shared/_Layout.cshtml` even when they surface links to Member 2-owned Flow 1/Flow 3 features.

Expected navigation:

- authenticated users: Home, Documents, Privacy;
- Admin or Subject Leader: Chapters, Reports;
- Admin only: Users;
- role badge: Admin / Subject Leader / Student.

UI visibility is not authorization; policies remain required server-side.

## Core/Data invariants

- entities use scalar foreign keys and no navigation properties;
- EF mapping belongs in dedicated `IEntityTypeConfiguration<TEntity>` classes;
- `ApplicationDbContext` stays thin;
- application schema changes use EF Core migrations;
- `(SubjectId, Number)` remains unique for chapters;
- `Document.ChapterId` remains nullable;
- chapter removal does not cascade-delete documents;
- timestamps are UTC;
- persisted enum conventions remain stable.

Adding roles/user-role membership does not change these invariants and does not require a migration.

## Shared contracts

### Flow 1 -> indexing

`IDocumentIndexingQueue` remains the request-to-background handoff. Member 2 persists a document and then enqueues its ID. Member 3 owns indexing.

### Retrieval/generation

`ITextEmbeddingService`, `IChatCompletionService`, `IRagQueryService`, `RagAnswer`, and `RagCitation` remain shared boundaries for Member 4/5.

Global authorization remains outside these provider-neutral interfaces.

## Handoff to Member 2

Member 2 continues owning Flow 1 request/business behavior and Flow 3 report behavior.

Member 2 should:

- keep `ManageDocuments` attributes on Flow 1 writes/Flow 3 access;
- not hard-code `SubjectLeader` checks when a policy already represents the authorization rule;
- not add Admin/role-management views;
- not edit repository docs;
- report role/UI/doc implications to Member 1.

## Handoff to Member 3

Member 3 continues indexing only. RBAC and documentation remain outside Member 3 scope.

## Handoff to Member 4

Member 4 owns server-side authenticated chat-session ownership within Flow 2 service behavior. If a global policy is needed, coordinate with Member 1 instead of adding feature-local role constants.

Member 4 does not edit repository docs.

## Handoff to Member 5

Member 5 owns Flow 2 MVC presentation/evaluation and consumes the existing authenticated role model.

Member 5 must not:

- add public role selection;
- build a duplicate user/role administration page;
- replace server-side session ownership with UI-only checks;
- edit repository docs.

## Documentation workflow

Member 1 is now the exclusive documentation editor.

Members 2-5 should place the following in their PR description/handoff when relevant:

- completed/pending status;
- new routes/screens;
- new configuration;
- changed contracts;
- migration/runtime requirements;
- integration notes;
- anything README/AGENTS/docs should reflect.

Member 1 then updates:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
relevant handoff/design docs
```

This avoids concurrent contradictory documentation edits.

## Regression requirements

Member 1 RBAC tests must keep proving:

- all three roles exist in the catalogue;
- Admin + Subject Leader satisfy `ManageDocuments`;
- Student/anonymous do not satisfy `ManageDocuments`;
- only Admin satisfies `ManageUsers`;
- Admin user controller requires `ManageUsers`;
- Admin POST actions use anti-forgery;
- existing Flow 1 authorization conventions remain green;
- EF pending-model check stays clean.
