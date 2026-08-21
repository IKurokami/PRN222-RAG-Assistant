# Multi-subject management

> Updated on 2026-08-21 for the Razor Pages + SignalR target architecture.

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
- Document SignalR subscriptions and broadcasts.

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

### SignalR subject isolation

Document realtime connections use subject-scoped groups, for example:

```text
subject:{SubjectId}
```

A user may join a group only after server-side authorization for that subject. Create/update/delete/index-status events are sent only to the affected subject group.

SignalR events are not the source of truth. Clients may refresh state from the authorized Razor Page/read endpoint when needed.

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

The follow-up presentation PR must preserve subject isolation while removing the remaining legacy MVC surfaces. Razor Page conversion and SignalR addition are not allowed to introduce global-corpus or cross-subject management paths.

See `razor-pages-signalr-architecture.md` for the canonical migration target.
