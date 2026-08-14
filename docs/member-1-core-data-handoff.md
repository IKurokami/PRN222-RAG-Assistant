# Member 1 handoff - Core/Data/RBAC/Multi-subject/Documentation

## Ownership

Member 1 owns:

- Domain/Data/Security baseline;
- Identity roles and policies;
- shared Application contracts and schema/migration coordination;
- Admin user/role management;
- Subject catalogue + Admin Subject management;
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

PRN222 remains seeded but no longer defines the workflow scope.

## Cross-workflow integration performed by Member 1

Flow 1 Documents/Chapters and Flow 3 Reports were changed only as necessary to carry SubjectId and enforce subject authorization.

Original Flow 1/3 business behavior remains Member 2-owned; indexing remains Member 3-owned.

## Next schema coordination point

Flow 2 is pending. Current `ChatSession` does not store SubjectId.

Before Member 4 implements RAG retrieval, Member 1 should coordinate the minimal subject-scoped persistence/application contract, likely including session subject ownership. If that changes the EF model, Member 1 coordinates the single migration and updates all docs.

## Documentation responsibility

Member 1 exclusively edits:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

After each meaningful merge, compare docs to actual `master` and update stale status/ownership/route/architecture statements before assigning the next work.
