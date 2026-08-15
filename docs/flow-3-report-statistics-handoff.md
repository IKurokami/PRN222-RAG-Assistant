# Flow 3 handoff - Report & Statistics

> Updated for AI-provider backup support.

## Status

Flow 3 is complete and remains a read-only Razor Pages workflow under `Pages/Reports/`.

Member 2 owns report behavior. Member 1 owns cross-cutting subject/RBAC/provider coordination. Member 3 owns the current visual redesign.

## Subject-scoped access

Reports require `ManageDocuments` plus `ISubjectAccessService.CanManageSubjectAsync` for the concrete Subject.

## Subject-scoped metrics

- total Chapters/Documents;
- unassigned Documents;
- Documents by Chapter;
- Uploaded/Processing/Indexed/Failed counts;
- total DocumentChunks;
- recent indexing failures;
- recently indexed Documents and chunk counts.

## AI provider boundary

Reports remain **provider-independent**.

They must not:

- call `ITextEmbeddingService`;
- call `IChatCompletionService`;
- call Ollama/Gemini/OpenAI directly;
- run similarity retrieval;
- mutate workflow state.

A provider switch may cause documents to be re-indexed, and reports can naturally reflect the resulting index status/chunk counts, but reports do not orchestrate that process.

## Transitional chat metrics

Chat session/message/citation totals remain global because Flow 2 is pending and `ChatSession` currently has no `SubjectId`.

## Ownership/documentation

Member 1 keeps repository docs synchronized.
