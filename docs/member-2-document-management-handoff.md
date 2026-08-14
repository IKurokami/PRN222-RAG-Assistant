# Member 2 handoff - Flow 1 Document/Chapter Management

## Status

Flow 1 request/presentation behavior is complete and uses MVC.

Member 2 owns the established business behavior. Member 1 owns the cross-cutting multi-subject/RBAC integration now applied around it.

## Locations

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Do not recreate `Pages/Documents` or `Pages/Chapters`.

## Current behavior

- subject-scoped Document list/filter;
- PDF/DOCX/PPTX upload validation, 50 MB maximum;
- source-file persistence;
- metadata persistence;
- subject-scoped Chapter create/edit/delete;
- chapter selection validation within the same Subject;
- details/edit/delete/re-index;
- safe Chapter deletion by unassigning affected Documents;
- queue handoff through `IDocumentIndexingQueue`.

## Authorization

Writes retain the coarse `AppPolicies.ManageDocuments` requirement and also validate the concrete Subject via `ISubjectAccessService`.

Do not replace the resource check with role-only logic.

A Subject Leader assigned to one Subject must not modify another Subject.

Admin can override subject management.

## PRN222 status

PRN222 is only the seeded demo Subject. Never restore `SeedData.Prn222SubjectId` as the active Flow 1 scope.

## Indexing boundary

Flow 1 request code persists a `Document` containing `SubjectId` and enqueues its Document ID. Parsing/chunking/embedding stays Member 3-owned.

Controllers must not call Ollama or pgvector directly.

## Documentation

Member 2 does not independently edit README/AGENTS/docs. Report status/behavior changes to Member 1 for synchronization.
