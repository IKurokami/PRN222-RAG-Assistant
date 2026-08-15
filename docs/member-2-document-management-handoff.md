# Member 2 handoff - Flow 1 Document/Chapter Management

> Updated for provider-neutral indexing integration.

## Status

Flow 1 request/business behavior is complete and uses MVC.

Member 2 owns established business behavior. Member 1 owns multi-subject/RBAC/provider configuration. Member 3 owns indexing and current visual redesign.

## Current behavior

- subject-scoped Document list/filter;
- PDF/DOCX/PPTX upload validation, 50 MB maximum;
- source-file/metadata persistence;
- subject-scoped Chapter CRUD;
- details/edit/delete/re-index;
- safe Chapter deletion by unassigning affected Documents;
- queue handoff through `IDocumentIndexingQueue`.

## Authorization

Writes require `AppPolicies.ManageDocuments` plus concrete Subject authorization via `ISubjectAccessService`.

## AI provider boundary

Flow 1 request code does not know whether indexing uses Ollama, Gemini, or OpenAI.

```text
Controller -> IDocumentIndexingQueue -> Member 3 indexing -> ITextEmbeddingService
```

Provider selection/config/API keys are Member 1-owned. Controllers must never call provider endpoints or read provider API keys.

Changing the embedding provider/model requires a complete corpus re-index, but it does not change upload/CRUD/re-index request semantics.

## Documentation

Member 2 reports changes to Member 1 instead of independently editing coordination docs.
