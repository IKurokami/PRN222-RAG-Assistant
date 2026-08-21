# Multi-subject management

> Synchronized after PR #40 on 2026-08-21.

## Goal

PRN222 remains the seeded demo subject, but the application supports runtime Subjects without hard-coding one course as the global workflow scope.

## Persisted subject boundaries

```text
Subject
Chapter(..., SubjectId)
Document(..., SubjectId)
ChatSession(..., SubjectId)
```

Subject context now reaches all three workflows:

- Flow 1 Document/Chapter management and indexing source ownership;
- Flow 2 Chat sessions and RAG retrieval;
- Flow 3 Report snapshots including Chat aggregates.

## Subject catalogue

Authenticated users enter through the Subject catalogue and product links preserve a concrete subject context where required.

## Admin subject management

Admin can create/edit/activate/deactivate Subjects and assign Subject Leaders. Hard delete remains intentionally omitted where referenced data would make lifecycle semantics unsafe.

## Subject Leader assignment

Assignments use ASP.NET Core Identity claims:

```text
Type  = prn222:managed-subject
Value = Subject Guid
```

No dedicated assignment table is required by the current design.

## Authorization service

`ISubjectAccessService` is the server-side Subject management authorization boundary for Flow 1/3 resource operations.

- Admin: any existing Subject.
- Subject Leader: assigned Subjects for management.
- Student: no academic-content management permission.

Entity actions authorize against persisted resource `SubjectId`, not an untrusted posted value.

## Flow 1

Subject context is preserved across Document and Chapter operations. The background indexing pipeline remains document-ID-driven; no per-subject worker is required.

## Flow 2

Product Chat is now complete MVC.

```text
selected Subject
 -> ChatSession.SubjectId
 -> IRagQueryService
 -> question embedding
 -> pgvector retrieval filtered by Document.SubjectId
 -> grounded generation
 -> messages/citations attached to the validated session
```

New product sessions are created with a concrete subject. The Chat controller can still include legacy null-subject sessions for compatibility, but product behavior should not intentionally revert to global-corpus retrieval.

Session ownership and subject consistency remain server-side invariants.

## Flow 3 after PR #40

Reports require a concrete subject and management permission.

`ReportQueryService` now scopes:

- Chapter/Document/index metrics;
- ChatSession totals by `ChatSession.SubjectId`;
- ChatMessages through those sessions;
- MessageCitations through those messages.

The old documentation warning that Flow 3 Chat totals were global is obsolete.

## Public registration

Public registration creates only a Student account. It does not create Subject Leader assignments or expose elevated-role selection.

## Migration history

- original Subject/Chapter/Document multi-subject work reused the existing domain model and Identity claims;
- PR #30 persisted `ChatSession.SubjectId` for subject-scoped RAG;
- PR #40 consumed that persisted scope in report queries without requiring a new subject model.

## Ownership

- Member 1: subject management/authorization/schema coordination/docs.
- Member 2: Flow 1/3 behavior.
- Member 3: indexing maintenance + cross-app UI baseline.
- Member 4: RAG backend maintenance.
- Member 5: completed Flow 2 MVC/evaluation product layer.

See `member-contributions.md` for actual merged contribution credit.
