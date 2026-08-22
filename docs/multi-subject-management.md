# Multi-subject management

> Verified against merged `master` on 2026-08-22. The Razor Pages + ManagementHub migration from PR #46 is implemented.

## Goal and persisted boundaries

PRN222 remains seeded demo data, while runtime workflows support multiple Subjects without hard-coding one course as global scope.

```text
Subject
Chapter(..., SubjectId)
Document(..., SubjectId)
ChatSession(..., SubjectId)
```

Subject context crosses Flow 1 management/indexing, Flow 2 Chat/RAG, Flow 3 academic reports, and authorized management realtime.

## Presentation and administration

All HTTP product/admin surfaces use Razor Pages. Subject context is carried in routes/query/forms and revalidated server-side.

Admins can create/edit/activate/deactivate Subjects and assign Subject Leaders. Hard delete remains intentionally avoided where referenced data makes lifecycle semantics unsafe.

Subject Leader assignments use Identity claims:

```text
Type  = prn222:managed-subject
Value = Subject Guid
```

`ISubjectAccessService` is the concrete subject authorization boundary:

- Admin: any existing Subject.
- Subject Leader: assigned Subjects for management.
- Student: no academic-content management permission.

Entity actions authorize persisted `SubjectId`, not an untrusted posted value.

## Flow 1 and realtime isolation

Documents/Chapters are Razor Pages and background indexing remains document-ID-driven.

Management realtime uses `/hubs/management` and `ManagementChanged`.

Subject-specific changes use:

```text
subject:{guid:D}
```

Administrative feeds use only:

```text
admin:users
admin:subjects
subjects:catalog
```

Every subscription performs server-side policy and concrete-subject authorization. Events are transient synchronization hints; PostgreSQL remains source of truth. Writes occur in Razor Page handlers/application services and notifications are emitted only after persistence succeeds.

## Flow 2

```text
selected Subject
 -> ChatSession.SubjectId
 -> IRagQueryService
 -> question embedding
 -> pgvector retrieval filtered by Document.SubjectId
 -> grounded generation
 -> messages/citations on validated session
 -> SSE presentation
```

Chat page/session data is behind `IChatPageService`. Evaluation is also Razor Pages and must preserve active-subject/user authorization rules.

## Flow 3

Academic reports require a concrete subject and authorized access. `ReportQueryService` scopes Chapter/Document/index metrics plus ChatSessions, ChatMessages and MessageCitations through `SubjectId`.

Billing analytics is deliberately not subject-scoped: current VNPay purchases grant account-level quota and normally have no meaningful Subject attribution. System-wide payment/quota metrics remain Admin-only and must not be assigned to whichever Subject is being viewed.

## Public registration

Public registration creates only a Student account. It does not create Subject Leader assignments or expose elevated-role selection.

## Invariants

Future changes must preserve persisted subject isolation across Razor Page handlers, RAG retrieval, reports and ManagementHub groups. Never introduce global-corpus retrieval, cross-subject management feeds, or subject-level revenue attribution without matching persisted product semantics.
