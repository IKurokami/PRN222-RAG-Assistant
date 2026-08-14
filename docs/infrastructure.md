# Infrastructure baseline

## Runtime stack

- ASP.NET Core .NET 10 host with MVC + Razor Pages.
- ASP.NET Core Identity.
- EF Core + PostgreSQL.
- pgvector for embeddings.
- Ollama for local chat/embedding models.
- runtime source storage under `storage/uploads/`.

PRN222 is the seeded demo subject; the runtime application is multi-subject.

## Authentication/authorization

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Subject-resource authorization is implemented by `ISubjectAccessService` and must accompany `ManageDocuments` for subject-specific writes/reports.

Subject Leader assignment uses existing Identity user claims:

```text
Claim type  = prn222:managed-subject
Claim value = Subject Guid
```

`AspNetUserClaims` already exists, so this feature adds no EF migration.

## PostgreSQL system of record

PostgreSQL persists:

- Subjects/Chapters;
- Documents/index state;
- DocumentChunks/embeddings;
- Identity users/roles/claims;
- ChatSessions/ChatMessages;
- MessageCitations.

Application schema changes use EF Core migrations. Init SQL is limited to runtime database concerns such as enabling `vector`.

## Subject lifecycle

Admin creates/edits/toggles Subjects at runtime. No hard-delete is exposed because data references Subjects.

Visibility:

- Admin: all subjects.
- Subject Leader: active subjects plus assigned inactive subjects.
- Student: active subjects.

Management:

- Admin: all subjects.
- Subject Leader: assigned subjects.
- Student: none.

## MVC/Razor allocation

```text
MVC:
  Flow 1 Documents/Chapters
  pending Flow 2 Chat
  Admin Users
  Subjects/Admin Subjects

Razor Pages:
  Auth/shell
  Flow 3 Reports
```

Global Documents/Chapters/Reports navigation is avoided because those screens need a selected Subject.

## Flow 1 indexing pipeline

```text
subject-aware HTTP request
 -> persist Document with SubjectId
 -> IDocumentIndexingQueue
 -> InMemoryDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parse/chunk/embed/persist
```

The queue is process-local. Startup recovery re-enqueues persisted Uploaded/Processing documents.

Parsers:

- PDF: PdfPig;
- DOCX/PPTX: OpenXml.

The indexing pipeline is not duplicated per subject.

## Ollama

Default local models:

```text
Chat:      qwen3:4b
Embedding: qwen3-embedding:0.6b
```

Indexing and future retrieval must use compatible embedding configuration. MVC/Razor request code and Flow 3 reports do not call Ollama directly.

## Flow 3

Subject-scoped report metrics read PostgreSQL with no workflow mutation.

Current chat metrics are global because Flow 2 has not established Subject ownership for ChatSession yet.

## Flow 2 infrastructure requirement

Before retrieval implementation, Flow 2 must establish a subject-scoped session/query boundary. Retrieval must join/filter through `Document.SubjectId`; citations must stay within that boundary.

A future `ChatSession.SubjectId` model change will require an EF migration. Member 1 coordinates it.

## Demo-user configuration

Demo seeding is disabled by default.

```text
Auth:SeedUsers:Enabled
Auth:SeedUsers:Admin:*
Auth:SeedUsers:SubjectLeader:*
Auth:SeedUsers:Student:*
```

Docker Compose maps the corresponding `AUTH_ADMIN_*`, `AUTH_SUBJECT_LEADER_*`, and `AUTH_STUDENT_*` variables.

Never commit real credentials.

## Intentionally not added

- Redis/RabbitMQ/external broker;
- another vector DB;
- RAGFlow/LangChain service;
- pgAdmin by default;
- automatic FLM crawling;
- subject hard delete;
- public elevated-role self-selection;
- duplicate Flow 1/Flow 2 Razor Pages.

## Validation

Before merge run:

```text
dotnet restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes ...
docker compose config
PostgreSQL migration/schema/pgvector validation through CI
```
