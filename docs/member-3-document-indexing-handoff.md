# Member 3 handoff - Document Indexing/Ingestion

## Status

Indexing is complete / merged through PR #9 and remains the single indexing implementation for every Subject.

## Ownership

Member 3 owns:

- PDF/DOCX/PPTX parsing;
- `DocumentParserFactory`;
- `TextChunker`;
- `TextEmbeddingBatcher`;
- `OllamaTextEmbeddingService`;
- `DocumentIndexingService`;
- `DocumentIndexingWorker`;
- coherent DocumentChunk replacement;
- indexing status/error/timestamp persistence;
- startup recovery for persisted Uploaded/Processing documents.

## Multi-subject impact

No per-subject indexing pipeline is needed.

Flow 1 persists:

```text
Document.Id
Document.SubjectId
...
```

and queues only `Document.Id`. The indexing service loads the persisted Document and indexes that record. Subject isolation matters later at **retrieval time**, where Member 4 must restrict candidate Documents/Chunks to the selected Subject.

Do not add `SeedData.Prn222SubjectId` filtering to the indexing worker/service.

## State flow

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-index coherently replaces stale chunks. Startup recovery re-enqueues persisted work because the in-memory queue is not durable.

## Boundaries

Member 3 does not own Subject CRUD/assignments/RBAC, Flow 3 reports, or Flow 2 retrieval/UI.

Any doc/status impact is reported to Member 1 rather than editing repository docs independently.
