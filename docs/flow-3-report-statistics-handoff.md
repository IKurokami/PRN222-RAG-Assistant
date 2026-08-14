# Flow 3 - Report & Statistics handoff

## Purpose and status

This document records the completed independent third workflow after PR #12 merged **Flow 3 - Report & Statistics**.

The project now uses:

1. **Flow 1 - Document Management & Indexing** - complete - Razor Pages presentation
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending Members 4/5 - **ASP.NET Core MVC Controllers + Views presentation**
3. **Flow 3 - Report & Statistics** - complete through PR #12 - Razor Pages presentation

Conversation History belongs to Flow 2.

## Owner

**Member 2 owns Flow 3 - Report & Statistics, and the assigned implementation is complete.**

Ownership boundaries remain:

- Member 1: Core/Data and migration coordination
- Member 2: completed Flow 1 request side + completed Flow 3
- Member 3: completed Flow 1 indexing side
- Member 4: pending Flow 2 RAG backend
- Member 5: pending **MVC** Flow 2 presentation/evaluation

Flow 3 should now be treated as merged baseline behavior rather than a pending feature branch.

## Merged implementation

PR #12 added:

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
tests/PRN222.RagAssistant.Tests/ReportStatisticsTests.cs
```

The shared layout also exposes the Reports navigation entry to Subject Leaders.

The Reports page is a Razor Pages implementation and is protected by:

```text
[Authorize(Policy = AppPolicies.ManageDocuments)]
```

`AppPolicies.ManageDocuments` requires the `SubjectLeader` role.

## Current dependency state

Flow 1 is complete through Member 2 + Member 3, so Flow 3 reports real indexing data from persisted `Document` / `DocumentChunk` state.

Available and implemented:

- chapter/document totals
- document counts by indexing state
- indexed/failed/processing/uploaded counts
- document grouping by chapter/unassigned
- `IndexedAtUtc` and `IndexError`
- `DocumentChunk` totals
- recently indexed documents with per-document chunk count
- indexing completion percentage

Chat usage data still depends on Flow 2. Until Members 4/5 persist chat records, the reporting UI correctly shows zero/empty chat metrics.

## Flow definition

Primary actor: **Subject Leader**.

Goal: inspect current PRN222 content/indexing/usage state through read-only aggregate information.

```text
Subject Leader
      |
      v
Open Reports / Statistics
      |
      +--> Document overview
      |      +--> total chapters/documents
      |      +--> documents by chapter
      |      \--> unassigned documents
      |
      +--> Indexing overview
      |      +--> Uploaded / Processing / Indexed / Failed
      |      +--> completion percentage
      |      +--> recent indexed items + chunk counts
      |      \--> recent failures / IndexError
      |
      +--> Chat usage overview
      |      +--> total chat sessions
      |      +--> total messages
      |      \--> total citations
      |
      v
Read-only dashboard / tables
```

## Implemented data sources

The merged page reads existing persistence from:

- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

No reporting-specific entity or migration was added.

## Query behavior

The implementation uses focused EF Core aggregate queries with `AsNoTracking()` where appropriate.

PostgreSQL remains the source of truth for report metadata. Reports do not scan `storage/uploads/` to calculate counts.

## Non-interference rules

Future Flow 3 maintenance must not:

- enqueue/re-index documents
- modify `DocumentIndexingWorker`
- change parser/chunker/embedding behavior
- change document index state
- perform pgvector similarity retrieval
- call Ollama
- duplicate Member 5 MVC chat/session/history pages
- mutate chapters/documents/chunks/chat sessions/messages/citations
- redesign shared entities for dashboard convenience
- create speculative analytics entities/migrations
- change shared `Application/` contracts solely for reporting convenience

If a genuine persistence gap is discovered, document it first and coordinate the schema through Member 1.

## Relationship to completed Flow 1

Flow 3 reads the output of Flow 1 but does not participate in the indexing pipeline.

```text
Flow 1
Document -> queue -> worker -> parse/chunk/embed -> persisted index state
                                              |
                                              v
Flow 3 reads persisted aggregate state --------+
```

See `docs/member-3-document-indexing-handoff.md` for indexing implementation details.

Post-merge local smoke testing confirmed this consumer boundary by uploading/indexing a PDF through Flow 1 and then observing the corresponding chapter/document/chunk/indexing values in Flow 3.

## Relationship to pending Flow 2 - MVC

Flow 2 is now explicitly assigned to **ASP.NET Core MVC Controllers + Views** for presentation.

Flow 3 may read `ChatSession`, `ChatMessage`, and `MessageCitation` after Flow 2 begins populating them, but it does not own:

- MVC chat controllers/views
- question retrieval
- answer generation
- conversation management
- citation creation

Those remain Flow 2 responsibilities.

Expected Flow 2 presentation areas include:

```text
src/PRN222.RagAssistant/Controllers/
src/PRN222.RagAssistant/Views/Chat/
```

Do not create a Razor Pages duplicate of Flow 2 under `Pages/Chat` or `Pages/Conversation`.

## Tests and validation

PR #12 reported `75/75` tests passing, including focused Flow 3 tests covering authorization attributes, empty/zero states, PRN222 scope filtering, index-state grouping, chapter counts, recent failure limits, and read-only behavior.

Post-merge local smoke testing additionally reported:

- anonymous access to `/Reports/Index` redirects to login
- Student access is denied
- Subject Leader access succeeds
- a real PDF can pass through `Uploaded -> Processing -> Indexed`
- resulting chunk/indexing data appears on the dashboard

## Acceptance criteria status

Flow 3 meets the original acceptance criteria:

1. Subject Leader can navigate to Reports/Statistics - **complete**.
2. The page reads aggregate PRN222 data from the application database - **complete**.
3. It shows document/indexing statistics and chat/session usage statistics - **complete**.
4. Empty/zero-data states render correctly - **complete**.
5. It remains read-only - **complete**.
6. No speculative schema/migration was introduced solely for reporting - **complete**.
7. Relevant tests cover aggregate correctness and access restriction - **implemented in PR #12**.
8. Repository documentation is synchronized by the team-lead documentation follow-up after merge.

## Maintenance guidance

Do not recreate `feature/report-statistics` as a competing implementation.

New work should focus on pending Flow 2 unless a new requirement explicitly reopens reporting.
