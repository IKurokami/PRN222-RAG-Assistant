# Project status

Last synchronized against `master` after PR #5 was merged.

Source baseline reviewed:

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
| Upload-to-index queue handoff | Member 2 -> Member 3 | Integrated with temporary stub | Document actions enqueue `Document.Id` through `IDocumentIndexingQueue`. `InMemoryDocumentIndexingQueue` is intentionally temporary and must be replaced/integrated by Member 3. |
| Document parsing/chunking/indexing | Member 3 | Pending | No real parser, chunker, hosted indexing worker, indexing service or Ollama embedding implementation is merged yet. |
| Retrieval / grounded RAG backend | Member 4 | Pending | pgvector retrieval, grounded prompting, chat generation and `IRagQueryService` implementation remain. |
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
