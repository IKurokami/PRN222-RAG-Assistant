# Role-based access control design

## Decision

The application uses three ASP.NET Core Identity roles:

- `Admin`
- `SubjectLeader`
- `Student`

`SubjectLeader` already existed in the baseline. This change keeps that stable role name, formalizes its responsibilities, and adds the missing `Admin` role plus an Admin-only user-management surface.

All identity/RBAC implementation, role-aware UI changes, tests, and repository documentation are owned by **Member 1**.

## Responsibility model

| Capability | Admin | Subject Leader | Student |
|---|:---:|:---:|:---:|
| Sign in and use authenticated learning features | Yes | Yes | Yes |
| View indexed document catalogue/details | Yes | Yes | Yes |
| Create/edit/delete chapters | Yes | Yes | No |
| Upload/edit/delete/re-index documents | Yes | Yes | No |
| View Report & Statistics | Yes | Yes | No |
| View user administration | Yes | No | No |
| Create application accounts | Yes | No | No |
| Assign `Admin` / `SubjectLeader` / `Student` roles | Yes | No | No |
| Self-assign an elevated role | No | No | No |

### Admin

Admin is the platform-level operator. It owns identity administration and has an override path for academic-management screens so the system is recoverable even when no Subject Leader is available.

Admin responsibilities:

- create application accounts;
- assign one managed application role to an account;
- access `/admin/users`;
- use all actions protected by `ManageDocuments` when operational intervention is required;
- access the reporting dashboard;
- never expose role assignment through public/self-service UI.

Safety rules:

- an Admin cannot remove their own Admin role;
- the last Admin account cannot be demoted;
- user administration does not hard-delete accounts because workflow rows reference `ApplicationUser`;
- role changes are server-side authorized and POST actions require anti-forgery validation.

### Subject Leader

Subject Leader is the academic-content owner for PRN222.

Responsibilities:

- manage runtime chapters;
- upload, edit, delete, and re-index source documents;
- monitor indexing results through Report & Statistics;
- curate the authoritative source material consumed by RAG.

Subject Leader explicitly does **not** manage accounts or roles.

### Student

Student is the learning consumer.

Current responsibilities:

- authenticate;
- view available document catalogue/details.

Pending Flow 2 responsibilities:

- create/open only their own chat sessions;
- ask grounded PRN222 questions;
- view their own Conversation History and citations.

Student never receives document-management or user-management permissions.

## Authorization policies

Role names remain centralized in `Security/AppRoles.cs` and policy names in `Security/AppPolicies.cs`.

```text
ManageUsers     -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

`ManageDocuments` continues to protect Flow 1 writes and Flow 3 reports. This avoids duplicating role checks in individual controllers/pages and preserves one server-side authorization boundary.

UI visibility is only presentation behavior. Controllers/Razor Pages remain protected by policies.

## Admin MVC surface

Member 1 owns:

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

The Admin UI supports account creation and role assignment. It intentionally does not add hard-delete or speculative profile/organization management.

## Role-aware shared UI

The shared layout:

- shows Chapters and Reports to Admin or Subject Leader;
- shows Users only to Admin;
- displays Admin / Subject Leader / Student badges;
- uses centralized `AppRoles` constants instead of duplicated role-name literals where the shared UI is changed.

## Demo seeding

Role records are always ensured by `IdentitySeeder`.

Demo-user seeding remains disabled by default. When enabled, configuration can seed:

```text
Auth:SeedUsers:Admin
Auth:SeedUsers:SubjectLeader
Auth:SeedUsers:Student
```

Docker Compose maps matching `AUTH_ADMIN_*`, `AUTH_SUBJECT_LEADER_*`, and `AUTH_STUDENT_*` variables from `.env`.

Example credentials in `.env.example` are local-only defaults and must be changed outside disposable development environments.

## Persistence impact

No new application entity or column is required.

ASP.NET Core Identity already persists roles and user-role membership, so this RBAC change requires **no EF Core migration**.

## Member ownership

### Member 1 - exclusive RBAC and documentation owner

Member 1 owns all of the following end-to-end:

- `Security/AppRoles.cs` and `Security/AppPolicies.cs`;
- Identity role/user seeding;
- Admin user/role management controller, models, and views;
- role-aware navigation and shared UI;
- role/policy regression tests;
- future authorization changes that affect multiple workflows;
- **all edits to `README.md`, `AGENTS.md`, `src/.../Application/AGENTS.md`, and `docs/`.**

Members 2-5 should report implementation/status changes to Member 1. They should not independently edit coordination/handoff documentation; Member 1 synchronizes docs after their code is ready or merged.

### Other members

- Member 2 continues to own Flow 1 business behavior and Flow 3 reporting behavior, but role/policy/shared-navigation changes around those screens belong to Member 1.
- Member 3 remains indexing owner and does not change RBAC.
- Member 4 must enforce authenticated chat-session ownership behind Flow 2 services but should coordinate any new global policy with Member 1.
- Member 5 consumes the established role model in Flow 2 MVC UI and must not add parallel role management or edit repository docs.

## Regression requirements

Tests must verify:

- `Admin`, `SubjectLeader`, and `Student` remain in the role catalogue;
- `ManageDocuments` allows Admin and Subject Leader, but not Student;
- `ManageUsers` allows only Admin;
- Admin user-management controller is policy protected;
- Admin user-management POST actions validate anti-forgery tokens;
- existing Flow 1 MVC authorization tests remain green.
