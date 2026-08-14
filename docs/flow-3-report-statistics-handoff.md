# Flow 3 - Report & Statistics handoff

## Purpose and status

Flow 3 - Report & Statistics is the completed independent third workflow merged through PR #12.

Presentation allocation:

1. **Flow 1 - Document Management & Indexing** - complete - MVC
2. **Flow 2 - RAG Question & Answer & Conversation Management** - pending - MVC
3. **Flow 3 - Report & Statistics** - complete - Razor Pages

Conversation History belongs to Flow 2.

## Ownership

- Member 2 owns Flow 3 reporting behavior and its Razor Pages implementation.
- Member 1 owns global roles/policies, role-aware shared UI, authorization regression tests, and all repository documentation including this handoff.

Member 2 should report future Flow 3 status/doc changes to Member 1 instead of editing repository docs directly.

## Implementation

```text
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml
src/PRN222.RagAssistant/Pages/Reports/Index.cshtml.cs
tests/PRN222.RagAssistant.Tests/ReportStatisticsTests.cs
```

The page is protected by:

```text
[Authorize(Policy = AppPolicies.ManageDocuments)]
```

Current policy mapping:

```text
ManageDocuments -> Admin OR SubjectLeader
```

Admin access is an operational override. Subject Leader remains the normal academic actor. Student cannot access Reports.

Canonical role design: `docs/role-access-control.md`.

## Available metrics

- chapter/document totals;
- document counts by indexing state;
- documents by chapter/unassigned;
- `IndexedAtUtc` and `IndexError`;
- `DocumentChunk` totals;
- recently indexed documents with chunk counts;
- indexing completion percentage;
- chat session/message/citation totals.

Chat usage remains zero/empty until Flow 2 begins persisting rows.

## Flow definition

Primary academic actor: **Subject Leader**. Administrative override actor: **Admin**.

```text
Admin or Subject Leader
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

- `Chapter`;
- `Document`;
- `DocumentChunk`;
- `ChatSession`;
- `ChatMessage`;
- `MessageCitation`.

No reporting-specific entity or migration is required. Queries use focused EF Core aggregates and `AsNoTracking()` where appropriate. PostgreSQL is the source of truth.

## Non-interference rules

Future Flow 3 maintenance must not:

- enqueue/re-index documents;
- modify indexing worker/parser/chunker/embedding behavior;
- mutate document index state or workflow rows;
- perform pgvector similarity retrieval;
- call Ollama;
- duplicate Flow 2 chat/session/history UI;
- create speculative analytics schema;
- change shared contracts solely for reporting convenience;
- redefine global roles/policies inside the reporting feature.

If a persistence or authorization gap is genuine, coordinate through Member 1.

## Relationship to Flow 1

```text
Flow 1 MVC request side
Document -> queue -> worker -> parse/chunk/embed -> persisted index state
                                                    |
                                                    v
Flow 3 reads persisted aggregate state -------------+
```

## Relationship to Flow 2

Flow 3 may read `ChatSession`, `ChatMessage`, and `MessageCitation` after Flow 2 populates them, but it does not own question retrieval, answer generation, Conversation History, citations, or MVC chat presentation.

## Acceptance criteria

1. Admin or Subject Leader can navigate to Reports/Statistics - complete after RBAC policy extension.
2. Student is denied server-side - required regression behavior.
3. The page reads aggregate PRN222 data from the database - complete.
4. It shows document/indexing/chat usage statistics - complete.
5. Empty/zero states render correctly - complete.
6. It remains read-only - complete.
7. No speculative reporting schema/migration exists - complete.
8. Relevant tests cover aggregates and policy access - required baseline.

## Maintenance guidance

Do not recreate `feature/report-statistics` as a competing implementation. New product work should focus on pending Flow 2 unless a requirement explicitly reopens reporting.

Documentation updates for Flow 3 are performed by Member 1.
