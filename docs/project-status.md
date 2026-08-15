# Project status

<<<<<<< Updated upstream
> AI-provider backup update based on `master` after merged PR #20. Member 1 owns synchronization of this file.
=======
Last synchronized against `master` after PR #5 was merged.
>>>>>>> Stashed changes

## Workflows

<<<<<<< Updated upstream
| Workflow | Presentation | Status | Owner |
|---|---|---|---|
| Flow 1 - Document Management & Indexing | MVC | Complete | Member 2 request behavior + Member 3 indexing; Member 1 subject/RBAC/provider integration |
| Flow 2 - RAG Q&A + Conversation Management | MVC | Pending | Member 4 backend + Member 5 UI/evaluation |
| Flow 3 - Report & Statistics | Razor Pages | Complete | Member 2 behavior; Member 1 subject/RBAC integration |

Conversation History is part of Flow 2.

## Platform/RBAC/provider state

| Area | Status | Owner |
|---|---|---|
| Core/Data/Identity | Complete | Member 1 |
| Admin/SubjectLeader/Student roles | Complete | Member 1 |
| Admin user/role management | Complete | Member 1 |
| Subject catalogue/Admin Subject management | Complete / merged | Member 1 |
| Subject Leader assignment | Complete / merged | Member 1 |
| Subject-specific authorization service | Complete / merged | Member 1 |
| AI provider-neutral interfaces | Existing baseline | Member 1 contracts |
| Ollama local adapter | Complete; embedding existing + chat adapter added | Member 1 provider foundation around Member 3 indexing |
| Gemini online Free Tier adapter | Implemented in provider-backup PR | **Member 1** |
| Optional OpenAI paid adapter | Implemented in provider-backup PR | **Member 1** |
| Provider selection/env/API-key validation | Implemented in provider-backup PR | **Member 1** |
| Embedding dimension/re-index invariant | Implemented/documented | **Member 1** |
| Public Student registration | Complete / merged in PR #19 | Member 3 implementation; Member 1 RBAC rules |
| Cross-app UI/UX redesign | Complete / merged in PR #19 | Member 3 |
| Documentation synchronization | Updated for provider backup | Member 1 |

## Online-free decision

The main online backup for development/demo is **Google Gemini Developer API Free Tier**:

```text
Chat:      gemini-3.6-flash
Embedding: gemini-embedding-2
```

As of 2026-08-15, Google's official pricing lists Standard Free Tier pricing as free of charge for input/output on `gemini-3.6-flash` and free of charge for Gemini Embedding 2 inputs. Free Tier remains rate-limited and has different data-use terms from paid tier.

OpenAI is retained only as an optional paid provider:

```text
Chat:      gpt-5.6-luna
Embedding: text-embedding-3-small
```

Do not describe OpenAI as the project's free backup.

## Provider selection

```text
RAG_PROVIDER=Ollama   # local/default
RAG_PROVIDER=Gemini   # online Free Tier backup
RAG_PROVIDER=OpenAI   # optional paid API
```

No automatic failover occurs.

## Embedding compatibility

Default dimension:

```text
RAG_EMBEDDING_DIMENSIONS=1024
```

If provider/model/dimension changes, re-index all documents before retrieval. Same vector length is not enough for cross-model compatibility.

## Multi-subject state

PRN222 remains seeded but is not the application-wide hard-coded scope.

```text
Subjects
  +--> Chapters (SubjectId)
  +--> Documents (SubjectId)
  +--> Subject Leader assignments (Identity claims)
  \--> future ChatSessions/RAG subject boundary [Flow 2 pending]
```

## Authorization state

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

Subject-specific actions additionally use `ISubjectAccessService`.

## Flow 1 state

Flow 1 request behavior remains unchanged. Indexing now resolves its embedding implementation through `ITextEmbeddingService` from the selected provider.

Provider switching does not require different workers. A document still queues only `Document.Id`.

## Flow 3 state

Flow 3 remains provider-independent/read-only. It must not call AI providers.

Chat metrics remain global because Flow 2 is pending and current `ChatSession` has no SubjectId.

## Flow 2 remaining requirement

Member 4/5 must not implement global-corpus chat or concrete-provider coupling.

Required direction:

```text
selected subject
 -> ITextEmbeddingService
 -> same-subject pgvector retrieval
 -> IChatCompletionService
 -> same-subject citations/history
```

Any real EF model change remains coordinated by Member 1.

## Next project priority

The major unfinished product workflow remains **Flow 2**. Provider infrastructure is prepared so Member 4 can focus on subject-scoped RAG behavior instead of hard-coding Ollama/online APIs.

## Documentation ownership

Member 1 exclusively edits README, AGENTS files, and `docs/*`.
=======
- `master` merge commit: `4a038bbf428cf96eafb97846f5a00904f9d78b63`
- merged feature: PR #5 - document management
- PR #5 head commit: `b5681e026de0dac94de0f04d8644305cff047a3b`
- CI for the PR #5 head commit completed successfully before merge

This file is the quickest place for team members and coding agents to understand what is already implemented and what remains.

## Current project state

