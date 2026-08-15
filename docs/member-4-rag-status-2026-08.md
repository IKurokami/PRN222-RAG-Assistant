# Member 4 - RAG Backend Status (2026-08-15)

> Lightweight status snapshot of the RAG/Chat backend implementation as of
> the current branch (`Member4/Flow-2-backend`). For the full design document,
> see `member-4-rag-backend-handoff.md`.

## Scope

Member 4 owns the Retrieval-Augmented Generation backend: the pipeline that
turns a user question into an answer grounded in stored document chunks for
the selected subject.

## Components delivered in this branch

| Layer | File | Responsibility |
|---|---|---|
| Application | `Application/Features/Rag/RagQueryService.cs` | Orchestrates retrieval, prompt building, completion, persistence. |
| Application | `Application/Models/RagAnswer.cs` and `RagCitation.cs` | Public contract consumed by Member 5 (chat UI). |
| Infrastructure | `Infrastructure/Rag/GroundedPromptBuilder.cs` | Builds the system + user prompt with citations. |
| Infrastructure | `Infrastructure/Rag/PgVectorDocumentChunkRetriever.cs` | Subject-scoped nearest-neighbour search via pgvector. |
| Infrastructure | `Infrastructure/Rag/RagOptions.cs` | Strongly-typed configuration (`Rag:Provider`, retrieval knobs). |
| Infrastructure | `Infrastructure/Rag/Exceptions/*` | Domain exceptions surfaced to the chat pipeline. |
| Tests | `tests/PRN222.RagAssistant.Tests/GroundedPromptBuilderTests.cs` | Prompt builder unit tests. |

## Integration contract

```text
IRagQueryService.AskAsync(Guid userId, Guid chatSessionId, string question, CancellationToken)
    -> RagAnswer { Content, Citations[] }
```

- `Citations` are ranked starting at 1 and carry `DocumentId`, `DocumentTitle`,
  `ChunkId`, `Excerpt` and optional `PageNumber` / `SlideNumber`.
- Subject isolation is enforced server-side: the retriever only considers
  `DocumentChunk` rows whose `Document.SubjectId` matches the active subject.

## Provider-neutral embedding

The pipeline consumes `ITextEmbeddingService`. The current concrete binding
in this branch is `OllamaTextEmbeddingService` (local default).

## Known follow-ups (not blocking demo)

- Ollama HTTP client registration is consolidated into the single DI helper
  under `Infrastructure/ServiceCollectionExtensions.cs`. A small follow-up
  cleanup is pending to drop the duplicated `AddHttpClient("Ollama")` block
  and unify the namespace import (`Infrastructure.Services` vs
  `Infrastructure.Rag`). Tracked in the deviation log of
  `member-4-rag-backend-handoff.md`.
- Two unit tests that require a full InMemory + IdentityDbContext harness
  remain skipped. They cover cancellation paths for `AskAsync`.

## Coordination notes

- No public contract changes for Member 5.
- No changes to the indexing pipeline owned by Member 3.
- No multi-subject rule changes; the retriever still filters by the
  current subject context only.
