# Agentic RAG and real chat streaming

This document describes the interactive Chat retrieval path introduced by PR #49.

## Goals

- Keep the existing deterministic RAG path as a safe fallback and evaluation baseline.
- Let capable chat models decide when and how to retrieve more evidence.
- Combine semantic pgvector retrieval with PostgreSQL full-text search.
- Keep authorization and Subject scope entirely on the server.
- Stream real provider/model output through Razor Pages SSE instead of replaying a completed answer.
- Prefer provider SDKs and `Microsoft.Extensions.AI` abstractions instead of hand-written REST/function-call parsers.

## Interactive Chat flow

```text
Browser
  -> POST /Chat?handler=AskStream
  -> IRagQueryService.AskStreamAsync
  -> validate User + ChatSession + Subject scope
  -> Agentic RAG when provider supports IAgenticChatCompletionService
       -> Gemini IChatClient (Google.GenAI)
       -> Microsoft.Extensions.AI FunctionInvokingChatClient
       -> server-side retrieval tools
            -> search_documents      (pgvector + PostgreSQL FTS + RRF)
            -> keyword_search        (PostgreSQL FTS)
            -> get_chunk_context     (neighbor chunks)
            -> list_documents        (indexed document metadata)
       -> final grounded model response
       -> real model deltas
  -> otherwise deterministic RAG fallback
       -> embedding
       -> pgvector retrieval
       -> grounded prompt
       -> streaming provider when available, otherwise one completed delta
  -> persist completed user/assistant messages and citations
  -> SSE delta/citations/done events
  -> Razor Chat UI
```

## Retrieval tools

### `search_documents`

Default retrieval tool. It runs semantic vector retrieval and PostgreSQL full-text retrieval, then combines their ranks with Reciprocal Rank Fusion (RRF). This improves exact-term lookup without giving up semantic matching.

### `keyword_search`

Use for exact names, codes, years, versions and terminology that may be poorly represented by embedding similarity alone.

### `get_chunk_context`

Given a `chunk_id` returned by a search tool, reads nearby chunks in the same document. This helps questions that depend on a paragraph, section or explanation immediately before/after the first retrieved match.

### `list_documents`

Lists indexed documents inside the current Subject scope and optionally filters by title/file name.

## Subject isolation and tool safety

The model never receives a `subjectId` argument for retrieval tools.

`RagQueryService` resolves the effective Subject from the authenticated user's `ChatSession`, rejects conflicting request values, and creates a scoped tool session. Every tool call uses that captured server-side Subject ID.

Do not replace these tools with generic SQL/database execution tools. The model should select retrieval intent, not authorization scope or arbitrary database queries.

## Grounding behavior

Agentic Chat does not forward model text to the browser until at least one retrieval tool has produced evidence. If a model attempts to answer directly without retrieval, the backend returns the configured `Rag:Chat:NoEvidenceMessage` instead of exposing an ungrounded answer.

Retrieved chunks are assigned stable markers such as `[1]`, `[2]`, and the final answer is expected to place these markers next to supported claims. Citations are persisted only after the response completes successfully.

## Real streaming

Before this change, `OnPostAskStreamAsync` called `IRagQueryService.AskAsync`, waited for the provider to finish the entire response, and then emitted the whole answer as one `delta` event. That was SSE transport, but not model streaming.

The new path is:

```text
Google.GenAI / IChatClient streaming update
  -> IStreamingChatCompletionService or IAgenticChatCompletionService
  -> RagDeltaEvent
  -> Razor Page SSE `delta`
  -> browser appends the delta immediately
```

SSE heartbeat comments are still emitted during long retrieval/tool/first-token waits to prevent idle proxy disconnects. Heartbeats contain no synthetic model content.

If the client disconnects or the request is cancelled before completion, the service does not persist a partial assistant message.

## SDK-first provider implementation

Gemini Chat uses:

- `Google.GenAI` for the official Gemini .NET client and provider streaming implementation.
- `Microsoft.Extensions.AI` for `IChatClient`, `AIFunctionFactory`, tool JSON schemas and function-invocation orchestration.
- `IHttpClientFactory` through `Google.GenAI.Types.ClientOptions.HttpClientFactory` so the SDK participates in the application's configured HTTP pipeline and remains testable.

Provider-specific REST/SSE parsing should not be reimplemented when the provider SDK exposes the required capability.

## Configuration

Agentic RAG is enabled by default when the selected Chat provider implements `IAgenticChatCompletionService`.

```json
{
  "Rag": {
    "Agentic": {
      "Enabled": true,
      "ToolTopK": 6,
      "MaxToolResultChars": 7000
    }
  }
}
```

Environment variable equivalents use normal ASP.NET Core configuration mapping, for example:

```text
Rag__Agentic__Enabled=true
Rag__Agentic__ToolTopK=6
Rag__Agentic__MaxToolResultChars=7000
```

`ToolTopK` is validated between 1 and 12. `MaxToolResultChars` must be at least 1000.

## Fallback and evaluation

Interactive Chat falls back to the previous deterministic pipeline when agentic mode is disabled or the chosen Chat provider does not implement the agentic capability.

`AskStatelessAsync`, used by Evaluation, deliberately remains deterministic so evaluation can continue to provide a stable classic-RAG baseline. This also makes it possible to compare classic RAG against the interactive Agentic RAG behavior without changing the evaluation dataset semantics.