| Area | Owner | Status | Notes |
|---|---|---|---|
| Core domain/data/security | Member 1 | Complete baseline | Entities, EF Core configurations, migration baseline, Identity roles/policy, pgvector wiring, shared application contracts and architecture tests are in place. |
| Chapter Management | Member 2 | Merged | Runtime list/create/edit/delete for PRN222 chapters is implemented. Chapters are not seed-only data. |
| Document Management | Member 2 | Merged | Upload, list, details, edit, delete and re-index request flows are implemented with server-side authorization and validation. |
| Upload-to-index queue handoff | Member 2 -> Member 3 | Complete | Document actions enqueue `Document.Id` through `IDocumentIndexingQueue`, consumed by `DocumentIndexingWorker`. |
| Document parsing/chunking/indexing | Member 3 | Complete | Parsers for PDF (PdfPig), DOCX (OpenXml), PPTX (OpenXml), fixed-size TextChunker, OllamaTextEmbeddingService (`/api/embed`), DocumentIndexingService, and DocumentIndexingWorker are implemented and registered. |
| Retrieval / grounded RAG backend | Member 4 | In Progress | `IRagQueryService`, Ollama chat/embedding services, pgvector retrieval, grounded prompting, and prompt builder are implemented and registered. Pending: Member 5 integration, architecture convention tests. |
| Chat UI / history / citations | Member 5 | Pending | Presentation for chat/history and citation rendering remains. |
| Evaluation set | Member 5 | Pending | `evaluation/` is reserved for the required human-authored evaluation dataset. |

## What Member 1 has already established

The shared persistence model already contains:

- `ApplicationUser`
- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Important invariants already established:

- PRN222 is the current product subject.
- `Chapter` rows may be created and maintained at runtime.
- `(SubjectId, Number)` is unique for chapters.
- `Document.ChapterId` is nullable.
- deleting a chapter must not cascade-delete documents.
- document-management writes use `AppPolicies.ManageDocuments`, restricted to `SubjectLeader`.
- entities do not use navigation properties; EF mapping belongs in dedicated configuration classes.
- application schema changes use the existing EF Core migration chain.

Shared contracts available under `Application/`:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService`
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

## What Member 2 has now merged

### Chapter Management

The repository now contains Razor Pages under `Pages/Chapters/` for:

- list
- create
- edit
- delete

The implementation includes:

- `ManageDocuments` authorization on write pages
- runtime chapter creation without seed/migration changes
- chapter number/title validation
- duplicate chapter-number checks within PRN222
- explicit PRN222 subject scoping
- safe chapter deletion that unlinks referenced documents by setting `Document.ChapterId = null` before deleting the chapter

### Document Management

The repository now contains Razor Pages under `Pages/Documents/` for:

- list/filter
- upload
- details
- edit
- delete
- re-index request

The implementation includes:

- Subject Leader authorization for write operations
- PDF/DOCX/PPTX upload validation
- 50 MB upload size limit
- source-file persistence under configured upload storage
- `Document` metadata persistence
- optional server-side validation that a selected chapter belongs to PRN222
- initial `Uploaded` index state
- queue handoff using the persisted `Document.Id`
- cleanup of the newly written source file if database persistence fails
- Student read access to document list/details without management actions

Tests added by the Member 2 work cover upload validation, temporary queue behavior, chapter input rules, duplicate detection, chapter delete safety, cross-subject/nonexistent chapter rejection and authorization expectations.

## Current integration boundary for Member 3

`InMemoryDocumentIndexingQueue` currently exists under:

```text
src/PRN222.RagAssistant/Infrastructure/Services/InMemoryDocumentIndexingQueue.cs
```

It is a temporary integration stub so Member 2 could complete request-side work independently.

Member 3 should build on the existing contract instead of changing the upload flow unnecessarily:

```text
Document upload/re-index
        |
        v
IDocumentIndexingQueue.EnqueueAsync(documentId)
        |
        v
Member 3 hosted/background worker
        |
        v
IDocumentIndexingService.IndexAsync(documentId)
        |
        +--> parse PDF/DOCX/PPTX
        +--> chunk
        +--> ITextEmbeddingService
        +--> replace DocumentChunk rows
        +--> update Document indexing state
```

Required state transitions remain:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Member 3 owns the real queue/worker/indexing implementation. The temporary queue class and its DI registration may be replaced as part of that integration, but the `IDocumentIndexingQueue` handoff used by Member 2 should remain stable unless all consumers are updated together.

## Current integration boundary for Members 4 and 5

Member 4 should implement `IRagQueryService` and provider/infrastructure services behind the existing shared contracts. The backend must validate chat-session ownership, retrieve indexed PRN222 chunks, generate grounded answers, persist messages/citations and return `RagAnswer` with ordered citations.

Member 5 should consume `IRagQueryService` from the presentation layer and should not query pgvector or call Ollama directly from UI code.

## Files team members should read before starting new work

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
```

When code and this status file disagree, the latest merged code on `master` is the source of truth. Update this document again after each major member workflow is merged.
>>>>>>> Stashed changes
