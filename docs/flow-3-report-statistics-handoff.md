# Flow 3 handoff - Report & Statistics

> Synchronized with `master` after PR #19.

## Status

Flow 3 is complete and remains a read-only Razor Pages workflow under `Pages/Reports/`.

Member 2 owns report behavior. Member 1 owns cross-cutting subject/RBAC integration. Member 3 completed the current visual redesign in PR #19.

## Subject-scoped access

Reports are opened with a concrete `subjectId`.

Authorization:

```text
ManageDocuments -> Admin OR SubjectLeader
AND
ISubjectAccessService.CanManageSubjectAsync(user, subjectId)
```

Admin can report on any Subject. Subject Leader can report only on assigned Subjects.

## Subject-scoped metrics

- total Chapters;
- total Documents;
- unassigned Documents;
- Documents by Chapter;
- Uploaded/Processing/Indexed/Failed counts;
- total DocumentChunks;
- recent indexing failures;
- recently indexed Documents and chunk counts.

`ChapterDocumentCountViewModel` now carries the Chapter ID used by the refreshed report presentation while preserving the same read-only reporting semantics.

## PR #19 presentation update

Member 3 refreshed the Reports UI as part of the application-wide design system rollout.

This does not change the report ownership or data boundaries. Reports remain subject-scoped, read-only, and provider-independent.

## Transitional chat metrics

Chat session/message/citation totals remain **global** because Flow 2 is pending and `ChatSession` currently has no `SubjectId`.

Do not mislabel these values as subject-scoped. Once Flow 2 introduces subject-owned sessions, Flow 3 should filter chat metrics by Subject as a follow-up integration.

## Boundaries

Reports must not:

- mutate workflow state;
- enqueue/re-index documents;
- call Ollama;
- run similarity retrieval;
- duplicate Conversation History;
- add speculative analytics schema.

## PRN222 status

PRN222 is a seeded demo Subject, not the report's hard-coded scope.

## Ownership/documentation

- Member 2 reports future report-behavior changes to Member 1.
- Member 3 reports future presentation changes to Member 1.
- Member 1 keeps repository docs synchronized.
