# RAG Demo - Member 4 End-to-End Guide

## Prerequisites

1. Docker Desktop running
2. `.NET 10.0 SDK` installed
3. `git`

## Step 1: Switch to demo branch

```bash
cd PRN222-RAG-Assistant
git checkout member4-rag-v2
```

## Step 2: Start infrastructure + Ollama

```bash
docker compose --profile local-ai up -d --build
```

Wait for all containers to be healthy:

```bash
docker compose ps
```

Expected output:
```
NAME                STATUS
prn222-app         running
prn222-postgres     running
prn222-ollama       running
```

## Step 3: Pull Ollama models (first time only)

```bash
docker exec prn222-ollama ollama pull qwen3:4b
docker exec prn222-ollama ollama pull qwen3-embedding:0.6b
```

Models are ~2.5GB + ~1GB. This takes a few minutes.

Verify models are available:

```bash
docker exec prn222-ollama ollama list
```

Expected:
```
NAME                      SIZE      MODIFIED
qwen3:4b                  2.5GB     ...
qwen3-embedding:0.6b      1.0GB     ...
```

## Step 4: Verify app is running

Open browser: **http://localhost:8080**

You should see the PRN222 RAG Assistant homepage.

## Step 5: Register a student account

1. Click **Đăng ký** (Register)
2. Fill in email/password
3. Login automatically after registration

## Step 6: Upload a test document

> If no PRN222 subject exists, the demo page auto-creates one.
> For a real test, upload a PDF about PRN222 topics (OOP, C#, .NET).

1. Go to **Tài liệu** (Documents) — requires SubjectLeader role
2. Switch to Admin/SubjectLeader account, or manually create via Razor Page:

**Create subject via SQL (inside postgres container):**

```bash
docker exec -i prn222-postgres psql -U postgres -d prn222_rag <<'EOF'
INSERT INTO "Subjects" ("Id", "Code", "Name", "Description", "IsActive")
VALUES (
    '11111111-1111-1111-1111-111111111111',
    'PRN222',
    'PRN222 - Introduction to Programming',
    'Môn học giới thiệu lập trình C#',
    true
)
ON CONFLICT ("Code") DO NOTHING;
EOF
```

3. Upload a PDF (e.g., a C# tutorial PDF)
4. Wait for status to change from `Processing` → `Indexed`

Check indexing status:

```bash
docker logs prn222-app --tail 20 -f
```

Look for: `Document indexing completed. DocumentId=...`

## Step 7: Demo the RAG Backend

Open: **http://localhost:8080/rag-demo**

### Test 1: With documents indexed

1. Type a question related to the uploaded PDF content
2. Click **Hỏi**
3. Expected:
   - ✅ Answer appears
   - ✅ Citations list shows document sources with page numbers
   - ✅ "Demo Session" created in database

### Test 2: Without documents (no-evidence path)

1. Make sure no documents are `Indexed`
2. Ask any question
3. Expected:
   - ✅ Answer: "Tôi chỉ có thể trả lời dựa trên tài liệu đã được index. Hiện không tìm thấy thông tin phù hợp."
   - ✅ Citations: empty

### Test 3: Verify data persistence

```bash
docker exec -i prn222-postgres psql -U postgres -d prn222_rag <<'EOF'
-- Check chat sessions
SELECT "Id", "Title", "UserId", "CreatedAtUtc" FROM "ChatSessions" LIMIT 5;

-- Check messages
SELECT m."Id", m."ChatSessionId", m."Role", LEFT(m."Content", 80) as "Content"
FROM "ChatMessages" m
ORDER BY m."CreatedAtUtc" DESC LIMIT 10;

-- Check citations
SELECT mc."Id", mc."ChatMessageId", mc."DocumentChunkId", mc."Rank",
       dc."Content" as "ChunkContent"
FROM "MessageCitations" mc
JOIN "DocumentChunks" dc ON dc."Id" = mc."DocumentChunkId"
LIMIT 5;
EOF
```

## What Member 4 Backend Does

```
User question
    │
    ▼
[1] Validate question (not empty)
    │
    ▼
[2] Verify ChatSession belongs to User
    │
    ▼
[3] Persist UserMessage to DB
    │
    ▼
[4] Generate question embedding (ITextEmbeddingService → Ollama)
    │
    ▼
[5] Search pgvector for TopK similar chunks (PgVectorDocumentChunkRetriever)
    │
    ▼
[6] Filter by MinimumSimilarityScore (0.3 default)
    │
    ├── No chunks found ──► Return NoEvidenceMessage
    │
    ▼
[7] Build prompt with context + history (GroundedPromptBuilder)
    │
    ▼
[8] Generate answer (IChatCompletionService → Ollama)
    │
    ▼
[9] Persist AssistantMessage + Citations to DB
    │
    ▼
[10] Auto-set session title from first question
    │
    ▼
Return RagAnswer (Answer + Citations)
```

## Troubleshooting

### Ollama container keeps restarting

```bash
docker logs prn222-ollama
```

If OOM (Out of Memory), increase Docker Desktop memory to 8GB+.

### Models not found after pull

```bash
docker restart prn222-ollama
docker exec prn222-ollama ollama list
```

### App returns 500 error

Check app logs:
```bash
docker logs prn222-app --tail 50
```

Common issues:
- `Connection refused` to Ollama → `Rag__Ollama__BaseUrl` should be `http://ollama:11434` (already set in docker-compose.yml)
- DB migration failed → `docker compose down -v && docker compose --profile local-ai up -d --build`

### Indexing stuck at "Processing"

```bash
docker logs prn222-app | grep -i "index"
```

The `DocumentIndexingWorker` processes queued documents. Check if Ollama is reachable.

## Configuration

Current AI provider: **Ollama** (local, no API key needed)

To switch providers, update environment:

```bash
# Gemini (free tier)
RAG_CHAT_PROVIDER=Gemini
RAG_EMBEDDING_PROVIDER=Gemini
GEMINI_API_KEY=your_key

# OpenRouter (free models)
RAG_CHAT_PROVIDER=OpenRouter
RAG_EMBEDDING_PROVIDER=OpenRouter
OPENROUTER_API_KEY=your_key
```

Restart app after changing provider:
```bash
docker compose up -d --build app
```

## Database Schema

```
ChatSessions ──────< ChatMessages
                          │
                          └──< MessageCitations >────── DocumentChunks >──── Documents
                            (rank, chunkId)           (content, embedding)   (title, indexStatus)
```
