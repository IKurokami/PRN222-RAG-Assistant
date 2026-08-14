# Flow 3 - Report & Statistics handoff

## Purpose and status

This document records the completed independent third workflow after PR #12 merged **Flow 3 - Report & Statistics**.

Current workflow presentation allocation:

1. **Flow 1 - Document Management & Indexing** - complete - **MVC Controllers + Views**
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - **MVC Controllers + Views**
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

Conversation History belongs to Flow 2.

The later Flow 1 Razor Pages -> MVC migration does not change Flow 3's read-only reporting implementation or its data sources.

## Owner

**Member 2 owns Flow 3 - Report & Statistics, and the assigned implementation is complete.**

Ownership boundaries:

- Member 1: Core/Data and migration coordination
- Member 2: completed Flow 1 MVC request side + completed Flow 3
- Member 3: completed Flow 1 indexing side
- Member 4: pending Flow 2 RAG backend
- Member 5: pending MVC Flow 2 presentation/evaluation

## Implementation

Flow 3 remains under:

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
tests/PRN222.RagAssistant.Tests/ReportStatisticsTests.cs
```

The page is protected by:

```text
[Authorize(Policy = AppPolicies.ManageDocuments)]
```

`AppPolicies.ManageDocuments` requires the `SubjectLeader` role.

## Current dependency state

Flow 1 is complete through the MVC request side plus the existing background indexing pipeline. Flow 3 reads the resulting persisted `Document` / `DocumentChunk` state.

Available metrics:

- chapter/document totals
- document counts by indexing state
- document grouping by chapter/unassigned
- `IndexedAtUtc` and `IndexError`
- `DocumentChunk` totals
- recently indexed documents with chunk counts
- indexing completion percentage
- chat session/message/citation totals

Chat usage becomes meaningful when Flow 2 begins persisting chat rows; until then zero/empty states are expected.

## Flow definition

Primary actor: **Subject Leader**.

```text
Subject Leader
      |
      v
Open Reports / Statistics
      |
      +--> Document overview
      +--> Indexing overview
      +--> Chat usage overview
      |
      v
Read-only dashboard / tables
```

## Data sources

Flow 3 reads existing persistence from:

- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

No reporting-specific entity or migration is required.

Queries use focused EF Core aggregates and `AsNoTracking()` where appropriate. PostgreSQL remains the source of truth; reports do not scan `storage/uploads/` for counts.

## Non-interference rules

Future Flow 3 maintenance must not:

- enqueue/re-index documents
- modify `DocumentIndexingWorker`
- change parser/chunker/embedding behavior
- mutate document index state
- perform pgvector similarity retrieval
- call Ollama
- duplicate Flow 2 MVC chat/session/history pages
- mutate chapters/documents/chunks/chat rows
- redesign shared entities for dashboard convenience
- create speculative analytics schema
- change shared `Application/` contracts solely for reporting convenience

If a genuine persistence gap is discovered, document it and coordinate schema changes through Member 1.

## Relationship to Flow 1

```text
Flow 1 MVC request side
Document -> queue -> worker -> parse/chunk/embed -> persisted index state
                                                    |
                                                    v
Flow 3 reads persisted aggregate state -------------+
```

The Flow 1 presentation migration does not alter this consumer boundary. See `docs/flow-1-mvc-migration.md` and `docs/member-3-document-indexing-handoff.md`.

## Relationship to Flow 2

Flow 2 is assigned to MVC Controllers + Views. Flow 3 may read `ChatSession`, `ChatMessage`, and `MessageCitation` after Flow 2 populates them, but does not own chat controllers/views, question retrieval, answer generation, Conversation History, or citation creation.

Do not create a Razor Pages duplicate of Flow 2.

## Tests and validation

PR #12 reported `75/75` tests passing and local smoke testing confirmed Subject-Leader access, real Flow 1 indexing, and corresponding aggregate values in Flow 3.

The Flow 1 MVC migration should preserve the same persisted behavior; Flow 3 requires no code or schema redesign for that presentation change.

## Acceptance criteria status

1. Subject Leader can navigate to Reports/Statistics - **complete**.
2. The page reads aggregate PRN222 data from the application database - **complete**.
3. It shows document/indexing and chat/session usage statistics - **complete**.
4. Empty/zero-data states render correctly - **complete**.
5. It remains read-only - **complete**.
6. No speculative reporting schema/migration was introduced - **complete**.
7. Relevant tests cover aggregates and access restriction - **complete**.

## Maintenance guidance

Do not recreate `feature/report-statistics` as a competing implementation. New product work should focus on pending Flow 2 unless a requirement explicitly reopens reporting.
