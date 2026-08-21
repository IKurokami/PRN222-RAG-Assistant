# Role-based access control

> Updated on 2026-08-21 for the Razor Pages + SignalR target architecture.

## Roles

```text
Admin
SubjectLeader
Student
```

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
| Receive Document realtime updates | Authorized managed Subject | Assigned Subject | No management feed |
| View Flow 3 Reports | Any Subject | Assigned Subjects | No |
| Use own authenticated Flow 2 Chat | Yes | Yes | Yes |
| Use Evaluation | Yes | Yes | Yes |
| Manage another user's Chat session | No special bypass | No | No |

## Razor Pages target

All HTTP product/admin surfaces must converge on Razor Pages.

Target families:

```text
Pages/Account
Pages/Admin/Users
Pages/Admin/Subjects
Pages/Subjects
Pages/Chapters
Pages/Documents
Pages/Chat
Pages/Evaluation
Pages/Reports
```

Authorization must be applied server-side in PageModels/handlers or the application services they invoke. Hidden navigation/buttons remain UX only.

## Public registration

Public registration creates a Student account only. Elevated roles are Admin-managed.

## Subject Leader assignment persistence

Assignments continue to use Identity claims:

```text
prn222:managed-subject -> Subject Guid
```

When an account is changed away from SubjectLeader, managed-subject claims are removed so stale assignments cannot later reactivate.

## Flow 1/3 subject authorization

`ManageDocuments` alone does not authorize a Subject Leader for every Subject.

Documents/Chapters Razor Page handlers and Reports PageModels must evaluate the concrete subject/resource with `ISubjectAccessService` or an equivalent authorized application boundary.

## Document SignalR authorization

SignalR does not weaken the subject boundary.

Recommended group shape:

```text
subject:{SubjectId}
```

Before joining/subscribing a connection to a subject group, the server must verify the authenticated user is allowed to receive management updates for that subject.

Security requirements:

- do not trust a client-supplied `SubjectId` by itself;
- do not broadcast one subject's management events globally;
- do not include sensitive data in an event when a stable ID/status is sufficient;
- disconnect/reject unauthorized subscriptions;
- Page Handlers remain the write path with normal antiforgery protection;
- broadcast only after the underlying write succeeds.

## Flow 2 session security

Chat/Evaluation require authentication.

RAG session behavior continues to validate:

- session ID belongs to the authenticated user;
- persisted `ChatSession.SubjectId` is consistent with the requested subject;
- retrieval uses the validated subject;
- messages/citations remain attached to the validated session.

Chat remains Razor Pages + SSE. Document SignalR must not become a cross-user Chat channel.

## UI is not authorization

Every state-changing, subject-specific, or realtime-subscription path enforces authorization server-side.

## Target routes/surfaces

Public URL compatibility may be preserved through Razor Page route templates, but the target HTTP implementation is Razor Pages only.

```text
Account/authentication -> Razor Pages
Admin users/subjects    -> Razor Pages
Subjects catalogue      -> Razor Pages
Documents/Chapters      -> Razor Pages
Chat                    -> Razor Pages + SSE
Evaluation              -> Razor Pages
Reports                 -> Razor Pages
Document realtime       -> SignalR hub
```

## Data Protection

ASP.NET Core Data Protection keys remain persisted in PostgreSQL, preserving authentication/antiforgery continuity across normal web-container restarts while the database persists.

## Migration acceptance

The follow-up implementation PR must include authorization regression coverage for Razor Page handlers and SignalR subject subscriptions before the legacy MVC presentation is removed.
