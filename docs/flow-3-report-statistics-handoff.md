# Flow 3 - Report & Statistics handoff

## Purpose

This document defines the third product workflow required by the course project without disturbing the existing Flow 1 indexing work or Flow 2 RAG/chat work.

The project now treats these as the three product workflows:

1. **Flow 1 - Document Management & Indexing**
2. **Flow 2 - RAG Question & Answer & Conversation Management**
3. **Flow 3 - Report & Statistics**

Conversation history belongs to Flow 2 because it is part of managing a student's chat sessions and previously persisted answers. It is no longer counted as the independent third workflow.

## Owner

**Member 2 owns Flow 3 - Report & Statistics after the merged Flow 1 request/presentation work.**

This assignment is intentionally chosen to avoid disrupting the currently active integration path:

- Member 1 remains the shared Core/Data and migration coordinator.
- Member 3 continues Flow 1 indexing/ingestion without reporting changes in the indexing worker.
- Member 4 continues Flow 2 RAG/backend work without reporting responsibilities.
- Member 5 continues Flow 2 chat/history presentation and the evaluation deliverable.
- Member 2 can start Flow 3 from the merged `master` baseline in a separate focused branch.

## Flow definition

Primary actor: **Subject Leader**.

Goal: inspect the current state and usage of the PRN222 RAG assistant through read-only aggregate information.

Suggested end-to-end flow:

```text
Subject Leader
      |
      v
Open Reports / Statistics
      |
      +--> Document overview
      |      +--> total documents
      |      +--> documents by indexing status
      |      +--> documents by chapter / unassigned
      |
      +--> Indexing overview
      |      +--> Indexed / Failed / Processing / Uploaded counts
      |      \--> recent indexing state where existing timestamps allow it
      |
      +--> Chat usage overview
      |      +--> total chat sessions
      |      +--> total messages
      |      \--> total persisted citations
      |
      v
Read-only dashboard / tables
```

The exact visual layout can be simple. The workflow is defined by the Subject Leader opening the reporting area, requesting current aggregate information, and receiving a read-only summary derived from persisted application data.

## Initial implementation scope

Flow 3 should use the persistence that already exists before proposing new analytics storage.

Expected data sources include:

- `Chapter`
- `Document`
- `DocumentChunk`
- `ChatSession`
- `ChatMessage`
- `MessageCitation`

Recommended first version:

- Subject-Leader-only Reports page
- total PRN222 chapters
- total PRN222 documents
- document count grouped by `DocumentIndexStatus`
- document count grouped by chapter, including unassigned documents
- total chat sessions
- total chat messages
- total message citations
- clear empty states when Flow 2 data does not exist yet

The first version does **not** need custom event tracking, a reporting warehouse, scheduled aggregation, exports, payments, subscriptions, or additional infrastructure.

## Non-interference rules

Flow 3 is deliberately read-only and must not destabilize the other member branches.

Member 2 must not implement Flow 3 by:

- changing document parsing, chunking, embedding, queue, or background-worker behavior owned by Member 3
- calling Ollama or running pgvector similarity retrieval owned by Member 4
- duplicating Member 5 chat/session/history pages
- redesigning shared entities merely to make a dashboard easier
- creating speculative analytics entities or migrations
- changing existing Flow 1 behavior while adding reporting
- using report pages to mutate documents, chapters, chats, messages, citations, or indexing state

Prefer aggregate, no-tracking EF Core queries against the existing model. If a genuine persistence gap is discovered, document the requirement first and coordinate the schema change through Member 1 instead of creating an isolated competing migration.

## Dependency behavior

Flow 3 can be developed incrementally from the current database model.

- Document and chapter metrics can work immediately from the merged Flow 1 persistence.
- Indexing metrics become more meaningful as Member 3 lands the real indexing implementation.
- Chat/session/message/citation metrics naturally become non-zero after Members 4 and 5 land Flow 2.

The reporting UI must handle zero rows gracefully so Member 2 does not need to wait for Members 3-5 before creating the basic workflow.

## Suggested branch

Use a focused branch after synchronizing with the latest `master`:

```text
feature/report-statistics
```

Keep the PR limited to Flow 3. Do not mix Flow 1 fixes, indexing changes, RAG changes, or chat UI refactors into the same PR.

## Acceptance criteria

Flow 3 is considered implemented when:

1. A Subject Leader can navigate to a Reports/Statistics page.
2. The page reads current PRN222 aggregate data from the existing application database.
3. At minimum it shows document/indexing statistics and chat/session usage statistics.
4. Empty/zero-data states render correctly.
5. The workflow is read-only and does not mutate source workflow records.
6. No speculative schema/migration is introduced solely for reporting.
7. Relevant tests cover the aggregate/query behavior and access restriction.
8. `docs/project-status.md`, `docs/team-workflow.md`, `README.md`, and `AGENTS.md` are synchronized when the implementation is merged.
