# Flow 3 - Report & Statistics handoff

## Purpose

This document defines the independent third product workflow without disturbing the completed Flow 1 indexing pipeline or the pending Flow 2 RAG/chat work.

The project uses:

1. **Flow 1 - Document Management & Indexing** - complete
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending Members 4/5
3. **Flow 3 - Report & Statistics** - pending Member 2

Conversation History belongs to Flow 2.

## Owner

**Member 2 owns Flow 3 - Report & Statistics.**

Ownership boundaries remain:

- Member 1: Core/Data and migration coordination
- Member 2: completed Flow 1 request side + pending Flow 3
- Member 3: completed Flow 1 indexing side
- Member 4: pending Flow 2 RAG backend
- Member 5: pending Flow 2 presentation/evaluation

Flow 3 must be implemented in a separate focused branch after synchronizing with latest `master`.

## Current dependency state

Member 3 is now merged through PR #9, so Flow 3 can immediately report real indexing data from persisted `Document`/`DocumentChunk` records.

Available now:

- chapter/document totals
- document counts by indexing state
- indexed/failed/processing/uploaded counts
- document grouping by chapter/unassigned
- `IndexedAtUtc` and `IndexError` where useful
- `DocumentChunk` totals where useful

Chat usage data depends on Flow 2. Until Members 4/5 persist chat records, the reporting UI must show correct zero/empty states rather than blocking development.

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
      |      +--> optional recent indexed items
      |      \--> optional recent failures / IndexError
      |
      +--> Chat usage overview
      |      +--> total chat sessions
      |      +--> total messages
      |      \--> total citations
      |
      v
Read-only dashboard / tables
```

The visual design can remain simple. The workflow is defined by the Subject Leader opening the reporting area and receiving current aggregate information without mutating source data.

## Initial implementation scope

Expected data sources:

- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Required first version:

- Subject-Leader-only Reports/Statistics page
- total PRN222 chapters
- total PRN222 documents
- document count grouped by `DocumentIndexStatus`
- document count grouped by chapter
- unassigned document count
- total chat sessions
- total chat messages
- total message citations
- zero/empty states

Useful optional additions that still fit the first version:

- total indexed chunks
- recent indexing failures using existing `IndexError`
- recently indexed documents using `IndexedAtUtc`

The first version does **not** need exports, payments/subscriptions, custom event tracking, a reporting warehouse, scheduled aggregation, or additional infrastructure.

## Query guidance

Prefer:

- aggregate EF Core queries
- `AsNoTracking()` for read-only queries where appropriate
- server-side authorization
- simple cards/tables/progress bars
- direct use of existing persisted indexing/chat state

Do not scan `storage/uploads/` to calculate report counts. PostgreSQL is the source of truth for report metadata.

## Non-interference rules

Flow 3 must not:

- enqueue/re-index documents
- modify `DocumentIndexingWorker`
- change parser/chunker/embedding behavior
- change document index state
- perform pgvector similarity retrieval
- call Ollama
- duplicate Member 5 chat/session/history pages
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

See `docs/member-3-document-indexing-handoff.md` for the indexing implementation details.

## Relationship to pending Flow 2

Flow 3 may read `ChatSession`, `ChatMessage`, and `MessageCitation` after Flow 2 begins populating them.

Flow 3 does not own:

- question retrieval
- answer generation
- conversation management
- citation creation

Those remain Flow 2 responsibilities.

## Suggested branch

```text
feature/report-statistics
```

Keep the PR limited to Flow 3. Do not mix indexing fixes, RAG implementation, chat UI work, or unrelated architecture refactors into the same PR.

## Suggested tests

At minimum cover:

- Subject Leader access
- Student/unauthorized behavior according to the chosen policy
- total chapter/document counts
- grouping by index status
- grouping by chapter and unassigned bucket
- zero/empty chat-data state
- read-only behavior

## Documentation synchronization

When Flow 3 is implemented, synchronize:

- `AGENTS.md`
- `src/PRN222.RagAssistant/Application/AGENTS.md`
- `README.md`
- `docs/project-status.md`
- `docs/team-workflow.md`
- `docs/infrastructure.md`
- `docs/member-1-core-data-handoff.md`
- `docs/member-2-document-management-handoff.md`
- `docs/member-3-document-indexing-handoff.md` if its consumer boundary changes
- this file

## Acceptance criteria

Flow 3 is complete when:

1. A Subject Leader can navigate to Reports/Statistics.
2. The page reads aggregate PRN222 data from the existing application database.
3. It shows document/indexing statistics and chat/session usage statistics.
4. Empty/zero-data states render correctly.
5. It remains read-only.
6. No speculative schema/migration is introduced solely for reporting.
7. Relevant tests cover aggregate correctness and access restriction.
8. Repository documentation reflects the actual merged state.