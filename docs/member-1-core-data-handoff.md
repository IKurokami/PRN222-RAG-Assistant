# Member 1 handoff - Core/Data/RBAC/Multi-subject/Documentation

> Synchronized with `master` after PR #19.

## Ownership

Member 1 owns:

- Domain/Data/Security baseline;
- Identity roles and policies;
- shared Application contracts and schema/migration coordination;
- Admin user/role behavior;
- Subject catalogue + Admin Subject behavior;
- Subject Leader assignment;
- subject-specific authorization service;
- cross-workflow subject-context integration;
- role/subject regression tests;
- all repository documentation.

## Current completed baseline

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers
ManageSubjects
ManageDocuments
```

Admin can manage users/roles and all Subjects. Subject Leaders manage only assigned Subjects.

## Multi-subject implementation

Primary files:

```text
Security/AppClaimTypes.cs
Security/ISubjectAccessService.cs
Security/SubjectAccessService.cs
Controllers/SubjectsController.cs
Controllers/AdminSubjectsController.cs
Models/Subjects/
Models/Admin/AdminSubjectViewModels.cs
Views/Subjects/
Views/AdminSubjects/
```

Assignments use Identity claims rather than a new application table.

```text
prn222:managed-subject -> Subject Guid
```

No EF migration is required for this feature.

PRN222 remains seeded but no longer defines workflow scope.

## Cross-workflow integration performed by Member 1

Flow 1 Documents/Chapters and Flow 3 Reports were changed only as necessary to carry SubjectId and enforce subject authorization.

Original Flow 1/3 business behavior remains Member 2-owned; indexing remains Member 3-owned.

## PR #19 coordination note

PR #19 is merged and establishes the current cross-app visual baseline.

The UI/UX redesign is assigned to **Member 3** and is complete. It includes auth/landing/shared layout and visual refreshes across existing Admin/Subject/Chapter/Document/Report screens.

Member 1 still owns Identity/RBAC rules even where PR #19 introduced public Student registration. Public registration must remain Student-only unless requirements explicitly change.

Member 1 also remains the documentation integrator for Member 3's UI handoff.

## Next schema coordination point

Flow 2 is pending. Current `ChatSession` does not store SubjectId.

Before Member 4 implements RAG retrieval, Member 1 should coordinate the minimal subject-scoped persistence/application contract, likely including session subject ownership. If that changes the EF model, Member 1 coordinates the single migration and updates all docs.

Member 5 should reuse the PR #19 design system for future Flow 2 MVC screens.

## Documentation responsibility

Member 1 exclusively edits:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

After each meaningful merge, compare docs to actual `master` and update stale status/ownership/route/architecture statements before assigning the next work.

Current synchronized milestone: merged PR #19, 2026-08-15.
