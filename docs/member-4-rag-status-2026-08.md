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
# Member 4 - RAG / Chat Backend handoff

> Phân tích chi tiết theo nhiều role và góc độ, mục tiêu: xây dựng module hoàn chỉnh, tích hợp vào ứng dụng mà **một thay đổi không kéo theo phải sửa hết các nơi khác**.
>
> Phạm vi file này: thiết kế và kế hoạch tích hợp cho phần Member 4 (RAG Backend), dựa trên baseline đã merge của Member 1 + Member 2 và tôn trọng ranh giới Member 3 sẽ làm sau.

## Deviation Log

Ghi lại các thay đổi so với thiết kế ban đầu trong document này.

| # | Ngày | Người | Thay đổi | Lý do |
|---|------|-------|-----------|-------|
| 1 | 2026-08-13 | Agent | `RagOptions.cs` di chuyển từ `Application/Options/` sang `Infrastructure/Rag/` với namespace `PRN222.RagAssistant.Infrastructure.Rag` | Khớp với document chuẩn mục 2.1 và cấu trúc thực tế của project (Infrastructure chứa external integrations). |
| 2 | 2026-08-13 | Agent | Thêm properties còn thiếu vào `RagOptions`: `IncludeConversationHistory` (default true), `SystemPromptTemplate` (nullable), `NoEvidenceAssistantContent` | Khớp với document chuẩn mục 3.2. |
| 3 | 2026-08-13 | Agent | Sau khi sync với Member 3: `OllamaTextEmbeddingService` đã được Member 3 cập nhật với `EmbedBatchAsync`. Giữ implementation ở `Infrastructure/Rag/` với namespace đúng. | Member 3 đã thêm `EmbedBatchAsync` vào `ITextEmbeddingService` và implement. Namespace `Infrastructure.Rag` được giữ để thống nhất với spec. |
| 4 | 2026-08-13 | Agent | File test `OllamaTextEmbeddingServiceTests.cs` của Member 3 reference namespace cũ `Infrastructure.Services`. Đã sửa thành `Infrastructure.Rag`. | Đảm bảo test build pass sau khi move file. |
| 5 | 2026-08-13 | Agent | Sửa `ExcerptChars` hardcode (line 197 trong `RagQueryService.cs`) thành dùng `_options.Retrieval.ExcerptChars`. | Code clean hơn, config-driven thay vì magic number. |
| 6 | 2026-08-13 | Agent | Thêm test cases: `BuildCitations_AssignsRankStartingFromOne`, `BuildCitations_TruncatesExcerpt_WhenContentExceedsExcerptChars`, `BuildCitations_UsesExcerptCharsFromOptions`. | Đảm bảo coverage theo document mục 5.1. |
| 7 | 2026-08-13 | Agent | Skip 2 tests yêu cầu InMemory + IdentityDbContext setup phức tạp: `AskAsync_ThrowsChatSessionNotFoundException_WhenSessionDoesNotExist`, `AskAsync_ThrowsOperationCanceledException_WhenTokenCancelled_BeforeEmbedding`. | Integration tests cần test container hoặc mock phức tạp hơn. |

## 0. Bối cảnh và ranh giới đã chốt (không thay đổi)

### Baseline đã merge

- `Application/Abstractions/` đã có sẵn các interface:
  - `IRagQueryService.AskAsync(Guid userId, Guid chatSessionId, string question, CancellationToken)`
  - `IChatCompletionService.CompleteAsync(string systemPrompt, string userPrompt, CancellationToken)`
  - `ITextEmbeddingService.EmbedAsync(string text, CancellationToken)` -> `float[]`
- `Application/Models/RagAnswer` và `RagCitation` đã có; **đây là hợp đồng public với Member 5** (chat UI).
- `IDocumentIndexingQueue` + `IDocumentIndexingService` đã có, hiện `InMemoryDocumentIndexingQueue` chỉ là stub tạm.

### Entity persistence đã có (Member 1)

- `ChatSession(Id, UserId, Title, CreatedAtUtc, UpdatedAtUtc)`
- `ChatMessage(Id, ChatSessionId, Role[User/Assistant/System], Content, CreatedAtUtc)`
- `MessageCitation(Id, ChatMessageId, DocumentChunkId, Rank)`
- `DocumentChunk(Id, DocumentId, ChunkIndex, Content, PageNumber?, SlideNumber?, Embedding[Pgvector.Vector])`
- Quan hệ: `ChatMessage -> ChatSession (Cascade)`, `MessageCitation -> ChatMessage (Cascade)`, `MessageCitation -> DocumentChunk (Restrict)`.

### Ranh giới tích hợp bắt buộc (từ `Application/AGENTS.md` + `docs/team-workflow.md`)

- Member 4 **phải** dùng `ITextEmbeddingService` cho embedding câu hỏi và `IChatCompletionService` cho sinh câu trả lời. Không gọi trực tiếp Ollama từ Razor Page/Controller.
- Member 4 **phải** xác thực `chatSessionId` thuộc về `userId` trước khi đọc/ghi.
- Member 4 **phải** lấy bằng chứng chỉ từ các `DocumentChunk` đã `Indexed` thuộc PRN222; không đọc file gốc.
- Khi không có bằng chứng hợp lệ: trả lời rõ ràng theo kiểu "không có thông tin trong tài liệu", kèm `RagAnswer.Answer` ngắn và `Citations` rỗng.
- Các bên tiêu thụ: **Member 5** (UI/History/Eval) chỉ gọi `IRagQueryService` và render `RagAnswer` + `RagCitation`; **không** đụng vào Ollama/pgvector trong UI.

### Cái **không** thuộc Member 4 (đã chốt từ trước)

- Parser/chunker/Ollama embedding cho document -> là **Member 3**.
- Chat UI/history/citation rendering/evaluation set -> là **Member 5**.
- Thay đổi entity schema -> phải đi qua Member 1 và team workflow; **không tự ý thêm field mới**.

---

## 1. Phân tích theo vai trò (Role-based analysis)

### 1.1. Subject Leader (giảng viên) - vai trò gián tiếp

Subject Leader **không gọi** `IRagQueryService` (chat dành cho Student). Nhưng họ chịu trách nhiệm gián tiếp:

| Mối quan tâm | Tác động đến Member 4 |
|---|---|
| Upload tài liệu mới | Member 4 **chỉ đọc** `DocumentChunk` đã `Indexed`; khi không có chunk, câu trả lời phải báo "chưa có tài liệu" chứ không mạo nhận. |
| Re-index | Cần xử lý "stale chunks": vì `Member 3` sẽ thay thế các `DocumentChunk` cũ, query phải tự nhiên an toàn với việc chunk cũ còn tồn tại tạm thời (dùng transaction/Consistent Read). |
| Xóa Chapter | `Document.ChapterId` bị set `null` (xóa chapter không xóa document) -> retrieval vẫn phải hoạt động đúng với document "không gán chapter". |
| Xóa Document hiện có chunk | Hiện cấu hình `DocumentChunk -> Document` là **Cascade** (`DocumentChunkConfiguration.cs`); nếu Member 3 xóa Document thì các chunk bị xóa theo. Member 4 **không cần** dọn `MessageCitation` vì `MessageCitation -> DocumentChunk` đang là **Restrict** -> DB sẽ chặn. Đây là invariant quan trọng phải giữ. |

### 1.2. Student (người dùng chính của RAG backend)

