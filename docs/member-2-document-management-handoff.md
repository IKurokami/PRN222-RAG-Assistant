# Member 2 - Document Management and Report & Statistics handoff

## Status

Member 2 owns two completed workflow responsibilities:

1. **Flow 1 request/presentation side - COMPLETE - MVC Controllers + Views**
2. **Flow 3 Report & Statistics - COMPLETE / merged through PR #12 - Razor Pages**

Member 3's PR #9 completed the downstream indexing side of Flow 1. Member 1 owns the global Admin/SubjectLeader/Student role model, authorization policies, role-aware shared UI, and all repository documentation.

For the canonical snapshot, see `docs/project-status.md` and `docs/role-access-control.md`.

## Completed Flow 1 scope

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

The old `Pages/Documents/` and `Pages/Chapters/` implementations are removed.

### Chapter Management

Current behavior:

- authenticated list access;
- create/edit/delete protected by `AppPolicies.ManageDocuments`;
- anti-forgery validation on POST;
- chapter number/title validation;
- duplicate number protection within PRN222;
- runtime-managed chapters;
- safe delete that preserves linked documents by clearing `ChapterId` first.

### Document Management

Current behavior:

- authenticated list/details;
- list/filter/upload/edit/delete/re-index;
- PDF/DOCX/PPTX validation;
- 50 MB limit;
- configured source storage;
- metadata persistence with initial `Uploaded` status;
- PRN222 `ChapterId` validation;
- enqueue persisted `Document.Id` through `IDocumentIndexingQueue`;
- orphan-file cleanup when database persistence fails;
- DB-first metadata deletion plus best-effort file cleanup.

## Authorization contract consumed by Member 2

Member 2 must continue protecting Flow 1 write actions and Flow 3 access with:

```text
AppPolicies.ManageDocuments
```

Member 1 owns the global definition. Current policy mapping is:

```text
ManageDocuments -> Admin OR SubjectLeader
```

Therefore:

- Subject Leader is the normal academic-content manager;
- Admin may perform the same management actions as an operational override;
- Student cannot perform writes or access Reports;
- Member 2 must not replace the policy with hard-coded `SubjectLeader` checks.

Member 2 does not implement Admin user/role management.

## Flow 1 indexing handoff - fulfilled

```text
DocumentsController upload / re-index
        |
        v
Persist Document / update index state
        |
        v
IDocumentIndexingQueue.EnqueueAsync(documentId)
        |
        v
InMemoryDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
DocumentIndexingService
        |
        +--> parse PDF / DOCX / PPTX
        +--> chunk
        +--> batch embed
        +--> replace DocumentChunk rows
        \--> Indexed / Failed
```

Member 2 must not move parsing/chunking/embedding into controllers.

## Flow 3 - Report & Statistics

Flow 3 remains under:

```text
Pages/Reports/Index.cshtml
Pages/Reports/Index.cshtml.cs
```

Metrics include:

- PRN222 chapter/document totals;
- documents by index state;
- documents by chapter/unassigned;
- persisted chunk total;
- indexing completion percentage;
- recent failures and recently indexed documents;
- chat session/message/citation totals;
- graceful zero/empty states before Flow 2 data exists.

Flow 3 is read-only. Access is Admin or Subject Leader through `ManageDocuments`.

It must not mutate workflow state, enqueue documents, perform similarity retrieval, call Ollama, or create speculative analytics persistence.

## Remaining handoff

### Member 4

Build presentation-agnostic RAG retrieval/generation on successfully indexed chunks. Any new global policy must be coordinated with Member 1.

### Member 5

Build Flow 2 MVC chat/session/history/citation presentation and evaluation. Do not create `Pages/Chat` or `Pages/Conversation` and do not add role-management UI.

## Documentation rule

Member 2 should **not** edit `README.md`, `AGENTS.md`, or files under `docs/` in future feature PRs.

When Member 2 work changes status, routes, behavior, configuration, or handoff details:

1. describe the documentation impact in the PR/handoff;
2. tell Member 1 what changed;
3. Member 1 updates repository documentation after reviewing actual code.

This file itself is maintained by Member 1.

## Tests

Flow 1 regression coverage includes validation, queue behavior, chapter-delete safety, policy attributes, and anti-forgery attributes. The RBAC suite additionally verifies Admin and Subject Leader satisfy `ManageDocuments` while Student/anonymous do not.

No EF migration is required for the Flow 1 MVC presentation or the Admin/SubjectLeader role-policy update.
