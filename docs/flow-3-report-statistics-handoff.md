# Flow 3 handoff - Report & Statistics

> Synchronized after PR #30 merged on 2026-08-18.

## Status

Flow 3 is complete and remains a read-only Razor Pages workflow under `Pages/Reports/`.

Member 2 owns report behavior. Member 1 owns cross-cutting subject/RBAC/provider coordination. Member 3 owns the current visual baseline.

## Subject-scoped access

Reports require `ManageDocuments` plus `ISubjectAccessService.CanManageSubjectAsync` for the concrete Subject.

## Subject-scoped document/index metrics

- total Chapters/Documents;
- unassigned Documents;
- Documents by Chapter;
- Uploaded/Processing/Indexed/Failed counts;
- total DocumentChunks;
- recent indexing failures;
- recently indexed Documents and chunk counts.

## AI provider boundary

Reports remain provider-independent. They must not call embedding/chat providers, run similarity retrieval or mutate workflow state.

## Chat metrics after PR #30

`ChatSession.SubjectId` now exists and the Member 4 backend is subject-aware.

Existing Flow 3 chat session/message/citation aggregates were originally implemented before that field existed, so they should be audited when Member 5 completes the final Flow 2 MVC product layer. The target state is explicit subject-scoped chat metrics rather than relying on legacy global totals.

This audit is follow-up integration work; it does not change the completed read-only Flow 3 document/indexing metrics.

## Ownership / contribution

- Member 2: Flow 3 behavior and report implementation.
- Member 1: subject/RBAC integration and documentation coordination.
- Member 3: visual baseline.
- Member 4: subject-scoped Flow 2 backend that now provides the chat subject context consumed by future report updates.
- Member 5: pending final Flow 2 MVC/evaluation integration.

Canonical contribution accounting: `docs/member-contributions.md`.

Project documentation uses Member numbers only and must not add GitHub usernames.