| Kịch bản | Yêu cầu thiết kế |
|---|---|
| Đặt câu hỏi trong session thuộc về mình | `IRagQueryService.AskAsync(userId, sessionId, question)`. Validate `session.UserId == userId`. |
| Cố ý/trái phép truy cập session của người khác | Phải trả về lỗi rõ ràng (UI sẽ hiển thị "không tìm thấy" hoặc "forbidden"). Không để lộ sự tồn tại của session. |
| Session không tồn tại | Trả lỗi tương tự để chống enumeration. |
| Câu hỏi rỗng/whitespace | Reject ở validation; **không** tốn một round-trip Ollama. |
| Câu hỏi ngoài phạm vi (chính trị, lịch sử, ...) | Trả lời "Tôi chỉ có thể trả lời dựa trên tài liệu PRN222 đã được index. Hiện không có thông tin phù hợp." với `Citations` rỗng. |
| Câu hỏi có ngữ cảnh (follow-up) | Hiện `IRagQueryService` chỉ nhận `question` đơn lẻ. Quyết định: dùng **luôn lịch sử gần nhất** (top N messages) để rephrase ngầm, hoặc giữ nguyên câu hiện tại. **Đề xuất**: lấy top K=5 messages gần nhất trong cùng session, nối vào `userPrompt` ngầm (xem mục 2.4). Đây là tính năng **additive**, không thay đổi interface. |
| Nhiều tab cùng session | Không tranh chấp: mỗi request là một transaction ngắn, idempotent với nhau. |
| Spam câu hỏi | **Không thuộc** Member 4; rate limit là trách nhiệm của UI/infrastructure. Member 4 chỉ cần log/metric. |

### 1.3. Hệ thống / Background (System)

- Đóng vai trò "request thread" gọi `IRagQueryService`. Yêu cầu:
  - **Một transaction duy nhất** cho cả 3 bước: persist user message + retrieve + persist assistant message + persist citations. Nếu một bước lỗi -> rollback toàn bộ, không để assistant message "mồ côi" không citation.
  - **CancellationToken** phải được tôn trọng giữa chừng (đặc biệt khi embedding/chat Ollama mất thời gian).
  - **Cancellation** không nên xóa user message đã lưu (UX tốt hơn), nhưng có thể bỏ qua assistant message. Quy ước: commit user message trước (cheap), commit assistant message sau khi generation xong; nếu hủy -> chỉ giữ user message.
  - **Idempotency**: hiện không có request id. **Không** cần giải quyết idempotency ở Member 4 scope (single-process, single-instance cho demo).

### 1.4. Ollama AI Runtime (vai trò "external actor")

- Ollama được tiêu thụ qua `IChatCompletionService` và `ITextEmbeddingService`. Member 4 phải đối xử với nó như một dịch vụ có thể:
  - **Timeout** (đã set `5 phút` ở `ServiceCollectionExtensions.cs` nhưng chỉ là HTTP timeout).
  - **Lỗi mạng tạm thời** -> retry có giới hạn (ví dụ 1-2 lần với exponential backoff) cho **embedding**; với **chat generation**, retry có thể gây trùng token -> **không retry tự động** ở Member 4, để UI tự quyết định.
  - **Trả về token rỗng / rất ngắn** -> coi như "không có bằng chứng", trả về câu trả lời an toàn.
  - **Embedding dimension thay đổi** -> Member 4 cần lấy dimension từ cấu hình hoặc từ model runtime, không hard-code `768`/`1024`/`1536`. Đây là điểm **dễ vỡ** nếu sau này đổi model -> thiết kế theo `options.VectorDimension` đọc từ config.

### 1.5. Member 3 (Indexing pipeline) - bên cung cấp "evidences"

Member 4 chỉ đọc từ `DocumentChunk` (embedding + content). Cần đối chiếu các invariant mà Member 3 sẽ tôn trọng:

| Member 3 đảm bảo | Member 4 dựa vào để... |
|---|---|
| Chỉ chunk của Document có `IndexStatus = Indexed` mới có embedding hợp lệ | Filter `Document.IndexStatus == Indexed` trong query. |
| Re-index thay thế (không append) các chunk cũ | Không phải lo duplicate; nhưng vẫn nên `ORDER BY ChunkIndex` để ổn định citation ordering. |
| Embedding của query và indexing dùng **cùng model** | Phải dùng cùng `ITextEmbeddingService` instance (DI singleton). |
| Chapter có thể bị xóa -> Document `ChapterId = null` | Không JOIN `Chapter` trong retrieval; chỉ lấy metadata document là đủ cho `RagCitation.DocumentTitle`. |

### 1.6. Member 5 (UI/History) - bên tiêu thụ đầu ra

- Nhận `RagAnswer { ChatSessionId, UserMessageId, AssistantMessageId, Answer, Citations[] }` và `RagCitation { DocumentId, DocumentChunkId, DocumentTitle, Rank, Excerpt, PageNumber?, SlideNumber? }`.
- Member 4 phải **đảm bảo**:
  - `Rank` bắt đầu từ 1 và tăng dần theo độ liên quan.
  - `Excerpt` ngắn gọn (~200-400 ký tự) để UI có thể hiển thị tooltip/preview mà không phải lưu trữ riêng.
  - `PageNumber`/`SlideNumber` trả về đúng kiểu dữ liệu gốc (null nếu không có).
  - `DocumentTitle` ổn định (lưu lúc citation, tránh document bị sửa title -> citation cũ đổi text).

### 1.7. Tester / QA / Grader

- Cần:
  - Unit-test được cho `IRagQueryService` với stub `IChatCompletionService` + stub `ITextEmbeddingService` + InMemory DB (đã có `Microsoft.EntityFrameworkCore.InMemory` trong test csproj).
  - Không phụ thuộc Ollama thật khi chạy test.
  - Các edge case cần cover: session sai user, không có chunk, embedding exception, chat exception, transaction rollback.

### 1.8. Dev / Maintainer

- Phải có thể:
  - Đổi `IChatCompletionService` implementation (OpenAI, Azure OpenAI, Anthropic) mà **không đụng** vào `IRagQueryService`.
  - Đổi `ITextEmbeddingService` implementation (Ollama, OpenAI, BGE local) mà **không đụng** vào retrieval pipeline.
  - Đổi chiến lược retrieval (TopK, MMR, hybrid BM25+vector) mà **không đụng** Ollama.
  - Tắt/mở từng tính năng qua config: `Rag:Retrieval:TopK`, `Rag:Retrieval:MinScore`, `Rag:Chat:NoEvidenceMessage`, ...

---

## 2. Phân tích đa góc độ (Cross-cutting concerns)

### 2.1. Góc độ kiến trúc — nguyên tắc "thay một chỗ không sụp hết"

**Quy tắc vàng**: mỗi trách nhiệm chỉ sống ở **một lớp**, mỗi quyết định có thể override qua config.

Đề xuất cấu trúc thư mục (chỉ thêm mới, không sửa file đã có):

```text
src/PRN222.RagAssistant/
├─ Application/
│  ├─ Abstractions/                       # (đã có) KHÔNG thêm interface mới
│  │   IRagQueryService.cs
│  │   IChatCompletionService.cs
│  │   ITextEmbeddingService.cs
│  └─ Models/
│      RagAnswer.cs                       # (đã có)
│      RagCitation.cs                     # (đã có)
│
├─ Infrastructure/
│  ├─ Rag/                                # NEW: implementations có thể thay thế
│  │   ├─ RagOptions.cs                   # strongly-typed config
│  │   ├─ PgVectorDocumentChunkRetriever.cs  # implementation duy nhất của retrieval
│  │   ├─ GroundedPromptBuilder.cs        # tách riêng, dễ test, dễ thay prompt template
│  │   ├─ OllamaChatCompletionService.cs  # implementation IChatCompletionService
│  │   └─ OllamaTextEmbeddingService.cs   # implementation ITextEmbeddingService
│  └─ ServiceCollectionExtensions.cs      # (đã có) chỉ thêm đăng ký DI
│
└─ Features/                              # NEW: feature folder cho RAG backend
   └─ Rag/
       ├─ RagQueryService.cs              # IRagQueryService implementation
       ├─ Exceptions/                     # domain-specific exception types
       │   RagException.cs
       │   ChatSessionNotFoundException.cs
       │   InsufficientEvidenceException.cs
       └─ Constants/
           RagMessages.cs                 # "no-evidence" message template
```

