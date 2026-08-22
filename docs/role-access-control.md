# Role-based access control

> Verified against merged `master` on 2026-08-22. PR #46's Razor Pages and ManagementHub authorization model is now the runtime baseline.

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

Roles/policies are coarse gates. Subject-specific management additionally checks the concrete persisted Subject/resource boundary.

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
| Receive management realtime | Authorized admin/subject feeds | Assigned Subject feed | No management feed |
| View academic Reports | Any Subject | Assigned Subjects | No |
| View Admin billing analytics | Yes | No | No |
| Use own authenticated Chat/Evaluation | Yes | Yes | Yes |
| Manage another user's Chat session | No special bypass | No | No |

## Razor Pages authorization

All product/admin HTTP surfaces use Razor Pages:

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

Authorization is enforced server-side in PageModels/handlers and purpose-specific application services. Hidden navigation/buttons are UX only.

## Public registration and Subject Leader assignments

Public registration creates a Student account only. Elevated roles are Admin-managed.

Subject Leader assignments use Identity claims:

```text
prn222:managed-subject -> Subject Guid
```

When an account leaves the SubjectLeader role, managed-subject claims are removed so stale assignments cannot reactivate.

## Concrete subject authorization

`ManageDocuments` does not authorize a Subject Leader for every Subject. Documents/Chapters/Reports and other subject-scoped operations evaluate persisted subject ownership through `ISubjectAccessService` or an equivalent authorized boundary.

## ManagementHub authorization

`/hubs/management` emits `ManagementChanged` only to authorized groups:

```text
subject:{guid:D}
admin:users
admin:subjects
subjects:catalog
```

Subscription methods:

```text
SubscribeToSubject(Guid subjectId)
SubscribeToAdminUsers()
SubscribeToAdminSubjects()
SubscribeToSubjectCatalog()
```

The server applies the same policy and concrete-subject checks as the corresponding Razor Pages. Client-supplied IDs never grant access.

SignalR is fan-out only:

```text
Razor Page handler
 -> policy + subject authorization + antiforgery + validation
 -> write commits
 -> notifier publishes ManagementChanged
 -> authorized clients
```

Do not broadcast subject data globally, expose hub write operations, or include sensitive data when stable IDs/status values are sufficient.

## Flow 2 session security

Chat/Evaluation require authentication. RAG validates session ownership, persisted `ChatSession.SubjectId`, retrieval scope, and message/citation attachment to the validated session.

Chat remains Razor Pages + SSE. Management SignalR is not a Chat transport.

PR #54 also preserves the distinction between provider/rate-limit failures and no-document/no-evidence responses; authorization/error handling must not leak other users' or subjects' data.

## Billing/report security

VNPay checkout applies to the authenticated account. Payment credentials are server-only secrets.

Academic reports remain subject-scoped. Billing analytics is system-wide and Admin-only through its own report/query boundary. Subject Leaders must not receive system-wide payment/order aggregates merely because they can view academic reports for assigned Subjects.

## Data Protection

ASP.NET Core Data Protection keys persist in PostgreSQL, preserving authentication/antiforgery continuity across normal web-container restarts while the database persists.

## Regression invariants

Maintain tests that reject direct PageModel DbContext access, protect Razor Page handlers, enforce concrete subject access, isolate ManagementHub groups/subscriptions, restrict billing analytics to Admin, preserve Chat session ownership, and keep server-side authorization authoritative over UI visibility.
