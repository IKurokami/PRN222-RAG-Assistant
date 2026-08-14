# Member 3 - Document Indexing / Ingestion handoff

## Status

Member 3's Flow 1 background indexing work is **complete and merged** through PR #9.

Merged baseline reviewed:

- PR #9: `feat(indexing): implement Member 3 document parsing, chunking, embedd…`
- PR #9 head commit: `0237813e51414a7535a7da77990d4e3f4156881b`
- PR #9 CI run #43 on the PR head: **success**
- Flow 3 later merged through PR #12 and now consumes the persisted indexing output read-only

This handoff records the completed Flow 1 indexing boundary for Member 4, Member 5, and the already-completed Flow 3 reporting workflow.

The remaining Flow 2 presentation is assigned to **ASP.NET Core MVC Controllers + Views**; this does not change Member 3's indexing boundary.

## Completed scope

Member 3 implemented:

- `DocumentParserFactory`
- `IDocumentParser`
- `PdfDocumentParser` using PdfPig
- `DocxDocumentParser` using OpenXml
- `PptxDocumentParser` using OpenXml
- `ParsedPage`
- `TextChunker`
- `TextEmbeddingBatcher`
- `OllamaTextEmbeddingService`
- single-text and ordered batch support on `ITextEmbeddingService`
- `DocumentIndexingService`
- `DocumentIndexingWorker`
- dependency-injection registration for the indexing pipeline
- parser/chunker tests

## End-to-end Flow 1 integration

```text
Member 2 upload / re-index
        |
        v
Persist Document with IndexStatus = Uploaded
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
        +--> resolve uploaded source file
        +--> select parser by extension
        +--> parse content
        +--> chunk text
        +--> embed chunks in bounded batches
        +--> replace existing DocumentChunk rows
        \--> update Document status/timestamps/errors
```

Implemented state transitions:

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

On success:

- stale chunks for the document are removed
- new `DocumentChunk` rows are inserted in coherent chunk order
- `IndexStatus` becomes `Indexed`
- `IndexedAtUtc` is set
- `IndexError` is cleared

On failure:

- `IndexStatus` becomes `Failed`
- a bounded error message is persisted in `IndexError`

## Queue behavior

`InMemoryDocumentIndexingQueue` is the active in-process queue implementation used by the merged worker.

It is not a durable external broker. Recovery is handled through persisted document state: when the application starts, `DocumentIndexingWorker` queries documents still marked `Uploaded` or `Processing` and re-enqueues their IDs.

For the current single-app course demo, this is the intended baseline. Do not add Redis, RabbitMQ, or a separate worker service without an explicit requirement.

## Parsing behavior

Supported uploaded formats:

- `.pdf` -> PdfPig
- `.docx` -> OpenXml Wordprocessing
- `.pptx` -> OpenXml Presentation

Unsupported extensions are rejected by the parser factory.

The parser output preserves page/slide metadata where available so citations can later point back to the source location.

## Chunking and embeddings

`TextChunker` produces ordered chunks with overlap and preserves page/slide metadata.

`ITextEmbeddingService` supports:

- `EmbedAsync(string text, ...)` for single-text/query embedding
- `EmbedBatchAsync(IReadOnlyList<string> texts, ...)` for ordered batch embedding

Member 3 uses the batch path for indexing. Member 4 should use the single-text path for question embedding so indexing and retrieval share the same configured embedding model.

Default embedding model remains configuration-driven (`qwen3-embedding:0.6b` in the local baseline). If the embedding model changes after documents have been indexed, affected documents must be re-indexed.

## Handoff to Member 4 - RAG backend

Member 4 can assume Flow 1 produces persisted, successfully indexed PRN222 `DocumentChunk` rows with embeddings.

Member 4 should:

1. validate the authenticated chat-session owner;
2. persist the user's question;
3. embed the question using `ITextEmbeddingService.EmbedAsync`;
4. retrieve top-K evidence from successfully indexed PRN222 chunks using pgvector;
5. build grounded context with source metadata;
6. generate the answer through `IChatCompletionService`;
7. persist the assistant message and ordered `MessageCitation` rows;
8. return `RagAnswer` / `RagCitation` to the presentation layer;
9. provide explicit no-evidence/out-of-scope behavior when retrieval is insufficient.

Member 4 must remain presentation-agnostic and must not:

- parse raw uploaded files
- reimplement chunking
- generate document embeddings itself
- bypass the existing `ITextEmbeddingService`
- mutate indexing state as part of normal question answering
- duplicate the completed Flow 3 reporting workflow
- put pgvector/Ollama logic directly in an MVC controller

## Handoff to Member 5 - Flow 2 MVC presentation

Member 5 should consume `IRagQueryService` from **ASP.NET Core MVC Controllers + Views**.

Expected presentation areas:

```text
src/PRN222.RagAssistant/Controllers/
src/PRN222.RagAssistant/Views/Chat/
```

Conversation History and citation rendering remain part of Flow 2 presentation.

Member 5 does not need to know parser/chunker/Ollama embedding payload details, must not query pgvector directly from controllers/views, and must not create a parallel Razor Pages implementation under `Pages/Chat` or `Pages/Conversation`.

## Flow 3 reporting consumer - COMPLETE

Member 2's Flow 3 reporting work merged through PR #12.

The Reports page now reads persisted indexing data produced by this pipeline, including:

- Uploaded count
- Processing count
- Indexed count
- Failed count
- `IndexedAtUtc`
- failed documents and `IndexError`
- document/chunk totals
- recently indexed documents with chunk counts

Reporting remains read-only and does not enqueue documents or mutate index state.

Post-merge local smoke testing confirmed the completed consumer boundary: a PDF upload progressed through the real worker/Ollama embedding path to `Indexed`, persisted chunks were created, and Flow 3 reflected those values on the dashboard.

No Member 3 indexing change was needed for PR #12.

## Files added/changed by the indexing implementation

Key implementation areas:

```text
src/PRN222.RagAssistant/Infrastructure/Parsing/
src/PRN222.RagAssistant/Infrastructure/Services/DocumentIndexingService.cs
src/PRN222.RagAssistant/Infrastructure/Services/DocumentIndexingWorker.cs
src/PRN222.RagAssistant/Infrastructure/Services/OllamaTextEmbeddingService.cs
src/PRN222.RagAssistant/Infrastructure/Services/TextEmbeddingBatcher.cs
src/PRN222.RagAssistant/Application/Abstractions/ITextEmbeddingService.cs
```

## Validation

PR #9 completed GitHub Actions CI successfully before merge. The merged implementation includes focused parser/chunker tests in addition to the existing architecture, EF, Chapter, and Document Management tests.

PR #12 later reported `75/75` tests passing and demonstrated that the indexing output could be consumed by the completed reporting workflow without changing the pipeline.

Future changes to indexing behavior should preserve coherent re-index replacement, stable metadata, status transitions, and the shared `ITextEmbeddingService` contract unless all affected consumers are updated together.

## Required reading for the next member

```text
AGENTS.md
src/PRN222.RagAssistant/Application/AGENTS.md
docs/project-status.md
docs/team-workflow.md
docs/infrastructure.md
docs/member-2-document-management-handoff.md
docs/flow-3-report-statistics-handoff.md
this file
```