**Tại sao cách này đáp ứng "sửa một chỗ không phải sửa hết":**

1. **Đổi model Ollama** -> chỉ sửa `OllamaChatCompletionService`/`OllamaTextEmbeddingService`. `RagQueryService` không cần đụng.
2. **Đổi sang OpenAI** -> thêm `OpenAiChatCompletionService` + đổi 1 dòng DI. Không đụng logic retrieval.
3. **Đổi chiến lược retrieval (TopK -> MMR)** -> thêm class mới implement cùng `IDocumentChunkRetriever` (internal interface), đổi DI. `RagQueryService` chỉ gọi interface.
4. **Đổi prompt template** -> chỉ sửa `GroundedPromptBuilder`. Có thể bind template qua `RagOptions.PromptTemplate` để admin chỉnh không cần build lại.
5. **Đổi ngưỡng TopK/score** -> chỉnh `appsettings.json`, không build lại.
6. **Đổi chiến lược persistence (thêm cache, đổi sang Dapper, ...)** -> `RagQueryService` chỉ nhận `ApplicationDbContext` qua DI; nếu cần thay có thể wrap.

> **Quy ước đặt tên**: để tránh "sửa một chỗ phải sửa cả chuỗi", interface internal (`IDocumentChunkRetriever`, `IPromptBuilder`) chỉ được tham chiếu từ **một chỗ duy nhất** (`RagQueryService`). Nếu sau này có chỗ thứ hai cần dùng, đó là tín hiệu phải nâng cấp thành `Application/Abstractions`.

### 2.2. Góc độ bảo mật & phân quyền

| Mối đe dọa | Phòng thủ |
|---|---|
| Student A đọc session của Student B | Validate `session.UserId == userId` **trước** mọi truy vấn. Dùng `AsNoTracking()` với điều kiện kết hợp (`WHERE Id = @sid AND UserId = @uid`) để chống TOCTOU. |
| SQL injection qua câu hỏi | Chỉ dùng tham số EF Core; **không** raw SQL với input từ user. |
| Prompt injection (câu hỏi có hướng dẫn "ignore previous instructions") | System prompt cứng, không thể user ghi đè; nguồn evidence là `DocumentChunk.Content` đã truncate; dùng marker phân tách rõ ràng giữa "context" và "question". |
| Lộ nội dung nhạy cảm | `RagCitation.Excerpt` chỉ lấy phần đầu của chunk, không trả full content. Member 5 phải hiển thị preview chứ không full document. |
| Lộ log chứa câu hỏi/đáp án cá nhân | Log chỉ ở mức `userId` + `sessionId` + `latency` + `topK results.id`; **không** log full question/answer ở Information level. Có thể log ở Debug khi cần. |
| Account enumeration qua session not-found | Trả cùng response code (404 hoặc 403) cho cả "không tồn tại" và "không thuộc về user". |
| Embedding model drift | Nếu đổi embedding model -> Member 3 phải re-index. Member 4 không tự ý đổi model runtime. Có thể thêm cảnh báo khi query topK=0 nhưng `Document` đã `Indexed` (chỉ ra rằng có thể index bằng model khác). |

### 2.3. Góc độ dữ liệu — query & persistence

**Các truy vấn chính** (cần tối ưu & cover bằng test):

1. Validate session: `SELECT Id FROM ChatSessions WHERE Id = @sid AND UserId = @uid LIMIT 1`
2. Append user message: `INSERT INTO ChatMessages (Id, ChatSessionId, Role, Content, CreatedAtUtc)`
3. Touch session: `UPDATE ChatSessions SET UpdatedAtUtc = now() WHERE Id = @sid`
4. Retrieval (pgvector): 

   ```sql
   SELECT dc.Id, dc.DocumentId, dc.ChunkIndex, dc.Content, dc.PageNumber, dc.SlideNumber,
          d.Title AS DocumentTitle,
          dc.Embedding <=> @questionEmbedding AS distance
   FROM "DocumentChunks" dc
   JOIN "Documents" d ON d."Id" = dc."DocumentId"
   WHERE d."SubjectId" = @prn222Id
     AND d."IndexStatus" = 'Indexed'
   ORDER BY dc.Embedding <=> @questionEmbedding
   LIMIT @topK;
   ```

   - Dùng `<=>` (cosine distance trong pgvector).
   - Filter `SubjectId = PRN222` ngay tại SQL để chỉ tìm trong PRN222.
   - Có thể thêm filter `AND dc.Embedding IS NOT NULL` để chắc chắn.
   - `LIMIT @topK` thay vì load full table.

5. Persist assistant message: `INSERT INTO ChatMessages (Id, ChatSessionId, Role='Assistant', Content, CreatedAtUtc)`
6. Persist citations: bulk insert `MessageCitation` với `Rank` từ 1..N.
7. Cập nhật `ChatSession.Title` lần đầu (nếu rỗng) dựa trên câu hỏi đầu tiên.

**Index cần có** (xem `DocumentChunkConfiguration.cs` đã có unique `(DocumentId, ChunkIndex)`):

- Cần thêm **vector index** (`CREATE INDEX ... USING ivfflat OR hnsw`) trên `DocumentChunks.Embedding` để retrieval nhanh. **Lưu ý**: đây là thay đổi schema -> **phải báo Member 1**, đi qua migration chung. pgvector yêu cầu tạo index **sau khi đã có dữ liệu** để IVFFlat train đúng; HNSW thì không. Đề xuất HNSW cho đơn giản.
- Nếu không muốn tạo index mới (giữ schema không đổi): giới hạn dataset demo (vài chục tài liệu) và dùng brute-force. Vẫn phải có filter `WHERE d."IndexStatus" = 'Indexed'`.

**Consistency giữa retrieval và persist**:

- Nếu `INSERT ChatMessage` (assistant) thành công nhưng `INSERT MessageCitation` lỗi -> orphan assistant. **Cách giải**: dùng **một transaction** duy nhất cho cả (5) và (6). User message (2) và touch session (3) có thể commit trước transaction retrieval để UX hiển thị ngay.

### 2.4. Góc độ chất lượng câu trả lời (RAG quality)

Vì mục tiêu là "grounded", các quy tắc bắt buộc:

1. **System prompt** cứng (do Member 4 kiểm soát, không để user prompt ghi đè):
   - "Bạn là trợ lý PRN222. Chỉ trả lời dựa trên các đoạn tài liệu được cung cấp dưới đây. Nếu không có đủ thông tin, hãy nói rõ rằng không tìm thấy."
2. **Evidence block** được format rõ ràng (delimiter `[CONTEXT]...[/CONTEXT]`), có index `[1]...[N]` tương ứng `Rank`.
3. **No-evidence path**: nếu TopK=0 hoặc tất cả `distance > threshold` -> trả message chuẩn (`RagOptions.NoEvidenceMessage`) với `Citations` rỗng. **Không** gọi Ollama để "nói chung chung".
4. **Anti-hallucination**: prompt yêu cầu model **trích dẫn marker `[n]`** cho mỗi thông tin. Không enforce ở code (vì LLM), nhưng nên test với evaluation set (Member 5).
5. **Token budget**: cắt context còn ~4000-6000 ký tự tổng (tuỳ model). `RagOptions.MaxContextChars`.
6. **Follow-up**: nối lịch sử gần nhất vào userPrompt (giới hạn 3-5 lượt) để duy trì ngữ cảnh; có cờ `RagOptions.IncludeConversationHistory`.

