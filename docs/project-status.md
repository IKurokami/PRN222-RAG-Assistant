# Project status

> Synchronized with `master` after merged PR #19 (`0375f9bd`) on 2026-08-15. Member 1 owns synchronization of this file.

## Workflows

| Workflow | Presentation | Status | Owner |
|---|---|---|---|
| Flow 1 - Document Management & Indexing | MVC | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC integration |
| Flow 2 - RAG Q&A + Conversation Management | MVC | Pending | Member 4 backend + Member 5 UI/evaluation |
| Flow 3 - Report & Statistics | Razor Pages | Complete | Member 2 behavior; Member 1 subject/RBAC integration |

Conversation History is part of Flow 2.

## Platform/RBAC state

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity | Complete | Member 1 |
| Admin/SubjectLeader/Student roles | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Subject catalogue | Complete / merged | Member 1 |
| Admin Subject CRUD (create/edit/activate/deactivate) | Complete / merged | Member 1 |
| Subject Leader assignment | Complete / merged | Member 1 |
| Subject-specific authorization service | Complete / merged | Member 1 |
| Flow 1 subject scoping | Complete / merged | Member 1 cross-cutting integration |
| Flow 3 subject scoping | Complete / merged | Member 1 cross-cutting integration |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 RBAC rules |
| Cross-app UI/UX redesign | **Complete / merged in PR #19** | **Member 3** |
| Documentation synchronization | Current after PR #19 | Member 1 |

## PR #19 UI/UX milestone

PR #19 is merged and considered the current visual baseline.

Completed by **Member 3**:

- landing page redesign and application shell refresh;
- shared design tokens/components stylesheet layer;
- Bootstrap Icons LibMan integration;
- redesigned Login/Register/Logout/AccessDenied/Error/Privacy screens;
- public registration flow that creates Student accounts only;
- visual refresh of Subjects, Admin Users, Admin Subjects, Chapters, Documents, and Reports;
- document search and index-status filtering with filter context preserved across delete/re-index redirects;
- supporting showcase/testimonial/support assets and interactive landing sections.

This task is **not pending and must not be reassigned** unless a future UI change is explicitly created as new work.

Business ownership is unchanged: Member 2 owns Flow 1/Flow 3 behavior, Member 1 owns RBAC/multi-subject rules, and Member 5 owns future Flow 2 MVC/history/citations/evaluation.

## Multi-subject state

PRN222 remains seeded but is no longer the application-wide hard-coded scope.

Runtime model:

```text
Subjects
  +--> Chapters (SubjectId)
  +--> Documents (SubjectId)
  +--> Subject Leader assignments (Identity user claims)
  \--> future ChatSessions/RAG subject boundary [Flow 2 pending]
```

Subject Leader assignment:

```text
Claim type:  prn222:managed-subject
Claim value: Subject Guid
```

No EF migration is required for this assignment because `AspNetUserClaims` already exists.

## Authorization state

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Resource authorization:

- Admin manages any existing subject.
- Subject Leader manages only subjects assigned through managed-subject claims.
- Student manages none.
- Public self-registration creates only Student accounts.
- Active subjects are visible to authenticated learners.
- An assigned Subject Leader may still access an inactive subject for operational cleanup.
- Subject-specific actions must use `ISubjectAccessService`; role policy alone is insufficient.

## Current routes/presentation

```text
GET /subjects
GET /admin/users
GET /admin/subjects
GET/POST /admin/subjects/create
GET/POST /admin/subjects/{id}/edit
GET/POST /admin/subjects/{id}/leaders
GET/POST /Account/Login
GET/POST /Account/Register
```

Global Documents/Chapters/Reports navigation is subject-first because those screens require subject context.

The current visual system is implemented through `wwwroot/css/design-tokens.css`, `wwwroot/css/components.css`, and `wwwroot/css/site.css`.

## Flow 1 state

Flow 1 controllers/views use a `subjectId` context rather than `SeedData.Prn222SubjectId`.

- document list/filter/upload is subject-scoped;
- chapter CRUD/validation is subject-scoped;
- edit/delete/re-index authorization is checked against persisted SubjectId;
- chapter options cannot come from another subject;
- redirects preserve subject context;
- PR #19 added document title/file search and status filtering while preserving current filters across delete/re-index actions;
- indexing handoff remains by Document.Id and does not need per-subject indexing workers.

## Flow 3 state

Report document/chapter/index/chunk/failure/recent-index metrics are subject-scoped. PR #19 refreshed the presentation but did not change the read-only reporting boundary.

Chat metrics remain global because Flow 2 is pending and current `ChatSession` has no SubjectId. This is a known transitional state.

## Flow 2 remaining requirement

Member 4/5 must not implement global-corpus chat.

Before backend implementation is complete, coordinate with Member 1 on:

- subject ownership for `ChatSession`;
- a subject-aware RAG query boundary;
- pgvector retrieval constrained to selected subject documents;
- same-subject citations;
- subject context in MVC session/history navigation;
- required EF migration, if `ChatSession.SubjectId` or related persistence changes are introduced.

Member 5 should reuse the PR #19 visual system for future Flow 2 MVC screens rather than introduce a second design system.

## Next project priority

The major unfinished product workflow is **Flow 2**.

Recommended ownership remains:

1. Member 1: coordinate minimal subject-aware chat persistence/contract changes if needed.
2. Member 4: implement subject-scoped RAG backend and persistence behavior.
3. Member 5: implement MVC chat/history/citations and evaluation tooling using the established UI system.
4. Member 3: UI/UX redesign task is complete; only take additional UI work if explicitly assigned as a new task.

## Documentation ownership

Member 1 exclusively edits:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

Members 2-5 report changes to Member 1 instead of modifying coordination docs in parallel.
