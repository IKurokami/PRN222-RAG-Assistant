# Role-based access control

> Synchronized after PR #40 on 2026-08-21.

## Roles

```text
Admin
SubjectLeader
Student
```

Role names are centralized in `Security/AppRoles.cs`.

## Policies

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Roles/policies are coarse gates. Subject-specific management additionally checks the concrete resource/subject boundary.

## Capability matrix

| Capability | Admin | Subject Leader | Student |
|---|:---:|:---:|:---:|
| Public self-registration | Student only | Student only | Yes |
| View active Subjects | Yes | Yes | Yes |
| Create/edit/activate/deactivate Subjects | Yes | No | No |
| Assign Subject Leaders | Yes | No | No |
| Manage users/roles | Yes | No | No |
| Manage Chapters/Documents | Any Subject | Assigned Subjects | No |
| Re-index Documents | Any Subject | Assigned Subjects | No |
| View Flow 3 Reports | Any Subject | Assigned Subjects | No |
| Use own authenticated Flow 2 Chat | Yes | Yes | Yes |
| Use Evaluation | Yes | Yes | Yes |
| Manage another user's Chat session | No special bypass | No | No |

## Public registration

Public registration creates a Student account only. Elevated roles are Admin-managed.

## Admin safeguards

- current Admin cannot remove their own Admin role;
- last Admin cannot be demoted;
- user hard-delete is not exposed while workflow data references users;
- Subject hard-delete is not exposed while workflow data references Subjects;
- state-changing forms use anti-forgery validation.

## Subject Leader assignment persistence

Assignments use Identity claims:

```text
prn222:managed-subject -> Subject Guid
```

When an account is changed away from SubjectLeader, managed-subject claims are removed so stale assignments cannot later reactivate.

## Flow 1/3 resource authorization

`ManageDocuments` alone does not authorize a Subject Leader for every Subject. Controllers/pages additionally use `ISubjectAccessService` against the concrete subject/resource.

PR #40 preserves this pattern for Reports: the PageModel authorizes the selected Subject before calling `IReportQueryService`.

## Flow 2 session security

Chat/Evaluation require authentication.

RAG session behavior validates:

- session ID belongs to the authenticated user;
- persisted `ChatSession.SubjectId` is consistent with the requested subject;
- retrieval uses the validated subject;
- messages/citations remain attached to the validated session.

Chat session deletion queries by both session ID and authenticated user ID. There is no Admin bypass in the current Chat controller for manipulating another user's session.

## UI is not authorization

Hidden links/buttons are UX only. Every state-changing or subject-specific management path must enforce authorization server-side.

## Main routes/surfaces

```text
/auth via Razor Pages Account pages
/admin/users
/admin/subjects
/subjects
/Documents + /Chapters MVC
/Chat MVC
/Evaluation MVC
/Reports Razor Pages
```

## Data Protection

PR #38 persists ASP.NET Core Data Protection keys in PostgreSQL. This protects authentication/antiforgery continuity across normal web-container restarts as long as the backing database persists.

## Ownership

- Member 1 owns RBAC/multi-subject/security coordination.
- Member 4 owns RAG backend session/subject validation maintenance.
- Member 5 owns the completed Flow 2 product presentation/evaluation.

Project documentation uses Member numbers only. See `member-contributions.md` for merged credit.