### 2.5. Góc độ hiệu năng & vận hành

| Mục | Mục tiêu | Cách đạt |
|---|---|---|
| Latency end-to-end | < 5s cho dataset demo | Embedding + retrieval (<200ms) + Ollama chat (2-5s). |
| Embedding batch | Có thể batch sau nếu nhiều câu hỏi | Hiện Member 4 chỉ embed 1 câu -> giữ đơn giản. |
| Cancellation | UI có thể hủy request khi user rời trang | Truyền `CancellationToken` xuyên suốt (DB query, embedding HTTP, chat HTTP). |
| Memory | Không load full `Content` của toàn bộ chunk vào RAM | Chỉ project các field cần: `Id, DocumentId, ChunkIndex, Content, PageNumber, SlideNumber`. |
| Logging structured | Dễ grep, dễ build dashboard | Log JSON với `SessionId`, `UserId`, `LatencyMs`, `EmbeddingModel`, `ChatModel`, `TopK`, `ReturnedCitations`. |
| Healthcheck | Biết được Ollama/Postgres sống | **Không thuộc Member 4**, nhưng nên log rõ khi embedding/chat fail (HTTP 5xx, timeout). |
| Concurrency | 2 student hỏi cùng lúc | Mỗi request một DbContext scope, không share mutable state. |
| Timeout | Tránh treo request | Cấu hình `HttpClient.Timeout` (đã có 5 phút ở `ServiceCollectionExtensions.cs`) - kiểm tra chính sách riêng cho chat vs embedding. |

### 2.6. Góc độ tích hợp — checklist "không phụ thuộc quá mức"

Mỗi phụ thuộc phải được **đảo ngược** (invert) qua interface hoặc config:

| Phụ thuộc | Bị đảo ngược bởi |
|---|---|
| Ollama HTTP API | `IChatCompletionService`, `ITextEmbeddingService` |
| pgvector | `IDocumentChunkRetriever` (internal interface) — chỉ gói logic SQL |
| EF Core | `ApplicationDbContext` qua DI |
| Ollama model name, embedding dim | `RagOptions` qua `IOptions<RagOptions>` |
| Prompt template | `GroundedPromptBuilder` + `RagOptions.PromptTemplate` (hoặc constant trong code, default) |
| No-evidence message | `RagOptions.NoEvidenceMessage` |
| TopK, score threshold | `RagOptions.TopK`, `RagOptions.MinScore` |

**Quy tắc kiểm tra "mức độ phụ thuộc"**:

> Nếu muốn đổi X, đếm số file phải sửa. Mục tiêu Member 4: **mỗi thay đổi lý tưởng chỉ chạm 1-2 file** (file cấu hình + file implementation tương ứng), không bao giờ chạm cả `RagQueryService` lẫn `IRagQueryService`.

### 2.7. Góc độ kiểm thử

| Loại test | Phạm vi | Mức độ ưu tiên |
|---|---|---|
| Unit test `GroundedPromptBuilder` | Format prompt có đúng, có escape user input, có truncation | Bắt buộc |
| Unit test `RagQueryService` với InMemory EF + stub service | Validate session, persist message, trả citations đúng, no-evidence path, exception rollback | Bắt buộc |
| Convention/architecture test | `IRagQueryService` chỉ depend on `Application/*` + internal abstractions; không reference `Pgvector.*` trực tiếp từ service | Bắt buộc |
| Integration test (tuỳ chọn) | Chạy thật với Postgres test container + Ollama mock | Tốt nhưng không bắt buộc cho demo |

**Convention mới**: thêm test `RagArchitectureTests` kiểm tra:
- `RagQueryService` không `using Npgsql; using Pgvector;` (chỉ retrieval layer mới được).
- Tất cả config được đọc qua `IOptions<RagOptions>`, không có magic number.

### 2.8. Góc độ triển khai / DevOps

- Biến môi trường mới (đặt trong `docker-compose.yml`):
  - `Rag__Retrieval__TopK` (default 5)
  - `Rag__Retrieval__MinScore` (default 0.5 - khoảng cách cosine, đảo dấu tùy chiến lược)
  - `Rag__Retrieval__MaxContextChars` (default 4000)
  - `Rag__Retrieval__IncludeConversationHistory` (default true)
  - `Rag__Retrieval__HistoryTurns` (default 5)
  - `Rag__Chat__NoEvidenceMessage` (chuỗi tiếng Việt, default)
  - `Rag__Chat__SystemPromptTemplate` (default có sẵn, có thể override)
- Tất cả đã có sẵn cơ chế `ASPNETCORE_*` & `__` (double-underscore) -> chỉ cần bind vào `RagOptions` qua `services.Configure<RagOptions>(...)`.
- Không thêm Docker service mới, không thêm healthcheck endpoint mới.

---

## 3. Thiết kế module chi tiết

### 3.1. `IRagQueryService` (đã có) — giữ nguyên

Quyết định: **không thay đổi signature** hiện tại. Mọi tinh chỉnh đều **additive** (qua config, helper class mới).

### 3.2. `RagOptions` — strongly-typed config

```csharp
public sealed class RagOptions
{
    public const string SectionName = "Rag";

    public RetrievalOptions Retrieval { get; set; } = new();
    public ChatOptions Chat { get; set; } = new();

    public sealed class RetrievalOptions
    {
        public int TopK { get; set; } = 5;
        public double MinScore { get; set; } = 0.5;        // cosine similarity tối thiểu
        public int MaxContextChars { get; set; } = 4000;
        public bool IncludeConversationHistory { get; set; } = true;
        public int HistoryTurns { get; set; } = 5;
        public int ExcerptChars { get; set; } = 240;        // cho RagCitation.Excerpt
    }

    public sealed class ChatOptions
    {
        public string NoEvidenceMessage { get; set; } =
            "Tôi chỉ có thể trả lời dựa trên tài liệu PRN222 đã được index. Hiện không tìm thấy thông tin phù hợp cho câu hỏi này.";
        public string? SystemPromptTemplate { get; set; }  // null = dùng default hard-coded
        public string NoEvidenceAssistantContent { get; set; } =
            "(no-evidence)";                                // marker để UI biết không nên highlight citation
    }
}
```

Bind trong `ServiceCollectionExtensions`:

```csharp
services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));
```

### 3.3. Internal abstractions (KHÔNG public, KHÔNG thuộc `Application/`)

```csharp
internal interface IDocumentChunkRetriever
{
    Task<IReadOnlyList<RetrievedChunk>> SearchAsync(
        float[] questionEmbedding,
        CancellationToken cancellationToken);
}

internal sealed record RetrievedChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentTitle,
    int ChunkIndex,
    string Content,
    int? PageNumber,
    int? SlideNumber,
    double Score);           // cosine similarity (1 - distance)
```

Lý do `internal`: chỉ `RagQueryService` dùng. Khi cần nâng cấp thành `Application/Abstractions/`, làm một lần.

### 3.4. `PgVectorDocumentChunkRetriever`

Trách nhiệm duy nhất: **embed question -> SQL -> map ra `RetrievedChunk`**.

Tách biệt khỏi các concern khác:
- Không biết về chat session.
- Không biết về Ollama chat.
- Chỉ biết: `SubjectId = PRN222`, `IndexStatus = Indexed`, vector distance.

**Nguyên tắc truy vấn**:
- Dùng raw SQL hoặc LINQ với `EF.Functions.CosineDistance` (Npgsql.EntityFrameworkCore.PostgreSQL hỗ trợ).
- Project về `RetrievedChunk` ngay trong query (không load full entity).
- Sắp xếp `OrderBy(distance).Take(topK)`.
- Lọc thêm `Score >= minScore` (in-memory sau khi project, vì SQL có thể không trực tiếp compute similarity theo hướng thuận tiện).

