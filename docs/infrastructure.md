# RAG infrastructure baseline

## Product context

The demo targets PRN222. Course documents are curated by Subject Leaders; Admins manage application accounts/roles and can override academic-management operations; Students consume successfully indexed content through pending Flow 2 chat.

Workflows:

1. **Flow 1 - Document Management & Indexing** - complete - MVC
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - MVC
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

## Current infrastructure state

The baseline includes:

- ASP.NET Core MVC + Razor Pages in one host;
- ASP.NET Core Identity;
- `Admin`, `SubjectLeader`, `Student` roles;
- `ManageUsers` and `ManageDocuments` policies;
- Admin MVC user/role administration;
- PostgreSQL + pgvector;
- EF Core persistence/migrations;
- Ollama local runtime;
- uploaded-file storage;
- complete Flow 1 request/indexing pipeline;
- complete read-only Flow 3 dashboard;
- shared application contracts for indexing and RAG handoffs.

Still pending:

- Flow 2 pgvector retrieval and grounded RAG backend;
- Flow 2 chat/session/history/citation MVC presentation and evaluation.

## ASP.NET Core host

```text
MVC Controllers + Views -> Flow 1 + pending Flow 2 + Admin user management
Razor Pages             -> Flow 3 + auth/shell pages
```

`Program.cs` keeps both MVC and Razor Pages registered/mapped.

## Authentication and authorization

### Roles

```text
Admin
SubjectLeader
Student
```

### Policies

```text
AppPolicies.ManageUsers     -> Admin
AppPolicies.ManageDocuments -> Admin OR SubjectLeader
```

`ManageDocuments` protects Flow 1 document/chapter writes and Flow 3 reports. `ManageUsers` protects the Admin account/role management controller.

Role-aware navigation is presentation only; policy attributes remain the server-side boundary.

Canonical role design: `docs/role-access-control.md`.

### Admin user management

Member 1 owns:

```text
Controllers/AdminUsersController.cs
Models/Admin/AdminUserViewModels.cs
Views/AdminUsers/
```

The controller uses `UserManager<ApplicationUser>` to create accounts and change application role membership.

Safety rules:

- POST actions validate anti-forgery tokens;
- Subject Leader and Student cannot satisfy `ManageUsers`;
- a signed-in Admin cannot remove their own Admin role;
- the last Admin cannot be demoted;
- hard-delete is intentionally not exposed because persisted workflow rows reference users.

No new entity/column is required for Admin or role membership; existing Identity tables are used, so no EF migration is required.

## Demo user configuration

Demo-user seeding is disabled by default through:

```text
Auth:SeedUsers:Enabled
```

When enabled, `IdentitySeeder` ensures all three roles and can seed:

```text
Auth:SeedUsers:Admin:Email
Auth:SeedUsers:Admin:Password
Auth:SeedUsers:Admin:DisplayName

Auth:SeedUsers:SubjectLeader:Email
Auth:SeedUsers:SubjectLeader:Password
Auth:SeedUsers:SubjectLeader:DisplayName

Auth:SeedUsers:Student:Email
Auth:SeedUsers:Student:Password
Auth:SeedUsers:Student:DisplayName
```

Docker Compose maps:

```text
AUTH_ADMIN_EMAIL
AUTH_ADMIN_PASSWORD
AUTH_ADMIN_DISPLAY_NAME
AUTH_SUBJECT_LEADER_EMAIL
AUTH_SUBJECT_LEADER_PASSWORD
AUTH_SUBJECT_LEADER_DISPLAY_NAME
AUTH_STUDENT_EMAIL
AUTH_STUDENT_PASSWORD
AUTH_STUDENT_DISPLAY_NAME
```

Example credentials belong only in `.env.example`; real credentials must never be committed.

## Flow 1 MVC boundary

```text
Controllers/DocumentsController.cs
Controllers/ChaptersController.cs
Models/Documents/
Models/Chapters/
Views/Documents/
Views/Chapters/
```

Flow 1 controllers handle request-side validation/persistence/orchestration and hand off indexing through `IDocumentIndexingQueue`. They must not parse/chunk/embed, call Ollama directly, or run pgvector similarity retrieval.

Access:

```text
read catalogue/details -> authenticated users
write chapter/document actions -> Admin OR SubjectLeader through ManageDocuments
```

## Document indexing queue and worker

```text
DocumentsController upload / re-index
        |
        v
IDocumentIndexingQueue
        |
        v
InMemoryDocumentIndexingQueue
        |
        v
DocumentIndexingWorker
        |
        v
IDocumentIndexingService
```

The queue is process-local. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

Merged parser support:

- PDF via PdfPig;
- DOCX via OpenXml Wordprocessing;
- PPTX via OpenXml Presentation.

`ITextEmbeddingService` supports single-text retrieval embedding and ordered batch indexing embedding. Indexing and retrieval must use the same configured model.

## PostgreSQL + pgvector

PostgreSQL is the system of record for:

- subjects/chapters;
- documents/index state;
- chunks/embeddings;
- users/roles/user-role membership;
- chat sessions/messages;
- citations.

Application schema changes use EF Core migrations. PostgreSQL init scripts are limited to runtime concerns such as enabling `vector`.

## Ollama

Default development models:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Member 3 uses embeddings for indexing. Member 4 will use question embeddings and chat completion for Flow 2. Controllers and Flow 3 reporting do not call Ollama directly.

## Workflow architecture

### Flow 1 - COMPLETE

```text
Admin or Subject Leader
    |
    +--> ChaptersController
    \--> DocumentsController
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
            +--> DocumentChunk + pgvector
            \--> Indexed / Failed
```

### Admin identity management - COMPLETE on this branch

```text
Admin
  |
  v
/admin/users
  |
  v
AdminUsersController
  |
  +--> create Identity user
  +--> assign managed role
  +--> protect self/last Admin
  \--> Identity persistence
```

### Flow 2 - PENDING

```text
Student browser
    |
    v
ChatController + Views/Chat
    |
    v
IRagQueryService
    |
    +--> authenticated session ownership
    +--> question embedding
    +--> pgvector retrieval
    +--> grounded generation
    +--> persist messages/citations
```

### Flow 3 - COMPLETE

```text
Admin or Subject Leader
      |
      v
Pages/Reports
      |
      v
Read-only aggregate EF Core queries
```

## Shared application contracts

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

## Document storage

Uploaded sources live under `storage/uploads/` and are mounted at `/app/storage/uploads` in Compose. Runtime uploads must not be committed.

## Intentionally not added

- Redis/RabbitMQ or separate worker service;
- another vector database;
- RAGFlow/LangChain service;
- automatic FLM crawling;
- analytics warehouse/event pipeline;
- public role self-selection;
- hard-delete user lifecycle;
- duplicate Razor Pages implementations for Flow 1 or Flow 2.

## Documentation ownership

Member 1 is the sole editor for:

```text
README.md
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/*
```

Members 2-5 report configuration/architecture/status changes to Member 1 rather than editing these files in parallel.
