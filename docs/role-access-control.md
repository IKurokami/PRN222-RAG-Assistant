# Role-based access control

> Synchronized after PR #30 merged on 2026-08-18.

## Roles

The application uses three ASP.NET Core Identity roles:

- `Admin`
- `SubjectLeader`
- `Student`

Role names are centralized in `Security/AppRoles.cs`.

## Why roles are not enough

The project is multi-subject. A Subject Leader must not gain access to every subject merely because they have the `SubjectLeader` role.

Authorization therefore has two layers:

```text
coarse role policy
      +
subject/resource permission
```

Policies:

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

For Flow 1/3, satisfying `ManageDocuments` does not by itself authorize a Subject Leader for a specific subject. Controllers/pages also use `ISubjectAccessService`.

Flow 2 backend additionally validates authenticated session ownership and subject consistency through the RAG service path.

## Capability matrix

| Capability | Admin | Subject Leader | Student |
|---|:---:|:---:|:---:|
| Public self-registration | No elevated role selection | No elevated role selection | Yes, Student only |
| View active subject catalogue | Yes | Yes | Yes |
| Create/edit/activate/deactivate subjects | Yes | No | No |
| Assign Subject Leaders | Yes | No | No |
| Manage users/roles | Yes | No | No |
| Manage chapters/documents | Any subject | Assigned subjects only | No |
| Re-index documents | Any subject | Assigned subjects only | No |
| View subject reports | Any subject | Assigned subjects only | No |
| View active document catalogue/details | Yes | Yes | Yes |
| Use own subject-scoped RAG session/backend | Yes | Yes | Yes |
| Manage another user's chat session | No special bypass | No | No |

## Public registration

Public registration creates a Student account only. Admin/SubjectLeader roles are never selectable by the registrant.

Member 3 owns the completed registration/auth presentation baseline. Member 1 retains Identity/RBAC policy ownership.

## Admin

Admin is the platform operator.

Admin can:

- create application users;
- assign one managed role (`Admin`, `SubjectLeader`, `Student`);
- create/edit/activate/deactivate Subjects;
- assign Subject Leader accounts to Subjects;
- manage any subject as an operational override;
- view reports for any subject.

Safeguards:

- current Admin cannot remove their own Admin role;
- last Admin cannot be demoted;
- user hard-delete is not exposed while workflow data references users;
- Subject hard-delete is not exposed while workflow data references Subjects;
- state-changing forms use anti-forgery validation.

## Subject Leader

A Subject Leader can be assigned zero, one or multiple subjects. For assigned subjects they can:

- create/edit/delete chapters;
- upload/edit/delete documents;
- request re-indexing;
- view reports/index status.

They cannot create subjects, assign leaders or manage user roles.

## Student

Student is a learning consumer. Students can self-register and view active subjects/document catalogue/details, but have no academic-content or identity administration permission.

Flow 2 backend must restrict chat/session/history/citations to the authenticated user's session and subject context.

## Subject Leader assignment persistence

Assignments use ASP.NET Core Identity claims:

```text
AppClaimTypes.ManagedSubject = "prn222:managed-subject"
claim value = Subject.Id as Guid string
```

`ISubjectAccessService` resolves managed Subject IDs from Identity claims on request-time authorization.

When an account is changed away from `SubjectLeader`, managed-subject claims are removed so stale assignments cannot later reactivate.

## Subject visibility

- Admin: all active/inactive subjects.
- Subject Leader: active subjects as learner plus assigned inactive subjects for management/cleanup.
- Student: active subjects only.

Inactive is not deletion. It prevents normal learner discovery while preserving referenced data and administrative access.

## Flow 2 subject/session security after PR #30

`ChatSession.SubjectId` is now persisted.

The merged Member 4 backend:

- queries sessions by both session ID and authenticated user ID;
- rejects a caller-supplied subject when it conflicts with the persisted session subject;
- supports subject-aware session creation/reuse;
- passes subject context into pgvector retrieval;
- keeps message/citation persistence attached to the validated session.

Product code should create/use sessions through the subject-aware RAG service path. Do not intentionally construct a null-subject product session to retrieve the global corpus.

## Server-side enforcement

Layout links and hidden buttons improve UX only; they are not authorization boundaries.

Every subject-specific write/report path must validate the concrete resource `SubjectId` server-side.

Document/chapter edit/delete/re-index actions authorize against the persisted entity's `SubjectId`, not a posted hidden value.

Flow 2 MVC controllers must remain thin adapters over `IRagQueryService` and must not query pgvector or provider APIs directly.

## Routes

Auth:

```text
/Account/Login
/Account/Register
/Account/Logout
/Account/AccessDenied
```

Admin identity:

```text
/admin/users
```

Admin subject management:

```text
/admin/subjects
/admin/subjects/create
/admin/subjects/{id}/edit
/admin/subjects/{id}/leaders
```

Authenticated subject selection:

```text
/subjects
```

## Persistence/migration impact

Managed-subject assignment continues to reuse Identity claims and does not require a dedicated assignment table.

PR #30 added the persisted `ChatSession.SubjectId` model/migration required for subject-scoped RAG sessions.

Member 1 remains the migration/schema coordinator for future cross-workflow model changes.

## Ownership and documentation identity

- Member 1 owns RBAC/multi-subject code, authorization rules, regression tests, schema coordination and docs.
- Member 3 owns the completed UI/UX/auth presentation baseline.
- Member 4 owns the merged Flow 2 backend authorization/session behavior.
- Member 5 owns the pending final Flow 2 MVC presentation/evaluation.

Project documentation uses Member numbers only. Do not add GitHub usernames.