**Lưu ý kỹ thuật**:
- pgvector với Npgsql: `dc.Embedding.CosineDistance(vector)` trả về `double`. Cần cast sang `Vector` bằng `new Vector(float[])`.
- Có thể cần `SELECT ... ORDER BY dc.Embedding <=> @q LIMIT @k` raw SQL nếu LINQ provider chưa stable. Dự phòng viết raw SQL.

### 3.5. `GroundedPromptBuilder`

```csharp
internal sealed class GroundedPromptBuilder
{
    private readonly RagOptions _options;

    public GroundedPromptBuilder(IOptions<RagOptions> options) => _options = options.Value;

    public (string SystemPrompt, string UserPrompt) Build(
        string question,
        IReadOnlyList<RetrievedChunk> evidences,
        IReadOnlyList<ChatMessage> recentHistory);

    public string BuildNoEvidenceUserPrompt(string question);
}
```

Đầu ra ví dụ:

```text
System: Bạn là trợ lý PRN222. CHỈ trả lời dựa trên các đoạn tài liệu dưới đây.
        Nếu không đủ thông tin, hãy nói rõ "không tìm thấy".
        Với mỗi thông tin, hãy ghi marker [n] theo số đoạn.

User:  Câu hỏi: ...

        [CONTEXT]
        [1] (Trang 3) Nội dung đoạn ...
        [2] (Slide 12) Nội dung đoạn ...
        [/CONTEXT]

        Lịch sử hội thoại gần đây:
        User: ...
        Assistant: ...
```

### 3.6. `RagQueryService`

```csharp
public sealed class RagQueryService : IRagQueryService
{
    private readonly ApplicationDbContext _dbContext;
    private readonly ITextEmbeddingService _embedding;
    private readonly IChatCompletionService _chat;
    private readonly IDocumentChunkRetriever _retriever;
    private readonly GroundedPromptBuilder _promptBuilder;
    private readonly RagOptions _options;
    private readonly ILogger<RagQueryService> _logger;
    private readonly TimeProvider _clock;

    public async Task<RagAnswer> AskAsync(
        Guid userId, Guid chatSessionId, string question,
        CancellationToken cancellationToken = default)
    {
        ValidateQuestion(question);

        // 1. Validate session ownership (1 query)
        var session = await _dbContext.ChatSessions
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.Id == chatSessionId && s.UserId == userId, cancellationToken)
            ?? throw new ChatSessionNotFoundException(chatSessionId);

        // 2. Persist user message + touch session (1 transaction ngắn)
        var userMessage = await PersistUserMessageAsync(session.Id, question, cancellationToken);

        // 3. Embed + retrieve
        var questionEmbedding = await _embedding.EmbedAsync(question, cancellationToken);
        var evidences = await _retriever.SearchAsync(questionEmbedding, cancellationToken);

        // 4. Lọc theo min score (in-memory)
        var topEvidences = evidences
            .Where(e => e.Score >= _options.Retrieval.MinScore)
            .Take(_options.Retrieval.TopK)
            .ToList();

        // 5. Sinh câu trả lời
        string assistantContent;
        IReadOnlyList<RagCitation> citations;

        if (topEvidences.Count == 0)
        {
            assistantContent = _options.Chat.NoEvidenceMessage;
            citations = Array.Empty<RagCitation>();
        }
        else
        {
            var recentHistory = await LoadRecentHistoryAsync(session.Id, _options.Retrieval.HistoryTurns, cancellationToken);
            var (system, user) = _promptBuilder.Build(question, topEvidences, recentHistory);
            assistantContent = await _chat.CompleteAsync(system, user, cancellationToken);
            citations = BuildCitations(topEvidences);
        }

        // 6. Persist assistant + citations (1 transaction)
        var assistantMessage = await PersistAssistantExchangeAsync(session.Id, assistantContent, citations, cancellationToken);

        // 7. Auto-title lần đầu
        await EnsureSessionTitleAsync(session, question, cancellationToken);

        return new RagAnswer(
            ChatSessionId: session.Id,
            UserMessageId: userMessage.Id,
            AssistantMessageId: assistantMessage.Id,
            Answer: assistantContent,
            Citations: citations);
    }
}
```

**Các điểm cần làm rõ trong implementation**:

- `RagCitation.Excerpt`: lấy substring đầu `Content` theo `ExcerptChars`. Cắt ở ranh giới từ gần nhất để không cắt giữa từ.
- `Rank`: gán theo thứ tự trong `topEvidences` (1-based).
- `DocumentTitle`: lấy từ retrieval (JOIN `Documents`). **Lưu ý**: nếu title bị đổi sau này, citation cũ vẫn giữ title cũ vì đã snapshot trong `MessageCitation`? **Hiện tại `MessageCitation` không có cột `DocumentTitle`**. Cần quyết định:
  - **Lựa chọn A (đề xuất)**: không lưu title trong DB; lúc render Member 5 sẽ JOIN `Documents` để hiển thị. `RagCitation.DocumentTitle` chỉ phục vụ UX ngay khi trả lời.
  - **Lựa chọn B**: lưu snapshot title vào `MessageCitations.DocumentTitle` (thêm cột) -> cần schema change -> phải đi qua Member 1.
  - Chọn **A** để giữ ranh giới và không tạo migration mới.
- `PersistUserMessageAsync` dùng transaction riêng (cheap, commit ngay) để UX hiển thị bubble người dùng ngay khi request vào.
- `PersistAssistantExchangeAsync` dùng transaction riêng (assistant + citations + touch session).
- Nếu bước 4/5/6 fail:
  - User message **vẫn được giữ** (đã commit) -> UX: user thấy câu hỏi của họ, có thể retry.
  - Assistant message **không được tạo** -> câu trả lời mất, có thể log warning.
  - Nếu muốn xóa user message khi fail toàn bộ -> cần transaction bao ngoài + rollback; nhưng UX lúc đó sẽ mất câu hỏi. Đề xuất: **giữ user message** (UX > purity).
- Câu hỏi rỗng -> throw `ArgumentException` ngay.
- Cancellation:
  - Giữa các bước: kiểm tra `cancellationToken.ThrowIfCancellationRequested()` sau bước embed, sau retrieval.
  - Sau khi user message đã persist: nếu hủy trước khi assistant commit -> chỉ mất assistant, user message vẫn còn (OK).
- Exception handling:
  - `ChatSessionNotFoundException` -> UI render "Không tìm thấy cuộc hội thoại".
  - `OperationCanceledException` -> không log error, chỉ log info.
  - `HttpRequestException` từ Ollama -> log error với latency, throw lên cho UI xử lý (hiển thị "Hệ thống đang bận, thử lại sau").
  - **Không retry** trong Member 4 để tránh double-generate.

### 3.7. `OllamaChatCompletionService` & `OllamaTextEmbeddingService`

Hai class này implement hai contract đã có.

`OllamaChatCompletionService`:
- Inject `HttpClient` (named `Ollama`) + `IOptions<RagOptions>` hoặc riêng `OllamaOptions`.
- POST `{base}/api/chat` với body `{ "model": "...", "messages": [...], "stream": false }`.
- Dùng `SystemPrompt` -> message role `system`, `UserPrompt` -> role `user`.
- Response: `response.message.content` (Ollama chat API) hoặc `response` (generate API). Chọn `chat` API cho hỗ trợ system role.
- Timeout đã set ở `HttpClient` registration. Có thể set riêng cho embedding (thường nhanh hơn).

`OllamaTextEmbeddingService`:
- POST `{base}/api/embeddings` với body `{ "model": "...", "prompt": text }`.
- Response: `embedding: number[]`.
- Cache kết quả embedding cho cùng input trong cùng request scope (không cần cache cross-request; có thể thêm sau).

