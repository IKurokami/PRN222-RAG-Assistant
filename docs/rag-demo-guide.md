# Flow 2 Chat, Evaluation & Document realtime demo guide

> Updated on 2026-08-21 after PR #42/#43 and the accepted Razor Pages + SignalR target architecture.
>
> The docs PR defines the final presentation target but does not implement the remaining migrations. During the transition, some non-Chat surfaces can still be served by the legacy runtime until the follow-up code PR lands.

## What this demo verifies

- document indexing into pgvector;
- subject-scoped RAG retrieval;
- grounded answer generation and citations;
- Razor Page Chat session/history behavior;
- SSE progress/typewriter rendering;
- citation reader and Markdown UI;
- the 50-question evaluation workflow;
- after the implementation PR: SignalR synchronization on the Document Management page.

## Local prerequisites

- Docker Desktop / Docker Engine + Compose v2;
- .NET 10 SDK if running commands outside containers;
- repository checked out on the branch/commit you want to test.

## Start local infrastructure

Local Ollama:

```bash
docker compose --profile local-ai up -d --build
```

Cloud/hybrid:

```bash
docker compose up -d --build
```

Open:

```text
http://localhost:8080
```

Health endpoint:

```text
http://localhost:8080/healthz
```

## Prepare users and subject

Use the normal Subject catalogue and Identity/RBAC flows. Public registration creates Student only; elevated roles/subject assignments are Admin-managed.

## Upload and index a document

Use an account authorized to manage the target subject:

1. Open the Subject catalogue.
2. Open Documents for the desired subject.
3. Upload a PDF/DOCX/PPTX.
4. Wait until the document reaches `Indexed`.
5. Optionally inspect Document details/chunks.

Useful logs:

```bash
docker logs prn222-app --tail 100
```

## Document SignalR target test

This section applies after the follow-up implementation PR adds the documented hub/client.

1. Open the same subject's Document Management page in two authenticated browser tabs/windows.
2. Keep both connections on the same authorized Subject.
3. In the first tab, upload/create a Document.
4. Confirm the second tab receives the new Document without manual refresh.
5. Edit the Document and confirm the second tab reflects the change.
6. Delete the Document and confirm the second tab removes/invalidates the row.
7. Trigger indexing/re-indexing and confirm status changes can arrive through `DocumentIndexStatusChanged`.
8. Disconnect/reconnect the network or reload a tab and verify the client reconnects/re-subscribes safely.
9. Verify an unauthorized user cannot subscribe to another Subject's management feed.

Expected transport separation:

```text
Document create/update/delete/index status -> SignalR notifications
Document writes                           -> Razor Page handlers
Chat progress/result                      -> SSE
```

## Demo the product Chat

Open:

```text
http://localhost:8080/Chat
```

Chat is a Razor Page after PR #42. Expected behavior:

1. choose an active subject;
2. create/use a subject-aware chat session;
3. ask a question grounded in an indexed document;
4. observe progress output;
5. receive the answer with citation markers/source pills;
6. open a citation to inspect the source excerpt;
7. switch sessions to verify history.

PR #43 keeps page/session data behind `IChatPageService`.

### Chat transport

Chat continues to use `text/event-stream` with event types such as:

```text
tool_call
citations
delta
done
error
```

This is SSE. Do not expect Chat SignalR events merely because Documents use SignalR in the target architecture.

## Demo Evaluation

The final target is a Razor Page under `Pages/Evaluation` backed by `IEvaluationService`.

During the documentation-only migration period, use the Evaluation URL exposed by the current runtime branch you are testing. The follow-up implementation PR must preserve single-question/full-suite behavior while moving the HTTP presentation to Razor Pages.

## No-evidence/contextual follow-up

- Ask a question unsupported by indexed evidence and verify citations are not fabricated.
- After a grounded question, try a short follow-up; contextual retrieval fallback should preserve the existing behavior.

## Verify persistence

Useful PostgreSQL checks:

```bash
docker compose exec -T postgres psql -U postgres -d prn222_rag -c \
  'SELECT "Id", "SubjectId", "Title", "CreatedAtUtc" FROM "ChatSessions" ORDER BY "CreatedAtUtc" DESC LIMIT 5;'

docker compose exec -T postgres psql -U postgres -d prn222_rag -c \
  'SELECT "ChatSessionId", "Role", LEFT("Content", 100) FROM "ChatMessages" ORDER BY "CreatedAtUtc" DESC LIMIT 10;'
```

SignalR notifications are transient and are not a persistence system; PostgreSQL remains the source of truth.

## Troubleshooting

### App returns 500

Check app logs, provider secrets, PostgreSQL health, selected Ollama service (if configured), and source-file availability.

### Document stays Processing/Failed

Inspect logs and confirm the selected embedding provider is reachable. Re-index after correcting provider/configuration issues.

### SignalR client does not update Documents

After the implementation PR, check:

- the client connected to `/hubs/documents`;
- the authenticated user is authorized for the selected Subject;
- the connection joined the expected subject group;
- the write succeeded before the event was published;
- reconnect logic rejoined the subject after a disconnect;
- no stale browser bundle is being served.

### pgvector dimensions during provider migration

PR #37 filters retrieval to vectors with the current query dimension. Documents with stale dimensions remain excluded until re-indexed.

### Antiforgery token errors after restart

Verify the `DataProtectionKeys` table exists and the application is using the expected PostgreSQL database.

## Safe cleanup

```bash
docker compose down
```

Do **not** add `-v` unless intentionally deleting PostgreSQL/Ollama volumes and local persisted data/models.
