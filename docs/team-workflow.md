# Team workflow and ownership

This document is the coordination contract for the five-member PRN222 RAG Assistant team. Member 1 owns this document and all other repository documentation.

## Current milestone

- Member 1 Core/Data baseline: **complete**.
- Member 1 Admin/SubjectLeader RBAC + Admin user-management UI: **complete on this branch**.
- Member 2 Flow 1 Document/Chapter Management request side: **complete**, MVC.
- Member 3 Flow 1 Document Indexing/Ingestion: **complete / merged through PR #9**.
- Member 2 Flow 3 Report & Statistics: **complete / merged through PR #12**.
- Member 4 Flow 2 RAG backend: **pending**.
- Member 5 Flow 2 MVC chat/history/citation presentation + evaluation: **pending**.

## Product workflows

1. **Flow 1 - Document Management & Indexing** - COMPLETE - MVC.
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING - MVC.
3. **Flow 3 - Report & Statistics** - COMPLETE - Razor Pages.

Conversation History is part of Flow 2, not a separate workflow.

## Role model

```text
Admin         -> user/role administration + academic-management override + reports
SubjectLeader -> PRN222 chapter/document/indexing-request management + reports
Student       -> learning consumer; pending own Flow 2 sessions/history
```

Policies:

```text
ManageUsers     -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Canonical design: `docs/role-access-control.md`.

## Member responsibilities

### Member 1 - Core/Data + RBAC + documentation

Member 1 owns:

- domain/data/security baseline;
- shared application contracts/models;
- EF schema/migration coordination;
- architecture/convention tests;
- ASP.NET Core Identity configuration/seeding;
- `Admin`, `SubjectLeader`, `Student` role definitions;
- global authorization policies;
- Admin user/role management MVC controller/models/views;
- role-aware shared navigation and role badges;
- cross-workflow authorization regression tests;
- **all repository documentation edits**.

Documentation owned exclusively by Member 1 includes:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

Members 2-5 do **not** independently update these files. They provide status/architecture/handoff notes in their PR description or tell Member 1 what changed. Member 1 then synchronizes documentation against merged code.

Member 1 may modify role-aware UI around another member's screen without taking ownership of that workflow's business logic.

### Member 2 - Flow 1 request side + Flow 3 reporting

Flow 1 locations:

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Owned behavior:

- runtime chapter CRUD;
- document list/filter/upload/details/edit/delete/re-index;
- PDF/DOCX/PPTX validation and 50 MB limit;
- source-file storage and document metadata persistence;
- PRN222 chapter validation;
- queue handoff after persistence;
- safe chapter deletion.

Flow 1 writes consume `AppPolicies.ManageDocuments`. Member 2 does not redefine the policy or role strings.

Flow 3 remains read-only under `Pages/Reports/`. It also consumes `ManageDocuments`, so Admin or Subject Leader may view it.

Member 2 must not implement role-management UI and must not edit repository docs.

### Member 3 - Document Indexing / Ingestion - COMPLETE

Owns:

- PDF/DOCX/PPTX parsing;
- `DocumentParserFactory`;
- `TextChunker`;
- `TextEmbeddingBatcher`;
- `OllamaTextEmbeddingService`;
- `DocumentIndexingService`;
- `DocumentIndexingWorker`;
- coherent `DocumentChunk` replacement;
- indexing state/error/timestamp transitions;
- startup recovery.

Do not build a second indexing pipeline. Member 3 does not edit RBAC or repository docs.

### Member 4 - RAG / Chat Backend - PENDING

Owns:

- question embedding;
- pgvector retrieval over indexed PRN222 chunks;
- top-K context selection;
- grounded prompt/no-evidence behavior;
- `IChatCompletionService`;
- `IRagQueryService`;
- authenticated chat-session ownership validation;
- `ChatMessage` and `MessageCitation` persistence.

Member 4 remains presentation-agnostic. If Flow 2 genuinely needs a new global policy, coordinate with Member 1. Do not invent feature-local role strings and do not edit repository docs.

### Member 5 - Flow 2 MVC Presentation / Conversation Management / Evaluation - PENDING

Owns:

- chat/session MVC controller actions;
- `Views/Chat/`;
- session create/open/navigation;
- Conversation History;
- citation/source rendering;
- consumption of `IRagQueryService`;
- 50-question evaluation set/tooling.

Member 5 consumes established authentication/authorization. It does not create Admin/SubjectLeader management UI and does not edit repository docs.

## Integration boundaries

### Flow 1

```text
Admin or Subject Leader
        |
        v
DocumentsController / ChaptersController
        |
        +--> validate / persist / manage
        |
        v
IDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
DocumentIndexingService
        |
        +--> parse
        +--> chunk
        +--> embed
        +--> DocumentChunk
        \--> Indexed / Failed
```

### Admin user management

```text
Admin browser
    |
    v
AdminUsersController                  [Member 1]
    |
    +--> UserManager<ApplicationUser>
    +--> create account
    +--> assign one managed role
    +--> self-demotion / last-Admin guards
    \--> Identity role membership
```

Subject Leader and Student must fail `ManageUsers` server-side.

### Flow 2

```text
Student browser
        |
        v
Member 5 ChatController + MVC Views
        |
        v
IRagQueryService
        |
        v
Member 4 RAG backend
        |
        +--> authenticated session ownership
        +--> question embedding
        +--> pgvector retrieval
        +--> grounded generation
        +--> messages/citations persistence
```

### Flow 3

```text
Admin or Subject Leader
        |
        v
Pages/Reports
        |
        v
Read-only aggregate EF Core queries
```

## Database coordination

If later work genuinely requires a schema change:

1. document the missing persistence requirement in the PR/handoff notes;
2. coordinate through Member 1;
3. Member 1 updates entity/configuration/migration boundaries as appropriate;
4. synchronize with latest `master`;
5. generate one migration;
6. run pending-model checks and tests;
7. Member 1 updates documentation after merge.

Avoid competing migrations.

The Admin role/user-role changes in the current RBAC work require no migration because the Identity role schema already exists.

## Documentation workflow

This is now explicit team policy:

1. Member 2/3/4/5 implement their owned code/tests.
2. Their PR description includes any status, architecture, route, config, or handoff information that documentation should reflect.
3. They do not edit README/AGENTS/docs in parallel.
4. Member 1 reviews merged/current code and performs documentation synchronization.
5. `master` code remains the source of truth if documentation is temporarily behind.

This centralization is intended to prevent contradictory status files and overlapping docs commits across five parallel members.

## Required reading

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/role-access-control.md
docs/flow-1-mvc-migration.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```
