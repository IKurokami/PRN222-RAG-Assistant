# Member 3 handoff - Document Indexing/Ingestion

> Synchronized after PR #37/#40. Member 3 remains the maintenance owner for indexing/ingestion.

## Status

The indexing pipeline is complete and active for every Subject.

```text
Document upload/re-index
 -> IDocumentIndexingQueue
 -> DocumentIndexingWorker
 -> parser (PDF/DOCX/PPTX)
 -> TextChunker
 -> ITextEmbeddingService
 -> replace DocumentChunk rows
 -> Indexed / Failed
```

## Maintenance ownership

Member 3 owns ongoing maintenance of:

- PDF/DOCX/PPTX parsers;
- `DocumentParserFactory`;
- `TextChunker`;
- `TextEmbeddingBatcher`;
- `DocumentIndexingService`;
- `DocumentIndexingWorker`;
- coherent `DocumentChunk` replacement;
- indexing status/error/timestamp persistence;
- startup recovery for persisted Uploaded/Processing documents.

## Contribution accounting

Maintenance ownership is not the same as implementation credit.

- original end-to-end indexing implementation in PR #9: credited in the canonical ledger to Member 1;
- chunk preview/chunking/PDF work in PR #23: credited to Member 1;
- issue #27 remediation in PR #30: credited to Member 4;
- Member 3 retains maintenance ownership of the resulting indexing scope.

See `member-contributions.md`.

## Provider-neutral embedding

Indexing consumes only:

```text
ITextEmbeddingService
```

Concrete provider selection belongs to cross-cutting provider infrastructure. The indexing worker/service must not branch on provider names or create provider-specific indexing pipelines.

## Embedding migration behavior after PR #37

A full corpus re-index is still required when embedding provider/model/dimension changes.

PR #37 changed how a **dimension-changing** migration behaves while it is in progress:

1. new document chunks can be written with the active embedding dimension;
2. older rows with another dimension may temporarily remain;
3. pgvector retrieval filters `vector_dims(Embedding)` to the query dimension before cosine distance;
4. old-dimension rows are excluded rather than causing a dimension exception;
5. each document becomes searchable again as it is re-indexed with the active configuration.

Do not generalize this to semantic compatibility. If two different embedding models both emit 1024-dimensional vectors, `vector_dims` cannot tell them apart. Those vector spaces still must not be intentionally mixed as if they were equivalent.

## Multi-subject behavior

No per-subject indexing worker is required. The queue carries `Document.Id`; the persisted Document carries `SubjectId`. Flow 2 retrieval uses the subject boundary independently.

## Issue #27 baseline

PR #30 closed issue #27 and added/hardened:

- bounded overlap and deterministic forward progress;
- Unicode normalization and safer grapheme boundaries;
- configurable chunking options;
- improved PDF two-column reading order;
- PDF regression tests;
- DOCX page-number correction;
- additional DOCX/PPTX parser/integration coverage.

## Deferred quality debt

- deeper complex DOCX list/table/layout fixtures;
- deeper PPTX grouped-shape/table/transform fixtures;
- additional difficult PDF table/side-note/rotated-text cases.

These are quality follow-ups, not missing core indexing behavior.