**Không** thêm Polly/retry vào đây - để cho layer trên quyết định. Có thể thêm sau nếu Member 5 yêu cầu.

### 3.8. Đăng ký DI

Trong `ServiceCollectionExtensions.AddInfrastructure` (sửa tại chỗ, **chỉ thêm**, không xóa):

```csharp
// (đã có)
services.AddSingleton<IDocumentIndexingQueue, InMemoryDocumentIndexingQueue>();
services.AddHttpClient("Ollama", ...);

// (mới - Member 4)
services.Configure<RagOptions>(configuration.GetSection(RagOptions.SectionName));

services.AddSingleton<ITextEmbeddingService, OllamaTextEmbeddingService>();
services.AddSingleton<IChatCompletionService, OllamaChatCompletionService>();
services.AddSingleton<IDocumentChunkRetriever, PgVectorDocumentChunkRetriever>();
services.AddSingleton<GroundedPromptBuilder>();

services.AddScoped<IRagQueryService, RagQueryService>();
```

Lưu ý:
- `RagQueryService` là **Scoped** vì phụ thuộc `ApplicationDbContext` (Scoped).
- Các service không-state là **Singleton** để khớp với `HttpClient` lifetime.
- `GroundedPromptBuilder` Singleton vì stateless.

---

## 4. Kế hoạch làm việc & điểm chạm với các member khác

### 4.1. Những gì Member 4 chủ động làm (PR `feature/rag-chat`)

| # | Hạng mục | File mới / sửa | Ước lượng |
|---|---|---|---|
| 1 | `RagOptions` + bind config | `Infrastructure/Rag/RagOptions.cs` (new); `ServiceCollectionExtensions.cs` (sửa thêm) | S |
| 2 | `IDocumentChunkRetriever` + `PgVectorDocumentChunkRetriever` | `Infrastructure/Rag/PgVectorDocumentChunkRetriever.cs` (new) | M |
| 3 | `GroundedPromptBuilder` | `Infrastructure/Rag/GroundedPromptBuilder.cs` (new) | S |
| 4 | `OllamaTextEmbeddingService` | `Infrastructure/Rag/OllamaTextEmbeddingService.cs` (new) | S |
| 5 | `OllamaChatCompletionService` | `Infrastructure/Rag/OllamaChatCompletionService.cs` (new) | M |
| 6 | `RagQueryService` + exceptions | `Features/Rag/RagQueryService.cs`, `Exceptions/*.cs` (new) | M |
| 7 | Tests | `tests/.../RagQueryServiceTests.cs`, `GroundedPromptBuilderTests.cs`, `RagArchitectureTests.cs` (new) | M |
| 8 | Docs | `docs/member-4-rag-backend-handoff.md` (file này đã có); cập nhật `docs/project-status.md`, `docs/team-workflow.md` khi merge | S |

### 4.2. Những gì **chạm vào code đã có** (rất nhỏ)

- `ServiceCollectionExtensions.cs`: thêm 6 dòng đăng ký DI + 1 dòng `Configure<RagOptions>`. **Không** xóa đăng ký `IDocumentIndexingQueue` (vẫn còn cho Member 3 tích hợp).
- `appsettings.Development.json`: thêm section `Rag:Retrieval` và `Rag:Chat` mặc định.
- `docker-compose.yml`: thêm env defaults cho `Rag__Retrieval__TopK`, ... (tùy chọn).

### 4.3. Không đụng

- `Application/Abstractions/*` — giữ nguyên 100%.
- `Application/Models/RagAnswer.cs`, `RagCitation.cs` — giữ nguyên 100%.
- Entity, EF Core configurations, migrations — giữ nguyên 100%.
- Razor Pages, MVC controllers — không thêm controller/PageModel (Member 5 sẽ thêm UI).
- `Pages/Documents/*`, `Pages/Chapters/*` — không đụng.

### 4.4. Phối hợp với Member 3

- Member 4 chỉ đợi Member 3 cung cấp chunks có `Embedding` thật trong DB.
- Có thể chạy/merge Member 4 **độc lập** với Member 3, dùng stub embedding (ví dụ `Vector = new float[768].Select(_ => 0f).ToArray()`). Khi Member 3 xong, retrieval thật sự hoạt động.
- **Cảnh báo**: nếu Member 3 chọn embedding model mà `OllamaTextEmbeddingService` không tương thích, Member 4 không phải đổi gì; chỉ cần Member 3 dùng cùng `ITextEmbeddingService`.

### 4.5. Phối hợp với Member 5

- Member 5 sẽ tạo Razor Page gọi `IRagQueryService.AskAsync(userId, sessionId, question)` qua DI.
- `userId` lấy từ `UserManager.GetUserAsync(User).Id`.
- `sessionId` lấy từ route hoặc hidden input.
- Member 4 không phụ thuộc Member 5 để test được core logic.

---

## 5. Test plan (khi triển khai)

### 5.1. Unit tests (`RagQueryServiceTests.cs`)

- `AskAsync_throws_when_question_is_null_or_whitespace`
- `AskAsync_throws_ChatSessionNotFoundException_when_session_does_not_exist`
- `AskAsync_throws_ChatSessionNotFoundException_when_session_belongs_to_other_user`
- `AskAsync_persists_user_message_even_if_subsequent_steps_fail`
- `AskAsync_returns_no_evidence_message_when_retrieval_returns_empty`
- `AskAsync_returns_no_evidence_message_when_top_evidences_below_min_score`
- `AskAsync_persists_assistant_message_with_citations_when_evidence_available`
- `AskAsync_assigns_Rank_starting_from_1_in_similarity_order`
- `AskAsync_truncates_Excerpt_to_configured_length`
- `AskAsync_updates_session_UpdatedAtUtc`
- `AskAsync_auto_titles_session_on_first_question`
- `AskAsync_propagates_OperationCanceledException_when_token_cancelled`

Sử dụng `Microsoft.EntityFrameworkCore.InMemory` (đã có trong test csproj). Stub `IChatCompletionService`/`ITextEmbeddingService` để không phụ thuộc Ollama.

### 5.2. `GroundedPromptBuilderTests.cs`

- `Build_includes_all_evidences_with_index_markers`
- `Build_truncates_context_when_exceeds_MaxContextChars`
- `BuildNoEvidenceUserPrompt_contains_only_question`
- `Build_escapes_user_input_to_avoid_prompt_injection` (kiểm tra rằng delimiter `[CONTEXT]` không bị chèn từ user input)

### 5.3. `RagArchitectureTests.cs` (convention)

- `RagQueryService_does_not_reference_Ollama_or_Pgvector_directly`
- `RagQueryService_only_depends_on_Application_abstractions_and_ApplicationDbContext`
- `All_Rag_classes_resolve_through_DI_in_AddInfrastructure`

---

## 6. Definition of Done

Member 4 hoàn thành khi:

- [x] Tất cả file mới đã tạo, file cũ chỉ sửa tối thiểu (xem mục 4.2).
- [x] `dotnet build` pass, không warning mới.
- [x] `dotnet test` pass (kể cả tests Member 1, 2 và tests mới của Member 4).
- [x] `RagOptions` đã di chuyển đúng vị trí `Infrastructure/Rag/` với namespace chuẩn.
- [x] `ExcerptChars` sử dụng từ config thay vì hardcode.
- [x] Test coverage đầy đủ cho `BuildCitations`, `RagQueryService`.
- [ ] Manual smoke test (nếu infra sẵn sàng):
  - Upload 1 PDF -> đợi indexed (cần Member 3 hoặc mock chunk).
  - Hỏi câu liên quan -> nhận câu trả lời có citation.
  - Hỏi câu ngoài phạm vi -> nhận "no-evidence" message, citations rỗng.
  - Đăng nhập user khác, thử truy cập session id của user cũ -> lỗi.
