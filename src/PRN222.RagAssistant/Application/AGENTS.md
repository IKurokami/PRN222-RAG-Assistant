# Application-layer instructions

This subtree contains stable contracts shared by document management, indexing, RAG, authorization consumers, and presentation workflows.

The project defines:

1. **Flow 1 - Document Management & Indexing** - complete - MVC Controllers + Views
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - MVC Controllers + Views
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

Conversation History belongs to Flow 2.

## Current role model

Global identity/RBAC is owned by **Member 1**.

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers     -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Canonical details live in `docs/role-access-control.md`.

Members 2-5 may consume established policies but must coordinate any new global role/policy requirement with Member 1. Do not duplicate role strings or create feature-local role-management abstractions.

## Current integration status

Member 1 established the shared contracts and now also owns Identity/RBAC, Admin user management, role-aware shared UI, and all repository documentation edits.

Member 2 consumes `IDocumentIndexingQueue` from Flow 1 MVC document upload/re-index actions and owns the completed Flow 3 reporting behavior. Member 3 completed the indexing implementation through PR #9. Member 4 owns the pending presentation-agnostic Flow 2 backend. Member 5 owns pending Flow 2 MVC presentation/evaluation.

Flow 1 handoff:

```text
DocumentsController persists/updates Document
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
IDocumentIndexingService.IndexAsync(documentId)
```

`InMemoryDocumentIndexingQueue` is an in-process transport, not a durable broker. Startup recovery re-enqueues persisted `Uploaded`/`Processing` documents.

## Dependency boundaries

- Presentation code may depend on `Application/Abstractions` and `Application/Models`.
- Application abstractions must not depend on Razor Pages, MVC controllers, `HttpContext`, Ollama-specific DTOs, or PostgreSQL-specific query types.
- Infrastructure implementations may depend on Application abstractions.
- Do not duplicate a shared contract inside a feature folder when an abstraction already exists here.
- Flow 1 MVC controllers must not parse/chunk/embed documents or call Ollama/pgvector directly.
- Member 4 must not parse raw uploaded files or duplicate indexing.
- Member 4 Flow 2 backend remains presentation-agnostic.
- Member 5 Flow 2 presentation uses MVC Controllers + Views, not Razor Pages.
- Member 5 MVC presentation must not call Ollama or query pgvector directly.
- Flow 3 must not call Ollama, run similarity retrieval, mutate indexing state, or change a shared contract solely for dashboard convenience.
- Global authorization changes belong to Member 1 even when the consuming workflow belongs to another member.

## MVC presentation boundaries

### Flow 1

Flow 1 request/presentation code lives in `DocumentsController`, `ChaptersController`, Flow 1 MVC view models, and `Views/Documents` / `Views/Chapters`.

Write actions are protected by `AppPolicies.ManageDocuments`. Member 1 owns the global policy definition; Member 2 owns the document/chapter behavior behind it.

Do not recreate `Pages/Documents` or `Pages/Chapters`.

### Admin user management

Member 1 owns:

```text
Controllers/AdminUsersController.cs
Models/Admin/AdminUserViewModels.cs
Views/AdminUsers/
```

This surface consumes ASP.NET Core Identity directly because account/role administration is identity infrastructure behavior. It must remain protected by `AppPolicies.ManageUsers` and anti-forgery on POST actions.

### Flow 2

Expected path:

```text
Student browser
    |
    v
ChatController / MVC action
    |
    v
IRagQueryService
    |
    v
Flow 2 backend services
    |
    v
RagAnswer + RagCitation[]
    |
    v
MVC View
```

The MVC layer may handle model binding, validation, authorization, redirects, and view selection. It must not implement embeddings, pgvector retrieval, prompt construction, Ollama generation, or indexing.

Do not introduce `Pages/Chat` or `Pages/Conversation`.

## Current shared contracts

### `IDocumentIndexingQueue`
Request-to-background handoff from Flow 1 upload/re-index actions.

### `IDocumentIndexingService`
One-document indexing pipeline executed by `DocumentIndexingWorker`.

### `ITextEmbeddingService`
Provider-neutral embedding boundary shared by indexing and retrieval. Supports single-text and ordered-batch embedding.

### `IChatCompletionService`
Provider-neutral generation boundary for Member 4.

### `IRagQueryService`
Presentation-facing grounded Q&A boundary. Member 4 implements it; Member 5 consumes it from MVC.

### `RagAnswer` / `RagCitation`
Presentation-safe Flow 2 result models.

There is intentionally no reporting-specific shared contract.

## Ownership expectations

### Member 1 - Core/Data/RBAC/docs

Owns shared contracts, schema/migration coordination, Identity/RBAC, Admin user management, role-aware shared UI, policy tests, and **all edits to README/AGENTS/docs**.

### Member 2 - Flow 1 + Flow 3

Owns Flow 1 document/chapter business actions and Flow 3 read-only reporting behavior. Role-policy changes around those screens are coordinated through Member 1.

### Member 3 - Indexing

Owns parsers, chunking, embedding batching, indexing service/worker, chunk persistence, state transitions, and startup recovery.

### Member 4 - Flow 2 backend

Owns retrieval, grounding, chat completion, session ownership validation, and message/citation persistence.

### Member 5 - Flow 2 MVC presentation/evaluation

Owns chat/session views/controllers, Conversation History, citations, and evaluation-facing tooling.

## Contract and documentation changes

Cross-member interfaces are stable integration points. Prefer additive changes.

If a signature must change:

1. explain why the current contract cannot represent the requirement;
2. coordinate with affected producers/consumers;
3. update implementations/consumers together;
4. report the change to Member 1;
5. Member 1 updates project status, team workflow, README, and handoff documentation.

Members 2-5 should not independently edit repository documentation.
