# Flow 2 Chat & Evaluation demo guide

> Replaces the obsolete `RagDemo` guide. `Pages/RagDemo` was removed in PR #35; use the product MVC Chat and Evaluation flows.

## What this demo verifies

- document indexing into pgvector;
- subject-scoped RAG retrieval;
- grounded answer generation and citations;
- MVC chat-session history;
- SSE progress/typewriter rendering;
- citation reader and Markdown UI;
- the 50-question evaluation workflow.

## Local prerequisites

- Docker Desktop / Docker Engine + Compose v2;
- .NET 10 SDK if running commands outside containers;
- repository checked out on the branch/commit you want to test.

Do not switch to an old `member4-rag-v2` branch; this guide targets current `master`.

## Option A: local Ollama

Copy `.env.example` to `.env` if needed and keep:

```env
RAG_PROVIDER=Ollama
RAG_CHAT_PROVIDER=
RAG_EMBEDDING_PROVIDER=
```

Start:

```bash
docker compose --profile local-ai up -d --build
```

If the configured Ollama models are not present, pull them into the Ollama container:

```bash
docker exec prn222-ollama ollama pull qwen3:4b
docker exec prn222-ollama ollama pull qwen3-embedding:0.6b
```

## Option B: cloud/hybrid

Configure the selected providers with server-side API keys, then run:

```bash
docker compose up -d --build
```

For example, the current Render architecture uses Gemini for Chat and OpenRouter for embeddings. Local Compose is configurable and does not have to match Render.

When changing the **embedding** provider/model/dimension, re-index the corpus before treating retrieval as migrated. Chat-only changes do not require re-indexing.

## Verify infrastructure

```bash
docker compose ps
```

Open:

```text
http://localhost:8080
```

The health endpoint is:

```text
http://localhost:8080/healthz
```

## Prepare users and subject

The repository seeds the PRN222 demo subject through the normal database initialization path. Do not rely on a removed RagDemo page to auto-create subjects.

For demo accounts either:

- enable/configure optional seed users through environment variables; or
- register a Student publicly and use an Admin account to manage elevated roles/subject assignments.

Public registration never lets the user select Admin/SubjectLeader.

## Upload and index a document

Use a user authorized to manage the target subject:

1. Open the Subject catalogue.
2. Open Documents for the desired subject.
3. Upload a PDF/DOCX/PPTX.
4. Wait until the document reaches `Indexed`.
5. Optionally open Document details to inspect chunks.

Useful logs:

```bash
docker logs prn222-app --tail 100
```

## Demo the product Chat

Open the MVC Chat flow from navigation or:

```text
http://localhost:8080/Chat
```

Expected behavior:

1. choose an active subject;
2. create/use a subject-aware chat session;
3. ask a question grounded in an indexed document;
4. observe the progress timeline;
5. receive the answer with citation markers/source pills;
6. open a citation to inspect the source excerpt;
7. switch sessions to verify conversation history.

### Transport note

The Chat page uses:

```text
POST /Chat/AskStream
response: text/event-stream
```

JavaScript consumes SSE events from a `fetch` response body. Current event types include:

```text
tool_call
citations
delta
done
error
```

This implementation does **not** use SignalR.

The current RAG call returns a completed answer to the controller, which then emits application-level word deltas for the typewriter experience; do not describe this as provider-native token streaming.

## No-evidence behavior

Ask a question for which indexed documents do not contain sufficient evidence. The backend should follow its configured grounded/no-evidence behavior and should not fabricate citations.

## Contextual follow-up behavior

After a grounded question, try a short follow-up such as asking for the author or intended audience. PR #35 can expand a short follow-up with recent conversation context when the standalone retrieval query returns no useful chunks.

## Demo Evaluation

Open:

```text
http://localhost:8080/Evaluation
```

The UI reads the packaged 50-question dataset. You can run a single question or the full suite. Evaluation resolves an active subject whose code matches the dataset subject code.

## Verify persistence

Useful PostgreSQL checks:

```bash
docker compose exec -T postgres psql -U postgres -d prn222_rag -c \
  'SELECT "Id", "SubjectId", "Title", "CreatedAtUtc" FROM "ChatSessions" ORDER BY "CreatedAtUtc" DESC LIMIT 5;'

docker compose exec -T postgres psql -U postgres -d prn222_rag -c \
  'SELECT "ChatSessionId", "Role", LEFT("Content", 100) FROM "ChatMessages" ORDER BY "CreatedAtUtc" DESC LIMIT 10;'

docker compose exec -T postgres psql -U postgres -d prn222_rag -c \
  'SELECT "ChatMessageId", "DocumentChunkId", "Rank" FROM "MessageCitations" LIMIT 10;'
```

## Troubleshooting

### App returns 500

```bash
docker logs prn222-app --tail 100
```

Check:

- selected cloud provider API key is present;
- PostgreSQL is healthy;
- selected Ollama service is running if Ollama is configured;
- uploaded source file still exists for re-index operations.

### Document stays Processing/Failed

Inspect app logs and confirm the selected embedding provider is reachable. Re-index from the product UI after correcting the provider/configuration issue.

### pgvector dimension issues during a provider migration

PR #37 filters retrieval to vectors with the current query dimension. If many documents disappear from retrieval after a dimension change, they are expected to remain excluded until re-indexed with the active embedding configuration.

### Antiforgery token could not be decrypted after restart

PR #38 persists Data Protection keys in PostgreSQL. Verify migrations have created `DataProtectionKeys` and the application is using the expected database. The old workaround of accepting cookie loss on every Render restart is no longer the intended architecture.

## Safe cleanup

Normal stop:

```bash
docker compose down
```

Do **not** add `-v` unless intentionally deleting PostgreSQL/Ollama volumes and all local persisted data/models.
