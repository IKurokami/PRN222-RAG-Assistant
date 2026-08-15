# Flow 1 MVC architecture

> Synchronized with `master` after PR #19.

## Status

Flow 1 presentation was migrated from Razor Pages to MVC and remains MVC. Functional behavior is complete.

Current locations:

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Do not recreate the removed `Pages/Documents` or `Pages/Chapters` implementation.

## Multi-subject update

Flow 1 is no longer scoped to `SeedData.Prn222SubjectId`.

Users select a Subject first through `/subjects`. Flow 1 routes/actions carry `subjectId`, while entity-specific actions derive the trusted SubjectId from persisted Document/Chapter data.

Authorization:

```text
coarse: ManageDocuments -> Admin OR SubjectLeader
resource: ISubjectAccessService -> concrete SubjectId
```

Subject Leader can write only assigned Subjects. Admin can manage any Subject.

## Request/indexing boundary

```text
MVC action
 -> validate subject/chapter/file
 -> persist Document/Chapter
 -> enqueue Document.Id when indexing is needed
 -> redirect preserving subject context
```

The MVC layer does not parse/chunk/embed/call Ollama/query pgvector.

## PR #19 UI/filter update

Member 3 completed the current Flow 1 visual refresh as part of the cross-app UI/UX redesign.

PR #19 also introduced document list filtering support for:

- text search across document title/original file name;
- indexing status;
- existing chapter filter.

Delete and re-index redirects preserve current `selectedChapterId`, `searchTerm`, and `selectedStatus` so the redesigned list does not unexpectedly lose user filter context.

These presentation/filter additions do not change the subject authorization or indexing boundaries above.

## Chapter deletion

Deleting a Chapter keeps Documents and sets affected `ChapterId` values to null. The operation is scoped to the same Subject.

## Ownership

- Member 2 owns established Flow 1 request/business behavior.
- Member 1 owns multi-subject/RBAC wiring around Flow 1.
- Member 3 owns indexing and the completed PR #19 visual redesign.

The UI/UX assignment does not transfer Flow 1 business semantics away from Member 2.
