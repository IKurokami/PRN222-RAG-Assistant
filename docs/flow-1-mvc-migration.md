# Flow 1 MVC architecture

## Status

Flow 1 presentation was migrated from Razor Pages to MVC and remains MVC.

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

Users select a Subject first through `/subjects`. Flow 1 routes/actions carry `subjectId`, while entity-specific actions derive the trusted SubjectId from the persisted Document/Chapter.

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
 -> enqueue Document.Id when indexing needed
 -> redirect preserving subject context
```

The MVC layer does not parse/chunk/embed/call Ollama/query pgvector.

## Chapter deletion

Deleting a Chapter keeps Documents and sets affected `ChapterId` values to null. The operation is scoped to the same Subject.

## Ownership

Member 2 owns established Flow 1 business behavior. Member 1 owns the multi-subject/RBAC wiring applied across Flow 1. Member 3 owns indexing.
