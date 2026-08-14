# Project status

Last synchronized against `master` after the merge of PR #12 (Member 2 Flow 3 Report & Statistics).

Source baseline reviewed:

- current `master`: `00903a38693956f59090f71649ca8a99e053e604`
- PR #12: **merged** - implements Flow 3 Report & Statistics for Subject Leaders
- PR #11: **merged** - synchronized documentation after Member 3 / Flow 1 completion
- PR #10: **merged** - established the three-workflow ownership model
- PR #9: **merged** - completed Member 3 document parsing, chunking, embeddings, indexing service, and background worker
- PR #12 verification reported `75/75` automated tests passing
- post-merge local smoke testing reported Flow 1 and Flow 3 working end-to-end with PostgreSQL/pgvector, Ollama embeddings, authorization, and real aggregate data

This file is the quickest status snapshot for team members and coding agents. When another document disagrees with this file, verify against the latest merged code on `master` and then synchronize the documentation.

## Product workflows

The project defines three independent workflows:

1. **Flow 1 - Document Management & Indexing** - COMPLETE
2. **Flow 2 - RAG Question & Answer & Conversation Management** - PENDING
3. **Flow 3 - Report & Statistics** - COMPLETE

Conversation History belongs to Flow 2. It is not counted as the independent third workflow.

## Current project state

| Area | Owner | Status | Notes |
|---|---|---|---|
| Core domain/data/security | Member 1 | Complete baseline | Entities, EF Core configurations/migration baseline, Identity roles/policy, pgvector wiring, shared application contracts, and architecture tests are in place. |
| Chapter Management | Member 2 | Complete / merged | Runtime PRN222 chapter list/create/edit/delete is implemented; chapters are not seed-only data. |
| Document Management | Member 2 | Complete / merged | Upload, list, details, edit, delete, re-index request, authorization, validation, storage, and queue handoff are implemented. |
| Document parsing/chunking/indexing | Member 3 | Complete / merged | PDF (PdfPig), DOCX/PPTX (OpenXml), chunking, batch embeddings, indexing service, worker, chunk replacement, and index-state transitions are implemented. |
| Flow 1 end-to-end | Members 2 + 3 | Complete | Upload/re-index -> queue -> worker -> parse -> chunk -> embed -> `DocumentChunk` persistence -> `Indexed`/`Failed`. |
| Flow 3 Report & Statistics | Member 2 | Complete / merged through PR #12 | Subject-Leader-only read-only dashboard over PRN222 chapter/document/indexing/chunk/chat aggregate data. |
| RAG retrieval / grounded backend | Member 4 | Pending | Question embedding, pgvector retrieval, grounded prompt construction, Ollama chat generation, `IRagQueryService`, message/citation persistence remain. |
| Chat UI / conversation history / citations | Member 5 | Pending | Chat/session UI, Conversation History, citation rendering, and evaluation integration remain. |
| Evaluation set | Member 5 | Pending | `evaluation/` remains reserved for the human-authored 50-question ground-truth set. |

## Flow 1 - end-to-end complete

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

`InMemoryDocumentIndexingQueue` is the active in-process transport consumed by `DocumentIndexingWorker`. It is process-local rather than a durable broker; startup recovery re-enqueues persisted documents still marked `Uploaded` or `Processing`.

## Flow 3 - Report & Statistics - complete

PR #12 completed Member 2's independent reporting workflow.

Primary merged files:

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
tests/PRN222.RagAssistant.Tests/ReportStatisticsTests.cs
```

The Reports page is protected by `AppPolicies.ManageDocuments`, which requires the `SubjectLeader` role.

Current dashboard reports:

- total PRN222 chapters
- total PRN222 documents
- unassigned documents
- document counts by chapter
- document counts by `Uploaded`, `Processing`, `Indexed`, and `Failed`
- indexing completion percentage
- total PRN222 `DocumentChunk` rows
- recent indexing failures with `IndexError`
- recently indexed documents with chunk counts/timestamps
- total chat sessions/messages/citations
- correct zero/empty states before Flow 2 data exists

The implementation is read-only and uses existing persistence. No analytics entity/migration, Ollama reporting call, pgvector retrieval, or indexing mutation was introduced.

Post-merge local smoke testing reported that a real PDF upload progressed through the Flow 1 worker/Ollama embedding path to `Indexed`, after which the Flow 3 dashboard reflected the resulting chapter/document/chunk/indexing data.

See `docs/flow-3-report-statistics-handoff.md` for the completed reporting boundary.

## Current handoff to Member 4

Member 4 is now the primary remaining backend implementation owner.

Member 4 can depend on successfully indexed `DocumentChunk` rows and the merged `ITextEmbeddingService` implementation.

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

Member 5 owns Flow 2 presentation/evaluation:

- chat/session creation and navigation
- asking questions through `IRagQueryService`
- answer/citation rendering
- persisted Conversation History
- evaluation set and evaluation-facing tooling

UI code must not call Ollama or query pgvector directly.

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

Flow 3 did not need a new cross-member reporting contract or schema change.

## Remaining work

The main unfinished product work is now **Flow 2**:

```text
Member 4: RAG backend / retrieval / generation / persistence
Member 5: chat UI / Conversation History / citations / evaluation
```

Do not recreate Flow 1 indexing or Flow 3 reporting in later branches.

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
