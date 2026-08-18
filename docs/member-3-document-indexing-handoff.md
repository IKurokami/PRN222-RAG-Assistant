# Member 3 handoff - Document Indexing/Ingestion

> Updated after PR #30 merged. Member 3 remains the maintenance owner for this scope.

## Status

The indexing pipeline is complete and active for every Subject.

Current pipeline:

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

Maintenance ownership is **not** the same as implementation credit.

Actual merged contribution is recorded as follows:

- the original end-to-end indexing implementation merged in PR #9 is credited to Member 1;
- the document chunk preview/chunking/PDF improvements merged in PR #23 are credited to Member 1;
- the issue #27 remediation merged in PR #30 is credited to Member 4;
- Member 3 retains ownership of the resulting indexing/ingestion scope going forward.

This prevents double-crediting implementation simply because Member 3 owns the area.

Canonical details: `docs/member-contributions.md`.

## Provider-neutral embedding handoff

Indexing consumes only:

```text
ITextEmbeddingService
```

Concrete provider selection belongs to Member 1 infrastructure.

Supported provider layer includes Ollama, Gemini, OpenAI and OpenRouter. The worker/indexing service must not branch by provider name and must not create provider-specific indexing pipelines.

## Embedding compatibility

The corpus must use one embedding vector space at a time.

If embedding provider/model/dimension changes:

1. treat existing stored embeddings as stale;
2. re-index the entire searchable corpus;
3. do not mix old/new embedding vectors during retrieval.

Changing only chat provider/model/fallback order does not require document re-indexing.

## Multi-subject behavior

No per-subject indexing worker is required.

Flow 1 queues only `Document.Id`. The persisted document carries `SubjectId`, while Flow 2 retrieval uses subject context to constrain candidate documents/chunks.

## Issue #27 merged changes

PR #30 closed issue #27 and added/hardened:

- bounded overlap and deterministic forward progress;
- Unicode normalization and safer grapheme boundaries;
- configurable `ChunkingOptions` with startup validation;
- improved PDF two-column reading order and PDF regression tests;
- DOCX blank paragraphs no longer become fake pages/page numbers;
- additional DOCX/PPTX extraction and integration coverage.

PDF is the primary real-world ingestion format currently receiving the most active testing.

## Deferred follow-up debt

Later focused work should add deeper coverage for:

- complex DOCX list/table/layout combinations;
- PPTX grouped shapes, tables and parent-group transform handling;
- more difficult PDF tables, side notes and rotated text.

These are follow-up quality improvements and are not blockers for the completed PR #30 milestone.

## Other Member 3 ownership

Member 3 also owns the completed cross-application UI/UX baseline from PR #19.

Any documentation/status impact is reported to Member 1 for repository-wide synchronization.

Project documentation uses Member numbers only and must not add GitHub usernames.
