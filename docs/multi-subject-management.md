# Multi-subject management

> Synchronized with `master` after PR #19.

## Goal

Remove PRN222 as a hard-coded application scope while keeping it as the seeded demo subject.

The application supports runtime Subjects such as PRN222, PRJ301, SWT301, SWP391, or future courses without changing source code for each course.

## Existing foundation

The domain contains:

```text
Subject(Id, Code, Name, IsActive)
Chapter(..., SubjectId)
Document(..., SubjectId)
```

The completed multi-subject work added runtime Subject management, Subject Leader assignment, subject-first navigation, and subject-specific authorization. Flow 1/3 no longer use `SeedData.Prn222SubjectId` as active workflow scope.

## Implemented design

### Subject catalogue

Authenticated users enter through:

```text
GET /subjects
```

This gives a concrete subject context before opening Documents, Chapters, or Reports.

The catalogue received the shared UI/UX refresh in PR #19 by Member 3, but visibility/authorization rules remain Member 1-owned.

### Admin subject management

```text
GET  /admin/subjects
GET  /admin/subjects/create
POST /admin/subjects/create
GET  /admin/subjects/{id}/edit
POST /admin/subjects/{id}/edit
GET  /admin/subjects/{id}/leaders
POST /admin/subjects/{id}/leaders
```

Admin can create/edit subjects, toggle `IsActive`, and assign Subject Leaders.

Hard delete is intentionally omitted.

These screens were visually refreshed in PR #19 without changing their authorization model.

### Assignment model

Subject Leader assignment uses ASP.NET Identity claims:

```text
Type  = prn222:managed-subject
Value = Subject Guid
```

No schema change is needed because `AspNetUserClaims` already exists.

### Authorization service

`ISubjectAccessService` is the server-side subject authorization boundary.

It supports:

- accessible subject listing;
- manageable Subject ID resolution;
- subject view checks;
- subject manage checks.

Admin is an override. Subject Leader manages only assigned IDs. Student manages none.

### Flow 1 changes

Documents/Chapters no longer use PRN222 seed ID as workflow scope.

Subject context is preserved across:

- document list/filter;
- upload;
- details/edit/delete/re-index;
- chapter list/create/edit/delete;
- chapter filter/validation;
- redirects and links.

An entity action authorizes against the persisted entity SubjectId so callers cannot move across subject boundaries by modifying query/form values.

PR #19 added document search/status filtering and preserves those filters across delete/re-index redirects. These additions remain inside the selected subject context.

### Flow 3 changes

Reports require `subjectId` and subject-specific manage permission.

Subject-scoped metrics:

- total chapters;
- total documents/unassigned documents;
- indexing state counts;
- total chunks;
- recent failures;
- recently indexed documents;
- document distribution by chapter.

PR #19 refreshed report presentation without changing this boundary.

Chat metrics remain global until Flow 2 persists subject ownership.

## Public Student registration

PR #19 introduced public registration as part of the redesigned auth UI.

A public registration creates only a `Student` account. It does not create Subject Leader assignments and does not allow elevated-role selection.

## Why no migration for current subject/RBAC work

The current multi-subject assignment feature uses an Identity table that already exists. `Subject`, `Chapter.SubjectId`, and `Document.SubjectId` also already existed.

Public registration also uses existing Identity persistence.

Therefore these features do not by themselves require a new application schema migration.

## Flow 2 contract

Flow 2 is the remaining place where subject isolation is not yet complete because the workflow is pending.

Before RAG backend work is complete:

1. establish a selected Subject in the chat/session boundary;
2. persist Subject ownership for each chat session;
3. retrieve only chunks whose `Document.SubjectId` equals that session/selected Subject;
4. persist citations only from that subject;
5. keep Conversation History subject-aware;
6. update Flow 3 chat metrics to subject-scoped once persistence exists.

Do not implement a global retrieval query and plan to filter later.

## Member ownership

Member 1 owns this feature end-to-end:

- Subject management behavior;
- Subject Leader assignment;
- subject authorization service;
- cross-workflow subject-context wiring;
- tests;
- schema coordination;
- all documentation.

Member 2 retains established Flow 1/Flow 3 business behavior.

Member 3 owns the completed PR #19 visual redesign of these screens, not their RBAC/business rules.
