# Member 4 - RAG Backend Handoff (2026-08-18)

## Scope

Flow 2 backend: subject-scoped RAG query pipeline.

## What was built

| File | Purpose |
|---|---|
| `Application/Abstractions/IRagQueryService.cs` | Pre-existing interface (Member 1 baseline) |
| `Infrastructure/Rag/InternalTypes.cs` | `IDocumentChunkRetriever`, `RetrievedChunk`, `ChatHistoryEntry` |
| `Infrastructure/Rag/RagOptions.cs` | Tuning knobs via `Rag:` config section |
| `Infrastructure/Rag/PgVectorDocumentChunkRetriever.cs` | pgvector cosine similarity retrieval |
| `Infrastructure/Rag/GroundedPromptBuilder.cs` | System/user prompt composition |
| `Features/Rag/RagQueryService.cs` | Main pipeline: embed → retrieve → complete → persist |
| `Features/Rag/Exceptions/RagException.cs` | Base exception |
| `Features/Rag/Exceptions/ChatSessionNotFoundException.cs` | Session validation error |
| `Infrastructure/ServiceCollectionExtensions.cs` | DI registration for Member 4 services |

## Key design decisions

### Bug fixes vs old implementation

1. **SQL injection fix**: Old code used `$@` interpolated string with `SqlQueryRaw`. New code uses `{0}` positional parameter with `new Vector(embedding)` — safe.
2. **ServiceCollectionExtensions**: Old branch had merge conflict markers (`<<<<<<<`). Clean baseline was used.
3. **Subject scoping**: `ChatSession` on master has no `SubjectId`. Retrieval currently searches ALL indexed documents. This is a known limitation — see "Known limitations" below.

### pgvector retrieval

Uses `Vector <=> query_vector` (cosine distance, `<=>` operator) with `ORDER BY ... LIMIT TopK`. Parameters are passed via `SqlQueryRaw` positional placeholders. Only chunks from documents with `IndexStatus = 'Indexed'` are retrieved.

### Prompt strategy

- System prompt: Vietnamese instruction to answer only from context, cite with `[n]` markers.
- User prompt: Question + Context block (chunks with location) + History block (if enabled).
- No-evidence path: Returns configured `NoEvidenceMessage` with empty citations.

### Configuration (`Rag:` section)

```json
{
  "Rag": {
    "Retrieval": {
      "TopK": 5,
      "MinimumSimilarityScore": 0.3,
      "MaxContextChars": 4000,
      "IncludeConversationHistory": true,
      "HistoryTurns": 5,
      "ExcerptChars": 240
    },
    "Chat": {
      "NoEvidenceMessage": "Tôi chỉ có thể trả lời dựa trên tài liệu đã được index. Hiện không tìm thấy thông tin phù hợp cho câu hỏi này."
    }
  }
}
```

## Known limitations

### 1. No subject scoping in retrieval

`PgVectorDocumentChunkRetriever` currently retrieves from ALL indexed documents. `ChatSession` on master does not have `SubjectId`, so multi-subject filtering cannot be applied at the SQL level.

To fix: Member 1 needs to add `SubjectId` to `ChatSession` (EF migration + schema change). Once `ChatSession.SubjectId` exists:
1. Add `WHERE d."SubjectId" = {subjectId}` to the retrieval SQL.
2. Add `Guid subjectId` parameter to `IRagQueryService.AskAsync`.

### 2. Chat session title auto-generation

Uses single `ExecuteUpdateAsync` to set `Title` from first question. This is an additive feature that does not break existing behavior.

## Tests

- `GroundedPromptBuilderTests`: 9 tests — system prompt, context, history, truncation, location formatting.
- `RagQueryServiceTests`: 5 tests — record properties, similarity score thresholds.

Run: `dotnet test`

## Dependencies

Member 4 code depends on:
- `ITextEmbeddingService` (Member 1 — Ollama/Gemini/OpenAI/OpenRouter)
- `IChatCompletionService` (Member 1 — Ollama/Gemini/OpenAI/OpenRouter)
- `ApplicationDbContext` (Member 1 — PostgreSQL + pgvector)
- `IDocumentIndexingService` (Member 3 — indexing pipeline)
- `Document`, `DocumentChunk`, `ChatSession`, `ChatMessage`, `MessageCitation` entities (Member 1)

## Integration points for Member 5 (Chat UI)

Member 5 receives `RagAnswer`:
```csharp
public sealed record RagAnswer(
    Guid ChatSessionId,
    Guid UserMessageId,
    Guid AssistantMessageId,
    string Answer,
    IReadOnlyList<RagCitation> Citations);
```

`RagCitation`:
```csharp
public sealed record RagCitation(
    Guid DocumentId,
    Guid DocumentChunkId,
    string DocumentTitle,
    int Rank,
    string Excerpt,
    int? PageNumber,
    int? SlideNumber);
```

Member 5 should call `IRagQueryService.AskAsync(userId, chatSessionId, question)` from MVC controllers and render the returned `RagAnswer.Answer` + `RagAnswer.Citations`.
