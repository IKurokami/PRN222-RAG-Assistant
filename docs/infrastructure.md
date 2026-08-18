# Infrastructure baseline

> Synchronized after PR #30 merged and issue #27 closed on 2026-08-18.

## Runtime stack

- ASP.NET Core .NET 10 host with MVC + Razor Pages.
- ASP.NET Core Identity.
- EF Core + PostgreSQL.
- pgvector for embeddings/retrieval.
- provider-neutral AI contracts with Ollama/Gemini/OpenAI/OpenRouter adapters.
- runtime source storage under `storage/uploads/`.
- Bootstrap + Bootstrap Icons plus the shared design system.

PRN222 is the seeded demo subject; the runtime application is multi-subject.

## AI provider selection

Backward-compatible default:

```text
Rag:Provider = Ollama | Gemini | OpenAI | OpenRouter
```

Purpose-specific overrides:

```text
Rag:ChatProvider      = Ollama | Gemini | OpenAI | OpenRouter
Rag:EmbeddingProvider = Ollama | Gemini | OpenAI | OpenRouter
```

Docker/env equivalents:

```text
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
RAG_EMBEDDING_DIMENSIONS=1024
```

Infrastructure registers one implementation of each contract:

```text
ITextEmbeddingService
IChatCompletionService
```

The chat and embedding implementations may come from different providers. Workflow code must not branch on provider names.

There is no hidden application-level local-to-cloud failover. Cloud selection remains explicit. OpenRouter model/provider fallback is allowed only after OpenRouter is explicitly selected.

## Embedding vector-space invariant

Matching vector dimensions do not make embedding models semantically compatible.

Operational rule:

```text
change embedding provider/model/dimension
  -> treat stored embeddings as stale
  -> re-index the complete searchable corpus
  -> only then use similarity retrieval
```

Do not rotate embedding models within one corpus. Chat-only provider/model/fallback changes do not require re-indexing.

## Docker modes

Local Ollama:

```bash
docker compose --profile local-ai up -d --build
```

Cloud/hybrid:

```bash
docker compose up -d --build
```

If either selected contract uses Ollama, enable the `local-ai` profile.

## Secrets and cloud-data boundary

Real provider keys remain server-side environment/deployment secrets only.

Never commit API keys to `.env.example`, appsettings, source, docs or tests using real credentials. Never render them to browser code or logs.

Selecting Gemini/OpenAI/OpenRouter sends embedding text and/or chat context to an external provider. Treat provider selection as a privacy/deployment decision as well as a cost/performance choice.

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

Subject-resource authorization is implemented by `ISubjectAccessService` for Flow 1/3 resource actions.

Flow 2 backend validates chat-session ownership and subject consistency through `IRagQueryService`.

## PostgreSQL system of record

PostgreSQL persists:

- Subjects/Chapters;
- Documents/index state;
- DocumentChunks/embeddings;
- Identity users/roles/claims;
- ChatSessions including `SubjectId`;
- ChatMessages;
- MessageCitations.

PR #30 added the `ChatSession.SubjectId` persistence required for subject-scoped RAG sessions.

## Presentation allocation

```text
MVC:
  Flow 1 Documents/Chapters                 [complete]
  Flow 2 final Chat/history/citation UI     [pending Member 5]
  Admin Users
  Subjects/Admin Subjects

Razor Pages:
  Auth/shell
  Flow 3 Reports
  internal RAG demo                         [development aid only]
```

The internal RAG demo is not the final Flow 2 product presentation.

## Flow 1 indexing pipeline

```text
subject-aware HTTP request
 -> persist Document with SubjectId
 -> IDocumentIndexingQueue
 -> InMemoryDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> IDocumentIndexingService
 -> parse PDF/DOCX/PPTX
 -> TextChunker
 -> ITextEmbeddingService
 -> replace DocumentChunk rows / persist status
```

The queue is process-local. Startup recovery re-enqueues persisted Uploaded/Processing documents.

Parsers:

- PDF: PdfPig;
- DOCX/PPTX: OpenXml.

The indexing pipeline is not duplicated per subject or provider.

### PR #30 / issue #27 hardening

Merged changes include:

- deterministic bounded chunk overlap;
- Unicode normalization and safer grapheme boundaries;
- configurable `ChunkingOptions` with startup validation;
- improved PDF two-column reading order and regression coverage;
- DOCX blank-paragraph/page-number correction;
- additional DOCX/PPTX parser/integration coverage.

PDF is the primary real-world ingestion format receiving the most active testing.

Deferred follow-up debt:

- complex DOCX list/table/layout fixtures;
- PPTX grouped-shape/table/parent-transform fixtures;
- harder PDF table/side-note/rotated-text layouts.

## Flow 2 backend infrastructure - COMPLETE BASELINE

Merged Member 4 path:

```text
subject-aware ChatSession
 -> IRagQueryService
 -> ITextEmbeddingService
 -> PgVectorDocumentChunkRetriever
 -> indexed Documents constrained by SubjectId
 -> GroundedPromptBuilder
 -> IChatCompletionService
 -> referenced citation parsing
 -> ChatMessage + MessageCitation persistence
```

Important properties:

- session lookup includes authenticated user ownership;
- conflicting caller/session subject IDs are rejected;
- subject-aware session creation/reuse is provided by the RAG service;
- conversation history is loaded before persisting the current turn;
- only citation markers referenced in the answer are persisted;
- RAG/chunking options are validated at startup;
- failure-path tests ensure provider failures do not persist incomplete conversation turns before generation succeeds.

Member 4 remains provider-neutral and must not call concrete provider APIs directly.

## Flow 3 infrastructure

Flow 3 remains provider-independent/read-only.

Because `ChatSession.SubjectId` now exists, existing report-side chat aggregates should be audited when Member 5 completes Flow 2 so chat metrics are explicitly subject-scoped.

## Ownership and actual contribution

Ownership:

- Member 1: Core/Data/RBAC/multi-subject/provider infrastructure/docs.
- Member 2: Flow 1 request behavior + Flow 3 reporting.
- Member 3: indexing/ingestion maintenance + UI/UX baseline.
- Member 4: merged Flow 2 RAG backend.
- Member 5: pending Flow 2 MVC/evaluation product layer.

Actual merged contribution credit is tracked separately in `docs/member-contributions.md`.

In particular, PR #9/#23 implementation credit belongs to Member 1 and PR #30 issue #27 remediation credit belongs to Member 4, while Member 3 retains indexing maintenance ownership.

Project documentation uses Member numbers only; do not add GitHub usernames.

## Intentionally not added

- hidden application-level local-to-cloud failover;
- embedding-model rotation;
- Redis/RabbitMQ/external broker;
- another vector DB;
- provider-specific logic in MVC/Razor pages;
- provider-specific contracts in Application;
- API keys in repository files.

## Validation

Before merge run the repository's CI-equivalent checks:

```text
dotnet tool restore
dotnet libman restore
dotnet restore
dotnet build
dotnet test
dotnet ef migrations has-pending-model-changes ...
docker compose config
PostgreSQL migration/schema/pgvector validation
```
