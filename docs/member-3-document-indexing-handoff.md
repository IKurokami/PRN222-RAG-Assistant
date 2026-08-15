# Member 3 handoff - Document Indexing/Ingestion

> Updated for provider-neutral AI runtime work. Member 3's indexing ownership remains unchanged.

## Status

Indexing is complete / merged through PR #9 and remains the single indexing implementation for every Subject.

## Ownership

Member 3 owns:

- PDF/DOCX/PPTX parsing;
- `DocumentParserFactory`;
- `TextChunker`;
- `TextEmbeddingBatcher`;
- `DocumentIndexingService`;
- `DocumentIndexingWorker`;
- coherent DocumentChunk replacement;
- indexing status/error/timestamp persistence;
- startup recovery for persisted Uploaded/Processing documents.

The original `OllamaTextEmbeddingService` implementation is now part of a broader provider layer coordinated by Member 1. This does **not** transfer indexing workflow ownership.

## Provider-neutral embedding handoff

Indexing consumes:

```text
ITextEmbeddingService
```

Startup configuration selects one concrete implementation:

```text
OllamaTextEmbeddingService
GeminiTextEmbeddingService
OpenAiTextEmbeddingService
```

Member 3 must not branch the worker/indexing service by provider name and must not create separate provider-specific indexing pipelines.

## Embedding provider change

Default vector dimension is 1024.

A provider/model switch means existing stored embeddings are semantically stale even if dimensions match. Member 1 coordinates the provider change; the existing Member 3 re-index path is then used to rebuild every searchable document.

Never leave a partially mixed corpus of embeddings from different models.

## Multi-subject impact

No per-subject indexing pipeline is needed. Flow 1 queues only `Document.Id`; the persisted document carries `SubjectId`.

Subject isolation matters later at retrieval time, where Member 4 must restrict candidate documents/chunks to the selected Subject.

## State flow

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-index coherently replaces stale chunks. Startup recovery re-enqueues persisted work because the in-memory queue is not durable.

## Other ownership

Member 3 also owns the completed PR #19 cross-app UI/UX redesign. Provider-backup factual copy updates do not transfer that presentation ownership.

Any docs/status impact is reported to Member 1.
