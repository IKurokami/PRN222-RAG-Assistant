# Team workflow and ownership

> Current coordination baseline: `master` after merged PR #19 on 2026-08-15. Member 1 is the sole documentation owner.

## Current milestone

- Member 1 Core/Data/Identity/RBAC: complete.
- Member 1 Admin user/role management: complete.
- Member 1 multi-subject management/assignment/subject scoping: complete / merged.
- Member 2 Flow 1 request/business behavior: complete.
- Member 3 Flow 1 indexing: complete / merged through PR #9.
- Member 2 Flow 3 reporting behavior: complete / merged through PR #12.
- **Member 3 cross-app UI/UX redesign: complete / merged through PR #19.**
- Member 4 Flow 2 backend: pending.
- Member 5 Flow 2 MVC/history/citations/evaluation: pending.

The UI/UX redesign is no longer unassigned work.

## Member 1 - Core/Data/RBAC/multi-subject/docs

Owns:

- domain/data/security baseline;
- shared contracts and EF migration coordination;
- Identity configuration/seeding and RBAC rules;
- Admin/SubjectLeader/Student roles;
- `ManageUsers`, `ManageSubjects`, `ManageDocuments` policies;
- Admin user/role behavior;
- Subject catalogue and Admin Subject behavior;
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

Member 1 owns subject-context/RBAC integration around these screens. Member 3 owns the current visual redesign of these screens. Neither changes Member 2's business-logic ownership.

## Member 3 - indexing + UI/UX redesign

### Indexing - complete

Owns parsers, chunker, embeddings, indexing service/worker, state transitions, chunk replacement, and startup recovery.

Indexing remains one document-ID-driven pipeline for all subjects.

### Cross-app UI/UX redesign - complete in PR #19

Member 3 is assigned and credited for the UI/UX modernization merged in PR #19.

Completed scope:

- application-wide design tokens and reusable visual components;
- landing page redesign, showcase/testimonial/FAQ/CTA interactions and local media assets;
- Login/Register/Logout/AccessDenied/Error/Privacy redesign;
- Student self-registration presentation and implementation introduced by PR #19;
- shared layout/navigation refresh;
- visual refresh of Subject/Admin/Chapter/Document/Report screens;
- document search/status filter presentation and filter-preserving action UX;
- Bootstrap Icons LibMan setup.

Rules:

- this work is **complete**, not pending;
- future screens should reuse the same design system;
- UI ownership does not bypass Member 1 authorization rules or Member 2 workflow semantics;
- future Flow 2 MVC/history/citation/evaluation implementation remains Member 5-owned.

See `docs/member-3-ui-ux-handoff.md`.

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

Reuse the UI system delivered by Member 3 in PR #19. Do not create a competing CSS/design-token layer for Flow 2.

## Integration map

```text
Admin
  +--> /admin/users                 [Member 1 behavior/RBAC, Member 3 visual baseline]
  +--> /admin/subjects              [Member 1 behavior/RBAC, Member 3 visual baseline]
  +--> Subject Leader assignments  [Member 1]
  \--> any subject as override

Subject Leader
  \--> assigned Subject(s)
       +--> Flow 1 MVC              [Member 2 behavior + Member 1 subject/RBAC + Member 3 visual baseline]
       +--> indexing queue          [Member 3 indexing]
       \--> Flow 3 Razor Pages      [Member 2 behavior + Member 1 subject/RBAC + Member 3 visual baseline]

Student / authenticated learner
  +--> active subject catalogue
  +--> public Student registration [PR #19]
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

## UI coordination

The PR #19 design system is now the shared presentation baseline.

- Prefer `design-tokens.css` + `components.css` over new one-off primitives.
- Preserve responsive/accessibility behavior.
- Do not use UI visibility as an authorization boundary.
- Elevated roles must never be selectable from public registration.
- New Flow 2 UI must follow the same visual language while remaining MVC.

## Documentation workflow

1. Members 2-5 implement owned code/tests.
2. Their PR/handoff describes status, architecture, routes/config changes.
3. They do not edit coordination docs independently.
4. Member 1 reconciles docs with actual code after integration/merge.
5. `master` code is the final source of truth if docs are temporarily behind.
