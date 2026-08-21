# Role-based access control

> Updated on 2026-08-21 for the PR #46/issue #47 management realtime implementation branch. PR #46 is not merged; merged contribution identity remains separate.

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
| Receive authorized management realtime updates | Authorized managed Subjects and admin feeds | Assigned Subject management feed | No management feed |
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

## Management SignalR authorization

SignalR does not weaken the role, policy, or subject boundary. The `/hubs/management` `ManagementHub` emits `ManagementChanged` only to authorized groups:

```text
subject:{guid:D}
admin:users
admin:subjects
subjects:catalog
```

Subscription methods are:

```text
SubscribeToSubject(Guid subjectId)
SubscribeToAdminUsers()
SubscribeToAdminSubjects()
SubscribeToSubjectCatalog()
```

The server must apply the same authorization used by the corresponding management page:

- `SubscribeToSubject` checks authenticated management permission and concrete access to the requested Subject;
- `SubscribeToAdminUsers` requires the `ManageUsers` policy;
- `SubscribeToAdminSubjects` requires the `ManageSubjects` policy;
- `SubscribeToSubjectCatalog` checks the server-side authorization for the active subject catalogue.

The client cannot gain access to another subject by supplying a different ID. Subject-scoped events are sent only to the affected group; user/role and subject/assignment events stay in their authorized admin/catalog groups.

Management notifications use `ManagementRealtimeEvent` with resources `Document`, `Chapter`, `Subject`, `SubjectLeaderAssignments`, and `User`, and changes `Created`, `Updated`, `Deleted`, `IndexStatusChanged`, `AssignmentsChanged`, and `RoleChanged`. Document index-status events retain their `Status` value.

SignalR is fan-out only:

```text
Razor Page handler
 -> policy + subject authorization, antiforgery and validation
 -> write commits
 -> notifier publishes ManagementChanged
 -> authorized connected clients
```

Do not broadcast one subject's management events globally, include sensitive data when a stable ID/status is sufficient, or expose a hub write operation. Broadcast only after the underlying write succeeds.

## Flow 2 session security

Chat/Evaluation require authentication.

RAG session behavior continues to validate:

- session ID belongs to the authenticated user;
- persisted `ChatSession.SubjectId` is consistent with the requested subject;
- retrieval uses the validated subject;
- messages/citations remain attached to the validated session.

Chat remains Razor Pages + SSE. Management SignalR must not become a cross-user Chat channel or replace the Chat SSE contract.

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
Management realtime       -> authorized SignalR `ManagementHub`
```

## Data Protection

ASP.NET Core Data Protection keys remain persisted in PostgreSQL, preserving authentication/antiforgery continuity across normal web-container restarts while the database persists.

## Migration acceptance

The PR #46 branch must retain authorization regression coverage for Razor Page handlers and ManagementHub subscriptions before merge. Its branch implementation is not a claim that PR #46 has merged; remove remaining legacy MVC presentation only after parity is verified.
