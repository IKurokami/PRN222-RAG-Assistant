# Member 3 handoff - Document Indexing/Ingestion

> Synchronized with `master` after PR #19. Member 3 now has two completed responsibility tracks: indexing and the cross-app UI/UX redesign documented separately in `member-3-ui-ux-handoff.md`.

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

and queues only `Document.Id`. The indexing service loads the persisted Document and indexes that record. Subject isolation matters later at retrieval time, where Member 4 must restrict candidate Documents/Chunks to the selected Subject.

Do not add `SeedData.Prn222SubjectId` filtering to the indexing worker/service.

## State flow

```text
Uploaded -> Processing -> Indexed
                     \-> Failed
```

Re-index coherently replaces stale chunks. Startup recovery re-enqueues persisted work because the in-memory queue is not durable.

## Relationship to PR #19 UI work

Member 3 also completed the cross-application UI/UX redesign merged in PR #19. That assignment is tracked separately in `docs/member-3-ui-ux-handoff.md` so indexing responsibilities remain easy to identify.

The UI work does not change indexing architecture or subject boundaries.

## Boundaries

Member 3 does not own Subject CRUD/assignments/RBAC, Flow 3 business logic, or Flow 2 retrieval/backend.

Future Flow 2 MVC/history/citation/evaluation ownership remains Member 5 even though Member 5 should reuse Member 3's PR #19 design system.

Any doc/status impact is reported to Member 1 rather than editing repository docs independently.
