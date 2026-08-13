# Project status

Last synchronized against `master` after the merge of PR #10 (three-workflow ownership/docs) and PR #9 (Member 3 document indexing).

Source baseline reviewed:

- `master` merge commit: `5591b8d872c1a6200ced0dd75ce7af7c524b3038`
- PR #10: **merged** - defines Flow 3 Report & Statistics and folds Conversation History into Flow 2
- PR #9: **merged** - implements Member 3 document parsing, chunking, embeddings, indexing service, and background worker
- PR #9 head commit: `0237813e51414a7535a7da77990d4e3f4156881b`
- PR #9 CI run #43: **successful** before merge

This file is the quickest status snapshot for team members and coding agents. When another document disagrees with this file, verify against the latest merged code on `master` and then synchronize the documentation.

## Product workflows

The project defines three independent workflows:

1. **Flow 1 - Document Management & Indexing**
2. **Flow 2 - RAG Question & Answer & Conversation Management**
3. **Flow 3 - Report & Statistics**

Conversation History belongs to Flow 2. It is not counted as the independent third workflow.

## Current project state

| Area | Owner | Status | Notes |
|---|---|---|---|
| Core domain/data/security | Member 1 | Complete baseline | Entities, EF Core configurations/migration baseline, Identity roles/policy, pgvector wiring, shared application contracts, and architecture tests are in place. |
| Chapter Management | Member 2 | Complete / merged | Runtime PRN222 chapter list/create/edit/delete is implemented; chapters are not seed-only data. |
| Document Management | Member 2 | Complete / merged | Upload, list, details, edit, delete, re-index request, authorization, validation, storage, and queue handoff are implemented. |
| Document parsing/chunking/indexing | Member 3 | Complete / merged | PDF (PdfPig), DOCX/PPTX (OpenXml), chunking, batch embeddings, indexing service, worker, chunk replacement, and index-state transitions are implemented. |
| Flow 1 end-to-end | Members 2 + 3 | Complete | Upload/re-index -> queue -> worker -> parse -> chunk -> embed -> `DocumentChunk` persistence -> `Indexed`/`Failed`. |
| RAG retrieval / grounded backend | Member 4 | Pending | Question embedding, pgvector retrieval, grounded prompt construction, Ollama chat generation, `IRagQueryService`, message/citation persistence remain. |
| Chat UI / conversation history / citations | Member 5 | Pending | Chat/session UI, conversation history, citation rendering, and evaluation integration remain. |
| Flow 3 Report & Statistics | Member 2 | Defined / pending implementation | Read-only Subject Leader reporting over existing document/indexing/chat persistence. |
| Evaluation set | Member 5 | Pending | `evaluation/` remains reserved for the human-authored 50-question ground-truth set. |

## Flow 1 - now end-to-end complete

### Member 2 request/presentation side

Merged behavior includes:

- PRN222 Chapter list/create/edit/delete
- Document list/filter/upload/details/edit/delete/re-index request
- PDF/DOCX/PPTX upload validation
- 50 MB upload limit
- configured source-file persistence
- `Document` metadata persistence with initial `Uploaded` status
- server-side validation that an optional `ChapterId` belongs to PRN222
- `AppPolicies.ManageDocuments` enforcement on write operations
- persisted `Document.Id` handoff through `IDocumentIndexingQueue`
- safe chapter deletion by clearing referenced nullable `Document.ChapterId` values before removing the chapter

### Member 3 background indexing side

PR #9 completed the indexing pipeline:

```text
Document upload / re-index
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
        |
        +--> DocumentParserFactory
        |       +--> PdfDocumentParser (PdfPig)
        |       +--> DocxDocumentParser (OpenXml)
        |       \--> PptxDocumentParser (OpenXml)
        +--> TextChunker
        +--> TextEmbeddingBatcher
        +--> ITextEmbeddingService / Ollama `/api/embed`
        +--> replace/persist DocumentChunk rows
        \--> update Document indexing state
```

Required state flow is implemented:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Successful indexing clears `IndexError` and sets `IndexedAtUtc`. Failures persist `Failed` plus a bounded error message. Re-indexing replaces existing chunks instead of appending duplicates.

### Queue/runtime note

`InMemoryDocumentIndexingQueue` is now the active in-process transport consumed by `DocumentIndexingWorker`; it is no longer an unused Member 2-only stub.

It is intentionally process-local rather than a durable external queue. `DocumentIndexingWorker` rehydrates documents whose persisted state is `Uploaded` or `Processing` at application startup, so the database indexing state remains the recovery source for the course-demo architecture.

Do not introduce Redis/RabbitMQ or a separate worker service unless a concrete requirement justifies it.

## Current handoff to Member 4

Member 4 can now depend on successfully indexed `DocumentChunk` rows and the merged `ITextEmbeddingService` implementation.

Member 4 owns:

- single-question embedding through `ITextEmbeddingService.EmbedAsync`
- pgvector similarity retrieval over indexed PRN222 chunks
- top-K context selection
- grounded/no-evidence behavior
- `IChatCompletionService` implementation for Ollama chat generation
- `IRagQueryService` implementation
- chat-session ownership validation
- persistence of user/assistant `ChatMessage` rows
- persistence of ordered `MessageCitation` rows

Member 4 must not parse raw uploaded files or duplicate the indexing pipeline.

See `docs/member-3-document-indexing-handoff.md` for the completed Member 3 boundary.

## Current handoff to Member 5

Member 5 owns Flow 2 presentation:

- chat/session creation and navigation
- asking questions through `IRagQueryService`
- answer/citation rendering
- persisted Conversation History
- evaluation set and evaluation-facing tooling

UI code must not call Ollama or query pgvector directly.

## Flow 3 - Report & Statistics

Member 2 owns Flow 3 in a separate focused branch such as `feature/report-statistics`.

The initial workflow remains read-only and should use existing persistence to show, at minimum:

- total PRN222 chapters/documents
- document counts by `DocumentIndexStatus`
- document counts by chapter, including unassigned documents
- total chat sessions/messages/citations
- zero/empty states where Flow 2 data does not exist yet

Because Member 3 is now complete, document/indexing statistics can use real persisted index states immediately. Chat usage statistics will naturally remain zero until Members 4/5 populate Flow 2 data.

Do not add analytics tables, event tracking, scheduled aggregation, or a reporting service merely to produce these counts.

## Shared persistence and contracts

Current domain model:

- `ApplicationUser`
- `Subject`
- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Current shared application contracts/models:

- `IDocumentIndexingQueue`
- `IDocumentIndexingService`
- `ITextEmbeddingService` - single-text and ordered batch embedding
- `IChatCompletionService`
- `IRagQueryService`
- `RagAnswer`
- `RagCitation`

Treat these public signatures as cross-member integration points. Prefer additive changes and update all affected producers/consumers together if a signature genuinely must change.

## Required reading before continuing

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-1-core-data-handoff.md
docs/member-2-document-management-handoff.md
docs/member-3-document-indexing-handoff.md
docs/flow-3-report-statistics-handoff.md
```

After each major workflow PR is merged, synchronize this status snapshot and every document whose ownership/status description changed.