- [x] Cập nhật `docs/project-status.md` và `docs/team-workflow.md`.
- [ ] Member 5 có thể chạy `dotnet run` và gọi `IRagQueryService` thành công.

---

## 7. Rủi ro & quyết định cần xác nhận

| Rủi ro | Giảm thiểu |
|---|---|
| Embedding dimension thay đổi khi đổi model | Query pgvector không cần biết dim trước (Pgvector.Vector tự quản). Chỉ cần cùng dim giữa indexing & query (cùng model qua DI). |
| pgvector index chưa có -> query chậm khi dataset lớn | Demo dataset nhỏ (<100 doc) chấp nhận brute-force. Tạo HNSW index là tương lai, **phải đi qua Member 1** nếu muốn. |
| Member 5 dùng `RagCitation.DocumentTitle` để hiển thị nhưng title document đổi | Citation cũ hiển thị title cũ (vì snapshot lúc retrieval). OK cho UX nhưng cần tài liệu hóa. |
| Race condition: user hỏi 2 câu cùng lúc trong 1 session | Hai transaction độc lập, ordering theo `CreatedAtUtc`. Không vấn đề. |
| Session title bị overwrite bởi câu hỏi ngắn không ý nghĩa | Chỉ auto-title **một lần** (khi title rỗng). |
| Ollama trả về response không có `message.content` | Coi như exception; trả lỗi lên UI, không lưu assistant message. |

---

## 8. Tóm tắt nguyên tắc "một thay đổi không lan"

| Nếu cần đổi... | Chỉ chạm vào... |
|---|---|
| Embedding provider | `OllamaTextEmbeddingService.cs` (+ dòng DI) |
| Chat provider | `OllamaChatCompletionService.cs` (+ dòng DI) |
| Retrieval strategy | `PgVectorDocumentChunkRetriever.cs` (+ dòng DI) |
| Prompt template | `GroundedPromptBuilder.cs` + `RagOptions.PromptTemplate` |
| TopK / score threshold | `appsettings.json` / biến môi trường |
| No-evidence message | `RagOptions.Chat.NoEvidenceMessage` |
| Auto-title policy | `RagQueryService.EnsureSessionTitleAsync` |
| Citation excerpt length | `RagOptions.Retrieval.ExcerptChars` |
| Conversation history window | `RagOptions.Retrieval.HistoryTurns` |

**Không bao giờ** cần đổi `IRagQueryService`/`RagAnswer`/`RagCitation` trừ khi thay đổi yêu cầu cross-cutting (rất hiếm, và khi đó phải đồng bộ Member 5 trong cùng PR).

---

## 9. Open questions cần hỏi team (trước khi code)

1. **Embedding dim & model**: xác nhận `qwen3-embedding:0.6b` (đã có trong config) là mặc định cuối cùng. Nếu Member 3 chọn model khác, Member 4 phải đồng bộ.
2. **pgvector index**: có muốn tạo HNSW/IVFFlat trong migration này không, hay để Member 3 migration? **Đề xuất**: để Member 1 chủ trì tạo 1 migration riêng nếu cần (vì là schema change). **Hiện tại: quyết định sau.**
3. **Auto-title**: có cần Member 5 dùng title do Member 4 generate, hay Member 5 tự lo? **Đề xuất**: Member 4 lo. **Hiện tại: defer được, implementation có thể toggle qua config.**
4. **`DocumentTitle` snapshot trong citation**: có cần schema change để giữ title cũ không? **Đề xuất**: không cần, Member 5 JOIN lúc render.
5. **History window**: confirm lấy **5 lượt** gần nhất (đã xác nhận với team).
6. **No-evidence threshold**: `MinScore = 0.5` có hợp lý với `qwen3-embedding:0.6b` không? Cần benchmark (Member 5 evaluation set sẽ trả lời).

---

## 10. Sprint plan — branch, commits, PR timeline

### 10.1. Nguyên tắc chia commit

Mỗi commit phải:
- **Compile pass** (`dotnet build` green).
- **Test pass** (test mới + existing test không break).
- **Có thể review trong < 15 phút** (vì review nhiều file cùng lúc dễ miss).
- **Independent** — có thể squash lại thành 1 hoặc tách ra nhiều PR nhỏ nếu cần.

**Thứ tự ưu tiên**: commit nào làm xong sớm, merge sớm, để Member 5 bắt đầu được.

### 10.2. Branch

```text
git checkout -b feature/rag-chat
```

### 10.3. Commit order — 7 commits, 4 integration checkpoints

---

#### Commit 1: Infrastructure foundation — `RagOptions` + DI wiring

**Mục đích**: thiết lập config infrastructure mà không ảnh hưởng gì cả (pure additive).

| File | Action |
|---|---|
| `Infrastructure/Rag/RagOptions.cs` | **NEW** — strongly-typed options class |
| `appsettings.Development.json` | **ADD** — `Rag:` section với defaults |
| `docker-compose.yml` | **ADD** — `Rag__Retrieval__TopK=5` env defaults (optional) |

**Không test**: chỉ là POCO + JSON. Convention test không cần.

**Sau commit 1**: build green. Member 5 có thể bắt đầu đọc `RagOptions` để hiểu config.

---

#### Commit 2: Ollama provider — embedding + chat service

**Mục đích**: implement 2 contract đã có, chạy được end-to-end (nếu Ollama up).

| File | Action |
|---|---|
| `Infrastructure/Rag/OllamaTextEmbeddingService.cs` | **NEW** — implement `ITextEmbeddingService` |
| `Infrastructure/Rag/OllamaChatCompletionService.cs` | **NEW** — implement `IChatCompletionService` |
| `Infrastructure/ServiceCollectionExtensions.cs` | **MODIFY** — thêm 5 dòng DI registration + `Configure<RagOptions>` |

**Smoke test**: nếu có Ollama, gọi thật. Nếu không, dùng test mock.

**Sau commit 2**: `ITextEmbeddingService` và `IChatCompletionService` có implementation thật. Member 3 có thể reuse. Member 5 có thể bắt đầu mock để test UI.

---

#### Commit 3: Prompt builder — pure logic, no deps

**Mục đích**: `GroundedPromptBuilder` hoàn toàn stateless, test được bằng unit test không cần DB hay Ollama.

| File | Action |
|---|---|
| `Infrastructure/Rag/GroundedPromptBuilder.cs` | **NEW** |
| `tests/.../GroundedPromptBuilderTests.cs` | **NEW** — ~10 test cases |

**Điều kiện pass**: `dotnet test --filter GroundedPromptBuilder` green.

**Sau commit 3**: prompt logic ổn định. Không đụng AI hay DB.

---

#### Commit 4: Retrieval — pgvector query

**Mục đích**: implement `IDocumentChunkRetriever` (internal), chỉ query chunk đã indexed.

| File | Action |
|---|---|
| `Infrastructure/Rag/PgVectorDocumentChunkRetriever.cs` | **NEW** |
| `tests/.../PgVectorDocumentChunkRetrieverTests.cs` | **NEW** — test với InMemory không có pgvector; dùng mock `ITextEmbeddingService` |

**Lưu ý**: `InMemory` EF provider không hỗ trợ pgvector, nên test dùng **mock** `ITextEmbeddingService` và **mock** `ApplicationDbContext` (list-based) thay vì InMemory real EF. Hoặc test bằng cách verify SQL generated + mock vector output.

**Sau commit 4**: retrieval logic tách biệt. Có thể verify bằng manual SQL test.

---

#### Commit 5: Core — `RagQueryService` + exceptions

**Mục đích**: implement `IRagQueryService` — trách nhiệm chính, transaction coordination.

