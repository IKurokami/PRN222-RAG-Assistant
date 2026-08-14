# Role-based access control

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

For Flow 1/3, satisfying `ManageDocuments` does **not** by itself authorize a Subject Leader for a specific subject. Controllers/pages also use `ISubjectAccessService`.

## Capability matrix

| Capability | Admin | Subject Leader | Student |
|---|:---:|:---:|:---:|
| View active subject catalogue | Yes | Yes | Yes |
| Create/edit/activate/deactivate subjects | Yes | No | No |
| Assign Subject Leaders | Yes | No | No |
| Manage users/roles | Yes | No | No |
| Manage chapters/documents | Any subject | Assigned subjects only | No |
| Re-index documents | Any subject | Assigned subjects only | No |
| View subject reports | Any subject | Assigned subjects only | No |
| View active document catalogue/details | Yes | Yes | Yes |
| Pending Flow 2 chat | Yes | Yes | Yes |

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

Subject Leader is an academic-content manager, not a global administrator.

A Subject Leader can be assigned zero, one, or multiple subjects. For assigned subjects they can:

- create/edit/delete chapters;
- upload/edit/delete documents;
- request re-indexing;
- view reports/index status.

They cannot create subjects, assign leaders, or manage user roles.

## Student

Student is a learning consumer. Students can view active subjects/document catalogue/details but have no academic-content or identity administration permission.

Flow 2 must restrict each student's chat/session/history/citations to the selected subject and their own authorized sessions.

## Subject Leader assignment persistence

Assignments use existing ASP.NET Core Identity claims:

```text
AppClaimTypes.ManagedSubject = "prn222:managed-subject"
claim value = Subject.Id as Guid string
```

`ISubjectAccessService` resolves managed Subject IDs from `ApplicationDbContext.UserClaims` on request-time authorization.

Benefits:

- no new assignment table/migration for the current requirement;
- one leader can manage many subjects;
- one subject can have many leaders;
- role and resource permission remain separate;
- Admin remains a controlled override.

When an account is changed away from `SubjectLeader`, its managed-subject claims are removed so stale assignments cannot later reactivate.

## Subject visibility

- Admin: all active/inactive subjects.
- Subject Leader: all active subjects as a learner plus assigned inactive subjects for management/cleanup.
- Student: active subjects only.

Inactive is not deletion. It prevents normal learner discovery while preserving referenced data and administrative access.

## Server-side enforcement

The shared layout and view buttons only improve UX. They are not an authorization boundary.

Every subject-specific write/report path must derive or accept a SubjectId and validate it with `ISubjectAccessService`.

Document/chapter edit/delete/re-index actions should authorize against the persisted entity's SubjectId, not trust a posted hidden SubjectId.

## Routes

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

This feature does not add an application table/column. Identity already provides `AspNetUserClaims`, so no EF migration is required.

Future Flow 2 subject-scoped chat persistence may require a real model migration because `ChatSession` currently has no SubjectId. Member 1 coordinates that schema change.

## Ownership

Member 1 owns all RBAC/multi-subject code, subject-aware shared UI, regression tests, schema coordination, and all repository documentation.
