# Flow 3 - Report & Statistics handoff

## Status

**Flow 3 - Report & Statistics is COMPLETE and merged through PR #12.**

Merged baseline:

- PR #12: `feat(flow3): implement Report & Statistics dashboard page for Subject Leader`
- Flow 3 owner: Member 2
- merged into `master` at `00903a38693956f59090f71649ca8a99e053e604`
- PR verification reported `75/75` automated tests passing
- post-merge local smoke testing confirmed Subject Leader access, Student/anonymous denial, real indexing aggregates, chapter/document counts, chunk totals, and recently indexed document rendering

The project now has:

1. **Flow 1 - Document Management & Indexing** - complete
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending Members 4/5
3. **Flow 3 - Report & Statistics** - complete

Conversation History belongs to Flow 2 and is not counted as Flow 3.

## Owner and boundaries

**Member 2 owns the completed Flow 3 implementation.**

Ownership remains:

- Member 1: Core/Data and migration coordination
- Member 2: completed Flow 1 request side + completed Flow 3 reporting
- Member 3: completed Flow 1 indexing side
- Member 4: pending Flow 2 RAG backend
- Member 5: pending Flow 2 presentation/evaluation

Flow 3 is now a downstream read-only consumer of persisted Flow 1/Flow 2 data. Later members must not move reporting logic into the indexing or RAG pipelines.

## Implemented flow

Primary actor: **Subject Leader**.

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
      |      +--> total DocumentChunk count
      |      +--> recently indexed documents
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

## Merged implementation

Primary files:

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
src/PRN222.RagAssistant/Pages/Shared/_Layout.cshtml
tests/PRN222.RagAssistant.Tests/ReportStatisticsTests.cs
```

The page model is protected by:

```text
[Authorize(Policy = AppPolicies.ManageDocuments)]
```

`AppPolicies.ManageDocuments` requires the `SubjectLeader` role. Hiding the Reports navigation link is only presentation behavior; server-side policy enforcement remains the security boundary.

## Implemented report data

Flow 3 reads existing persistence through EF Core aggregate queries and `AsNoTracking()` where appropriate.

Current dashboard includes:

- total PRN222 chapters
- total PRN222 documents
- unassigned document count
- documents grouped by chapter
- document counts for `Uploaded`, `Processing`, `Indexed`, and `Failed`
- indexing completion percentage
- total persisted `DocumentChunk` count for PRN222 documents
- up to 10 recent indexing failures with `IndexError`
- up to 10 recently indexed documents with chunk count and index timestamp
- total `ChatSession` count
- total `ChatMessage` count
- total `MessageCitation` count
- graceful zero/empty states while Flow 2 has no chat data

PostgreSQL remains the source of truth. Flow 3 does not scan `storage/uploads/` to compute counts.

## Non-interference rules

The completed Flow 3 implementation preserves these boundaries and future changes must continue to do so.

Flow 3 must not:

- enqueue/re-index documents as part of reporting
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

If a genuine persistence gap appears later, document it first and coordinate schema/migration work through Member 1.

## Relationship to completed Flow 1

Flow 3 reads the output of Flow 1 but does not participate in indexing.

```text
Flow 1
Document -> queue -> worker -> parse/chunk/embed -> persisted index state
                                              |
                                              v
Flow 3 reads persisted aggregate state --------+
```

The local smoke test after PR #12 confirmed this integration with a real uploaded PDF: indexing reached `Indexed`, persisted chunks were created, and the report updated chapter/document/chunk/indexing metrics without additional report-side writes.

See `docs/member-3-document-indexing-handoff.md` for the indexing implementation details.

## Relationship to pending Flow 2

Flow 3 already reads `ChatSession`, `ChatMessage`, and `MessageCitation`. Until Members 4/5 populate those tables through Flow 2, chat usage metrics correctly remain zero.

Flow 3 does not own:

- question retrieval
- answer generation
- conversation management
- citation creation

Those remain Flow 2 responsibilities.

## Tests and validation

The merged PR added focused Report & Statistics tests covering:

- required authorization attribute/policy usage
- Student exclusion intent
- empty/zero states
- PRN222 subject scoping
- chapter/document aggregation
- index-state grouping
- chunk totals
- recent failure behavior and limits
- chat usage counts
- read-only calculation expectations

The PR reported `75/75` tests passing.

A post-merge local smoke test additionally verified:

- anonymous `/Reports/Index` access redirects to login
- Student access is denied
- Subject Leader access returns the report successfully
- Flow 1 upload/indexing completes through the background worker and Ollama embedding runtime
- Flow 3 reflects the resulting chapter, document, chunk, progress, grouping, and recently indexed data

## Completion criteria

Flow 3 is considered complete because:

1. A Subject Leader can navigate to Reports/Statistics.
2. Server-side authorization blocks anonymous/Student access.
3. The page reads aggregate PRN222 data from the existing application database.
4. It shows document/indexing statistics and chat/session usage statistics.
5. Empty/zero-data states render correctly.
6. It remains read-only.
7. No speculative schema/migration was introduced solely for reporting.
8. Relevant automated tests and local smoke verification cover the merged behavior.

## Future maintenance

Flow 3 should now be treated as a completed workflow, not as pending feature work. Future changes should be small reporting improvements or fixes unless requirements explicitly expand the workflow.

When Flow 2 lands, no Flow 3 redesign should be required: the existing chat counters should begin reflecting persisted chat/session/citation data automatically.
