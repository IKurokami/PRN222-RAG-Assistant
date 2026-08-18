# Multi-subject management

> Synchronized after PR #30 merged on 2026-08-18.

## Goal

PRN222 remains the seeded demo subject, but the application supports runtime Subjects without hard-coding one course as the global workflow scope.

## Persisted subject boundaries

Current model:

```text
Subject(Id, Code, Name, IsActive)
Chapter(..., SubjectId)
Document(..., SubjectId)
ChatSession(..., SubjectId)
```

The completed multi-subject work provides runtime Subject management, Subject Leader assignment, subject-first navigation and subject-specific authorization for Flow 1/3. PR #30 extends the subject boundary into Flow 2 chat/RAG persistence.

## Subject catalogue

Authenticated users enter through:

```text
GET /subjects
```

This establishes a concrete subject context before opening Documents, Chapters or Reports.

Member 3 owns the current visual baseline; visibility/authorization behavior remains Member 1-owned.

## Admin subject management

```text
GET  /admin/subjects
GET  /admin/subjects/create
POST /admin/subjects/create
GET  /admin/subjects/{id}/edit
POST /admin/subjects/{id}/edit
GET  /admin/subjects/{id}/leaders
POST /admin/subjects/{id}/leaders
```

Admin can create/edit subjects, toggle `IsActive`, assign Subject Leaders and manage any subject as an operational override. Hard delete remains intentionally omitted.

## Subject Leader assignment

Assignments use ASP.NET Identity claims:

```text
Type  = prn222:managed-subject
Value = Subject Guid
```

No dedicated assignment table is required for the current design.

## Authorization service

`ISubjectAccessService` is the server-side subject authorization boundary for Flow 1/3 resource operations.

- Admin: any existing subject.
- Subject Leader: assigned subjects only for management.
- Student: no management permission.

Entity actions authorize against the persisted entity `SubjectId`, not a posted/query value that can be tampered with.

## Flow 1

Subject context is preserved across:

- document list/filter;
- upload;
- details/edit/delete/re-index;
- chapter list/create/edit/delete;
- chapter validation;
- redirects/links.

The indexing pipeline remains one document-ID-driven pipeline for all subjects.

## Flow 3

Reports require a concrete `subjectId` plus subject-specific manage permission.

Document/indexing metrics are subject-scoped. Because `ChatSession.SubjectId` now exists after PR #30, chat aggregates should be audited when Member 5 completes Flow 2 so they explicitly use the same subject boundary.

## Flow 2 after PR #30

The Member 4 backend now establishes subject-aware chat/RAG behavior:

```text
selected/default active Subject
 -> ChatSession.SubjectId
 -> IRagQueryService
 -> question embedding
 -> pgvector retrieval filtered by Document.SubjectId
 -> grounded generation
 -> messages/citations attached to the validated session
```

Important rules:

- session ownership is validated against the authenticated user;
- a caller-supplied subject conflicting with the persisted session subject is rejected;
- product callers should use the subject-aware session creation/reuse path;
- product code must not intentionally construct a null-subject session and fall back to global-corpus retrieval;
- Conversation History and citations remain tied to the validated chat session.

The remaining Member 5 MVC UI must preserve this subject context rather than reimplementing retrieval or provider calls in controllers/views.

## Public Student registration

Public registration creates only a `Student` account. It does not create Subject Leader assignments and does not expose elevated-role selection.

## Migration history

The original Subject/Chapter/Document multi-subject work reused existing model fields and Identity claims.

PR #30 adds the real persisted `ChatSession.SubjectId` change/migration needed for subject-scoped RAG sessions.

Member 1 remains the schema/migration coordinator for future cross-workflow model changes.

## Ownership and contribution

- Member 1: Subject management, Subject Leader assignment, authorization service, schema coordination, cross-workflow subject wiring and docs.
- Member 2: established Flow 1/Flow 3 behavior.
- Member 3: completed visual baseline and indexing maintenance ownership.
- Member 4: merged Flow 2 subject-scoped RAG backend.
- Member 5: pending final Flow 2 MVC/evaluation layer.

Actual merged contribution credit is tracked separately in `docs/member-contributions.md`.

Project documentation uses Member numbers only and must not add GitHub usernames.