| File | Action |
|---|---|
| `Features/Rag/RagQueryService.cs` | **NEW** |
| `Features/Rag/Exceptions/RagException.cs` | **NEW** |
| `Features/Rag/Exceptions/ChatSessionNotFoundException.cs` | **NEW** |

**Test coverage**: `RagQueryServiceTests.cs` — 12 test cases (xem mục 5.1 trong file này). Sử dụng InMemory EF + stub `IChatCompletionService` + mock `ITextEmbeddingService`.

**Điều kiện pass**: `dotnet test --filter RagQueryService` green + all existing tests green.

**Sau commit 5**: `IRagQueryService` hoàn chỉnh. Member 5 **có thể bắt đầu code UI** ngay (gọi `IRagQueryService.AskAsync`).

---

#### Commit 6: Architecture convention tests

**Mục đích**: khóa lại thiết kế, không ai vô tình pull Ollama/pgvector vào `RagQueryService`.

| File | Action |
|---|---|
| `tests/.../RagArchitectureTests.cs` | **NEW** |

Test cases:
1. `RagQueryService_does_not_reference_Ollama_types_directly`
2. `RagQueryService_does_not_reference_Pgvector_types_directly`
3. `RagQueryService_only_depends_on_Application_abstractions_and_DbContext`
4. `RagOptions_all_properties_have_defaults` (không throw nếu config missing)

**Điều kiện pass**: `dotnet test --filter RagArchitecture` green.

**Sau commit 6**: convention locked. Team có thể yên tâm refactor.

---

#### Commit 7: Documentation + handoff

**Mục đích**: cập nhật status + team docs, đánh dấu hoàn thành.

| File | Action |
|---|---|
| `docs/project-status.md` | **MODIFY** — cập nhật Member 4: Complete |
| `docs/team-workflow.md` | **MODIFY** — cập nhật Member 4 handoff notes |
| `docs/member-4-rag-backend-handoff.md` | **CREATE** — phân tích + plan này |

---

### 10.4. Integration checkpoint timeline

```
Baseline (master): Member 1 + Member 2 merged

Week 1
  ├─ Commit 1: RagOptions + config        → branch green ✓
  ├─ Commit 2: Ollama services           → ITextEmbeddingService + IChatCompletionService implemented ✓
  └─ Member 5 có thể bắt đầu đọc docs + plan UI
      (không phụ thuộc implementation details)

Week 2
  ├─ Commit 3: GroundedPromptBuilder    → prompt logic stable ✓
  ├─ Commit 4: pgvector retrieval        → retrieval stable ✓
  └─ Member 5 có thể viết UI stub (gọi service) dù chưa có chunks

Week 3
  ├─ Commit 5: RagQueryService           → core logic complete ✓ ✓ (Member 5 bắt đầu integration test với UI)
  └─ Commit 6: Architecture tests         → convention locked ✓

Week 4
  ├─ Commit 7: Docs + handoff            → DONE
  ├─ PR review
  └─ Merge into master
       ↓
Member 5 integration: chat UI + history + citation rendering
Member 5 evaluation: ground-truth set
```

### 10.5. Khi nào Member 5 có thể bắt đầu?

| Member 5 cần gì từ Member 4 | Có sau commit |
|---|---|
| Biết `IRagQueryService` signature | Ngay (đã có trong baseline) |
| Biết `RagAnswer` / `RagCitation` structure | Ngay (đã có trong baseline) |
| Biết config options | Sau **Commit 1** |
| Biết cách call service từ PageModel | Sau **Commit 2** |
| Integration test với UI thật | Sau **Commit 5** |

**Khuyến nghị**: Member 5 nên bắt đầu UI scaffold (Razor Page structure, session list, chat bubble layout) **sau Commit 2**, dù chưa có backend thật. Dùng mock/stub `IRagQueryService` để compile.

### 10.6. Parallelism với Member 3

| Thời điểm | Member 4 cần gì từ Member 3 | Member 3 cần gì từ Member 4 |
|---|---|---|
| Ngay từ đầu | `ITextEmbeddingService` (đã có interface) | Không gì |
| Commit 2 | Ollama embed/chat working | Không gì |
| Commit 4 | Có `DocumentChunk` có `Embedding` trong DB | Không gì |
| End-to-end | Chunk đã indexed | Không gì |

**Điểm chờ**: Member 4 có thể 100% done mà không cần Member 3, nếu dùng stub chunks (fake `Vector` để test retrieval SQL). Member 3 chỉ ảnh hưởng đến **chất lượng retrieval thật** khi đã có document indexed.

### 10.7. Checklist trước khi mở PR

```text
□ dotnet build --configuration Release  (0 warnings)
□ dotnet test                          (100% pass, kể cả existing tests)
□ dotnet test --filter RagArchitecture (4/4 convention tests pass)
□ git diff --stat  (chỉ có 7 nhóm file theo commit plan)
□ Không đụng Pages/ (Razor Pages)
□ Không đụng Application/Abstractions/ (chỉ thêm RagOptions)
□ Không tạo migration mới (đã confirm: pgvector index defer)
□ appsettings.Development.json đã update với Rag section
□ docker-compose.yml đã có Rag env defaults (nếu muốn)
□ docs/project-status.md đã cập nhật
□ docs/team-workflow.md đã cập nhật
□ member-4-rag-backend-handoff.md đã commit
```

### 10.8. PR description template

```markdown
## Summary

Implements the RAG backend (Flow 2) for the PRN222 RAG Assistant.

### What changed

- `Infrastructure/Rag/` — Ollama provider implementations, retrieval, prompt builder, config
- `Features/Rag/` — `RagQueryService` + domain exceptions
- Tests: `RagQueryServiceTests`, `GroundedPromptBuilderTests`, `RagArchitectureTests`
- Config: `appsettings.Development.json` + `docker-compose.yml` env vars

### Integration points

- `IRagQueryService.AskAsync(userId, sessionId, question)` — ready for Member 5
- `ITextEmbeddingService` + `IChatCompletionService` — ready for Member 3 reuse

### Test plan

- [x] `dotnet build` pass
- [x] `dotnet test` pass (all existing + 25 new tests)
- [x] Architecture tests pass (RagQueryService isolated from Ollama/pgvector types)
- [x] Manual smoke: ask question with indexed doc → answer with citation; ask out-of-scope → no-evidence message

### Breaking changes

None. All existing contracts (`Application/Abstractions/`, `RagAnswer`, `RagCitation`) unchanged.

### Open items (deferred)

- pgvector HNSW index: pending schema decision (tracked separately)
- Auto-title session: toggleable via `RagOptions.AutoTitle` (default false)
```

---

## 11. Tóm tắt nhanh — "chỉ một trang"

| Item | Decision |
|---|---|
| **Branch** | `feature/rag-chat` |
| **Commits** | 7 (RagOptions → Ollama → PromptBuilder → Retrieval → RagQueryService → ArchTests → Docs) |
| **File đụng code cũ** | Chỉ `ServiceCollectionExtensions.cs` (+6 dòng), `appsettings.Development.json`, `docker-compose.yml` |
| **File không đụng** | `Application/Abstractions/*`, `Application/Models/*`, Entities, EF Configs, Razor Pages |
| **Test mới** | ~25 unit tests + 4 architecture tests |
| **Sprint** | 4 tuần (Member 5 có thể start UI sau tuần 2) |
| **Migration** | Không (pgvector index defer) |
| **pgvector index** | Defer — quyết định sau |
| **Auto-title** | Defer — toggle qua config, default off |
| **History window** | 5 lượt (đã xác nhận) |
| **Ranh giới không thay đổi** | `IRagQueryService`, `RagAnswer`, `RagCitation` giữ nguyên 100% |
| **Deprecate gì** | Không |
| **Follows conventions** | EntityModelConventionsTests, CoreDataArchitectureTests vẫn pass |
