# Project status

This document describes the intended post-merge state of the current multi-subject feature branch, based on `master` after merged PR #17 (`feat: add Admin and Subject Leader RBAC`). Member 1 owns synchronization of this status file.

## Workflows

| Workflow | Presentation | Status | Owner |
|---|---|---|---|
| Flow 1 - Document Management & Indexing | MVC | Complete | Member 2 request side + Member 3 indexing; Member 1 subject/RBAC integration |
| Flow 2 - RAG Q&A + Conversation Management | MVC | Pending | Member 4 backend + Member 5 UI/evaluation |
| Flow 3 - Report & Statistics | Razor Pages | Complete | Member 2; Member 1 subject/RBAC integration |

Conversation History is part of Flow 2.

## Platform/RBAC state

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity | Complete | Member 1 |
| Admin/SubjectLeader/Student roles | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Subject catalogue | Complete on current branch | Member 1 |
| Admin Subject CRUD (create/edit/activate/deactivate) | Complete on current branch | Member 1 |
| Subject Leader assignment | Complete on current branch | Member 1 |
| Subject-specific authorization service | Complete on current branch | Member 1 |
| Flow 1 subject scoping | Complete on current branch | Member 1 cross-cutting integration |
| Flow 3 subject scoping | Complete on current branch | Member 1 cross-cutting integration |
| Documentation synchronization | Complete on current branch | Member 1 |

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
- Active subjects are visible to authenticated learners.
- An assigned Subject Leader may still access an inactive subject for operational cleanup.
- Subject-specific actions must use `ISubjectAccessService`; role policy alone is insufficient.

## UI/routes

```text
GET /subjects
GET /admin/users
GET /admin/subjects
GET/POST /admin/subjects/create
GET/POST /admin/subjects/{id}/edit
GET/POST /admin/subjects/{id}/leaders
```

Global Documents/Chapters/Reports navigation has been replaced by Subject-first navigation because those screens require subject context.

## Flow 1 state

Flow 1 controllers/views now use a `subjectId` context rather than `SeedData.Prn222SubjectId`.

- document list/filter/upload is subject-scoped;
- chapter CRUD/validation is subject-scoped;
- edit/delete/re-index authorization is checked against the document/chapter's persisted SubjectId;
- chapter options cannot come from another subject;
- redirects preserve subject context;
- indexing handoff remains by Document.Id and does not need per-subject indexing workers.

## Flow 3 state

Report document/chapter/index/chunk/failure/recent-index metrics are subject-scoped.

Chat metrics remain global because Flow 2 is pending and current `ChatSession` has no SubjectId. This is a known, explicit transitional state.

## Flow 2 remaining requirement

Member 4/5 must not implement global-corpus chat.

Before backend implementation begins, coordinate with Member 1 on:

- subject ownership for `ChatSession`;
- a subject-aware RAG query boundary;
- pgvector retrieval constrained to selected subject documents;
- same-subject citations;
- subject context in MVC session/history navigation;
- required EF migration, if `ChatSession.SubjectId` or related persistence changes are introduced.

## Documentation ownership

Member 1 exclusively edits:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

Members 2-5 report changes to Member 1 instead of modifying coordination docs in parallel.
