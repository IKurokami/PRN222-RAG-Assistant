# Flow 1 MVC migration

## Decision

Flow 1 - Document Management & Indexing uses **ASP.NET Core MVC Controllers + Views** for its request/presentation layer instead of Razor Pages.

This was a presentation-layer migration. The domain model, EF Core schema, document storage rules, indexing queue, parsers, chunker, embedding service, background worker, and indexing state machine remain unchanged.

The later RBAC extension changes who satisfies the existing management policy without changing Flow 1 business behavior.

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
    └── Chapters/
```

The previous `Pages/Documents/` and `Pages/Chapters/` implementations are removed so Flow 1 has one HTTP/presentation implementation.

## Primary routes

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

## Preserved behavior

### Document Management

- authenticated document list/details access;
- management writes through `AppPolicies.ManageDocuments`;
- chapter filtering;
- PDF/DOCX/PPTX validation;
- 50 MB limit;
- PRN222 `ChapterId` server-side validation;
- configured source-file persistence;
- cleanup of a new file when metadata persistence fails;
- initial `Uploaded` persistence;
- queue handoff only after persistence;
- edit/delete/re-index behavior;
- DB-first metadata delete followed by best-effort file cleanup.

### Chapter Management

- authenticated chapter list access;
- create/edit/delete through `AppPolicies.ManageDocuments`;
- chapter number/title validation;
- unique chapter number within PRN222;
- runtime chapter management;
- safe chapter deletion in a transaction;
- linked documents are preserved and have `ChapterId = null` before chapter deletion.

## Authorization after RBAC extension

`AppPolicies.ManageDocuments` is defined globally by Member 1 and is satisfied by:

```text
Admin OR SubjectLeader
```

Student and anonymous users do not satisfy the policy.

This means Admin and Subject Leader can perform Flow 1 write actions. Subject Leader remains the normal academic-content owner; Admin access is an operational override.

All Flow 1 POST actions use `[ValidateAntiForgeryToken]`. Hiding management controls in a view is never the authorization boundary.

Canonical RBAC design: `docs/role-access-control.md`.

## Mixed host

```text
Flow 1 -> MVC Controllers + Views
Flow 2 -> MVC Controllers + Views (pending)
Flow 3 -> Razor Pages
Auth/shell -> Razor Pages
Admin user management -> MVC
```

## Indexing handoff

Flow 1 controllers remain request/presentation adapters and must not absorb parser, chunker, embedding, pgvector retrieval, Ollama generation, or background-worker responsibilities.

```text
DocumentsController upload / re-index
        |
        v
IDocumentIndexingQueue.EnqueueAsync(documentId)
        |
        v
DocumentIndexingWorker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
```

## Tests

Flow 1 tests target MVC input/view models and controllers. Authorization regression now verifies:

- `ManageDocuments` allows Admin and Subject Leader;
- Student/anonymous are denied;
- write actions carry `ManageDocuments`;
- POST actions carry anti-forgery protection.

No EF Core migration is required for the MVC migration or the role-policy extension because neither changes the application persistence model.

## Ownership

- Member 2 owns Flow 1 document/chapter business behavior and MVC workflow code.
- Member 3 owns background indexing.
- Member 1 owns global roles/policies, role-aware shared UI, authorization tests, and all documentation changes.

Members 2/3 report status or documentation needs to Member 1 rather than editing repository docs in parallel.
