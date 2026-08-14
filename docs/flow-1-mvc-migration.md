# Flow 1 MVC migration

## Decision

Flow 1 - Document Management & Indexing now uses **ASP.NET Core MVC Controllers + Views** for its request/presentation layer instead of Razor Pages.

This is a presentation-layer migration only. The existing domain model, EF Core schema, document storage rules, indexing queue, parsers, chunker, embedding service, background worker, and indexing state machine remain unchanged.

## MVC structure

```text
src/PRN222.RagAssistant/
├── Controllers/
│   ├── DocumentsController.cs
│   └── ChaptersController.cs
├── Models/
│   ├── Documents/DocumentViewModels.cs
│   └── Chapters/ChapterViewModels.cs
└── Views/
    ├── Documents/
    │   ├── Index.cshtml
    │   ├── Upload.cshtml
    │   ├── Details.cshtml
    │   └── Edit.cshtml
    ├── Chapters/
    │   ├── Index.cshtml
    │   ├── Create.cshtml
    │   ├── Edit.cshtml
    │   └── Delete.cshtml
    ├── Shared/_ValidationScriptsPartial.cshtml
    ├── _ViewImports.cshtml
    └── _ViewStart.cshtml
```

The previous `Pages/Documents/` and `Pages/Chapters/` implementations are removed so Flow 1 has a single HTTP/presentation implementation.

## Route mapping

The existing conventional MVC route is used:

```text
{controller=Home}/{action=Index}/{id?}
```

Primary Flow 1 routes are therefore:

```text
GET  /Documents/Index
GET  /Documents/Upload
POST /Documents/Upload
GET  /Documents/Details/{id}
GET  /Documents/Edit/{id}
POST /Documents/Edit/{id}
POST /Documents/Delete/{id}
POST /Documents/Reindex/{id}

GET  /Chapters/Index
GET  /Chapters/Create
POST /Chapters/Create
GET  /Chapters/Edit/{id}
POST /Chapters/Edit/{id}
GET  /Chapters/Delete/{id}
POST /Chapters/Delete/{id}
```

`/Documents` and `/Chapters` also resolve to each controller's `Index` action through the conventional default action.

## Preserved behavior

### Document Management

The MVC implementation preserves:

- authenticated document list/details access
- Subject-Leader-only write operations through `AppPolicies.ManageDocuments`
- chapter filtering
- PDF/DOCX/PPTX upload validation
- 50 MB upload limit
- PRN222 server-side `ChapterId` validation
- configured source-file persistence
- cleanup of a newly written file when metadata persistence fails
- `Document` persistence with initial `Uploaded` status
- queue handoff only after document persistence
- edit, delete, and re-index behavior
- DB-first delete consistency followed by best-effort physical-file cleanup

### Chapter Management

The MVC implementation preserves:

- authenticated chapter list access
- Subject-Leader-only create/edit/delete
- chapter number/title validation
- unique chapter number within PRN222
- runtime chapter management
- safe chapter deletion in a transaction
- linked documents are preserved and have `ChapterId` set to `null` before chapter deletion

## Authorization and CSRF protection

Flow 1 MVC controllers use `[Authorize]` for authenticated read access and `[Authorize(Policy = AppPolicies.ManageDocuments)]` for all write actions.

All Flow 1 POST actions use `[ValidateAntiForgeryToken]`. Hiding management buttons in a View is not treated as the authorization boundary.

## Mixed host after migration

The application still intentionally enables both presentation models:

```text
Flow 1 -> MVC Controllers + Views
Flow 2 -> MVC Controllers + Views (pending)
Flow 3 -> Razor Pages
Auth / shell pages -> Razor Pages
```

`Program.cs` therefore continues to register/map both MVC and Razor Pages.

## Tests

Flow 1 tests now reference MVC input/view models rather than Razor `PageModel` classes. Authorization tests reflect on `DocumentsController` and `ChaptersController`, verify the `ManageDocuments` policy on write actions, and verify anti-forgery protection on POST actions.

No EF Core migration is required for this change because the persistence model is unchanged.

## Ownership boundary

The MVC controllers remain request/presentation adapters for the already-completed Flow 1 behavior. They must not absorb parser, chunker, embedding, pgvector retrieval, Ollama generation, or background-worker responsibilities.

Document upload/re-index still hands off to the existing indexing pipeline through `IDocumentIndexingQueue`.
