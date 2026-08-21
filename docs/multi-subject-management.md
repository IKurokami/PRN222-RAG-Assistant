# Multi-subject management

> Updated on 2026-08-21 for the PR #46/issue #47 management realtime implementation branch. PR #46 is not merged; subject ownership and contribution identity rules remain unchanged.

## Goal

PRN222 remains the seeded demo subject, but runtime workflows support multiple Subjects without hard-coding one course as global scope.

## Persisted subject boundaries

```text
Subject
Chapter(..., SubjectId)
Document(..., SubjectId)
ChatSession(..., SubjectId)
```

Subject context reaches all workflows:

- Flow 1 Document/Chapter management and indexing source ownership;
- Flow 2 Chat sessions and RAG retrieval;
- Flow 3 Report snapshots including Chat aggregates;
- Authorized management SignalR subscriptions and broadcasts use the same persisted subject boundary.

## Target presentation

All HTTP surfaces converge on Razor Pages. Subject context is preserved with Razor Page routes/query values/forms and revalidated server-side.

## Admin subject management

Admin can create/edit/activate/deactivate Subjects and assign Subject Leaders through Razor Pages in the target architecture. Hard delete remains intentionally omitted where referenced data would make lifecycle semantics unsafe.

## Subject Leader assignment

Assignments use ASP.NET Core Identity claims:

```text
Type  = prn222:managed-subject
Value = Subject Guid
```

## Authorization service

`ISubjectAccessService` remains the server-side Subject management authorization boundary.

- Admin: any existing Subject.
- Subject Leader: assigned Subjects for management.
- Student: no academic-content management permission.

Entity actions authorize against persisted resource `SubjectId`, not an untrusted posted value.

## Flow 1

Documents/Chapters migrate to Razor Pages while preserving concrete subject context.

The background indexing pipeline remains document-ID-driven; no per-subject worker is required.

### Management realtime isolation

Management realtime uses the authorized `/hubs/management` `ManagementHub` and a common `ManagementChanged` event. Subject-specific Document/Chapter changes use:

```text
subject:{guid:D}
```

Administrative changes use only their corresponding scoped groups:

```text
admin:users
admin:subjects
subjects:catalog
```

The hub exposes `SubscribeToSubject(Guid subjectId)`, `SubscribeToAdminUsers()`, `SubscribeToAdminSubjects()`, and `SubscribeToSubjectCatalog()`. Each method performs server-side policy and concrete-subject authorization; a client-supplied ID is never sufficient. User/role and Subject Leader assignment events do not leak into unrelated subject groups.

Management resources are Document, Chapter, Subject, SubjectLeaderAssignments, and User. Changes are Created, Updated, Deleted, IndexStatusChanged, AssignmentsChanged, and RoleChanged. Document index-status notifications retain their status payload.

SignalR events are not the source of truth. Razor Page handlers remain the write path, and broadcasts occur only after persistence commits. Clients automatically reconnect and reload authorized state when an event is insufficient.

## Flow 2

Chat is Razor Pages after PR #42 and remains subject-scoped:

```text
selected Subject
 -> ChatSession.SubjectId
 -> IRagQueryService
 -> question embedding
 -> pgvector retrieval filtered by Document.SubjectId
 -> grounded generation
 -> messages/citations attached to validated session
 -> SSE presentation
```

PR #43 moves Chat page/session data behind `IChatPageService`.

Evaluation's target presentation is Razor Pages and must preserve the same active-subject resolution rules.

## Flow 3

Reports require a concrete subject and management permission.

`ReportQueryService` scopes:

- Chapter/Document/index metrics;
- ChatSession totals by `ChatSession.SubjectId`;
- ChatMessages through those sessions;
- MessageCitations through those messages.

## Public registration

Public registration creates only a Student account. It does not create Subject Leader assignments or expose elevated-role selection.

## Migration rule

The PR #46 branch must preserve subject isolation while the implementation is reviewed and merged. Razor Page conversion and ManagementHub addition are not allowed to introduce global-corpus, cross-subject, or unauthorized management paths.

See `razor-pages-signalr-architecture.md` for the canonical migration target.
