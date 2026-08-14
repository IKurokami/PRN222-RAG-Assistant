# Team workflow and ownership

This is the coordination contract for the five-member team. Member 1 is the sole documentation owner.

## Current milestone

- Member 1 Core/Data/Identity/RBAC: complete.
- Member 1 Admin user/role management: complete.
- Member 1 multi-subject management/assignment/subject scoping: complete on current feature branch.
- Member 2 Flow 1 request/business behavior: complete.
- Member 3 Flow 1 indexing: complete / merged through PR #9.
- Member 2 Flow 3 reporting behavior: complete / merged through PR #12.
- Member 4 Flow 2 backend: pending.
- Member 5 Flow 2 MVC/history/citations/evaluation: pending.

## Member 1 - Core/Data/RBAC/multi-subject/docs

Owns:

- domain/data/security baseline;
- shared contracts and EF migration coordination;
- Identity configuration/seeding;
- Admin/SubjectLeader/Student roles;
- `ManageUsers`, `ManageSubjects`, `ManageDocuments` policies;
- Admin user/role UI;
- Subject catalogue and Admin Subject UI;
- Subject Leader assignment;
- `ISubjectAccessService`;
- cross-workflow subject-context integration for Flow 1/3;
- authorization regression tests;
- all README/AGENTS/docs edits.

Members 2-5 report documentation impacts to Member 1 instead of editing these files in parallel.

## Member 2 - Flow 1 request behavior + Flow 3 reporting

Member 2 retains ownership of existing workflow behavior:

- Chapter CRUD semantics;
- Document list/filter/upload/details/edit/delete/re-index semantics;
- upload validation/storage/queue handoff;
- safe chapter deletion;
- read-only Flow 3 reporting behavior.

Member 1 owns the subject-context/RBAC integration around these screens. Do not remove or bypass it.

## Member 3 - indexing

Owns parsers, chunker, embeddings, indexing service/worker, state transitions, chunk replacement, and startup recovery.

Indexing remains one document-ID-driven pipeline for all subjects.

## Member 4 - Flow 2 backend

Pending responsibilities:

- subject-scoped RAG query design;
- question embedding;
- pgvector retrieval over indexed documents of the selected subject only;
- grounding/no-evidence behavior;
- provider-neutral completion implementation;
- session ownership validation;
- messages/citations persistence.

Before implementation, coordinate with Member 1 because current `ChatSession` has no SubjectId and the shared RAG contract may need subject context.

Do not implement retrieval across all subjects.

## Member 5 - Flow 2 MVC/evaluation

Owns Chat MVC actions/views, subject-aware session navigation/history/citations, and the 50-question evaluation set/tooling.

Do not create Flow 2 Razor Pages.

## Integration map

```text
Admin
  +--> /admin/users                 [Member 1]
  +--> /admin/subjects              [Member 1]
  +--> Subject Leader assignments  [Member 1]
  \--> any subject as override

Subject Leader
  \--> assigned Subject(s)
       +--> Flow 1 MVC              [Member 2 behavior + Member 1 subject/RBAC]
       +--> indexing queue          [Member 3]
       \--> Flow 3 Razor Pages      [Member 2 behavior + Member 1 subject/RBAC]

Student / authenticated learner
  +--> active subject catalogue
  \--> Flow 2 [pending Member 4/5]
```

## Database coordination

Current multi-subject assignment uses existing Identity claims and requires no migration.

If Flow 2 adds `ChatSession.SubjectId` or another real persistence change:

1. Member 4/5 describe the needed invariant;
2. Member 1 coordinates entity/configuration/shared contract updates;
3. synchronize latest `master`;
4. generate one migration;
5. run tests + pending-model check + PostgreSQL migration validation;
6. Member 1 updates docs.

Avoid competing migrations.

## Documentation workflow

1. Members 2-5 implement owned code/tests.
2. Their PR/handoff describes status, architecture, routes/config changes.
3. They do not edit coordination docs independently.
4. Member 1 reconciles docs with actual code after integration/merge.
5. `master` code is the final source of truth if docs are temporarily behind.
