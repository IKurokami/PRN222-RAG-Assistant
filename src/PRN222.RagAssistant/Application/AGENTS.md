# Application-layer instructions

> Synchronized with `master` after PR #19.

This subtree contains stable cross-workflow contracts/models. Keep it independent from MVC/Razor/Ollama/PostgreSQL-specific presentation details.

## Current workflow state

1. Flow 1 - Document Management & Indexing - complete - MVC.
2. Flow 2 - RAG Q&A + Conversation Management - pending - MVC.
3. Flow 3 - Report & Statistics - complete - Razor Pages.
4. Cross-app UI/UX redesign - complete in PR #19 - Member 3.

The PR #19 UI work does not move presentation-specific dependencies into this Application layer.

## Subject boundary

The application is multi-subject. PRN222 is only the seeded demo subject.

`Document` and `Chapter` persist `SubjectId`. Flow 1 and Flow 3 carry a concrete subject context and authorize via `ISubjectAccessService` in the Security layer.

Do not add a contract that allows retrieval or persistence to silently omit subject context once Flow 2 implementation begins.

Current Flow 2 persistence limitation: `ChatSession` has no `SubjectId`. Before Member 4 implements retrieval/chat persistence, coordinate with Member 1 to add the minimal subject-scoped contract/schema required so sessions, retrieval and citations cannot cross subjects.

## Roles/policies

Global RBAC is Member 1-owned:

```text
ManageUsers     -> Admin
ManageSubjects  -> Admin
ManageDocuments -> Admin OR SubjectLeader
```

`ManageDocuments` is a coarse role gate. Resource/subject permission is separate and cannot be represented by role membership alone.

Public registration introduced in PR #19 creates `Student` accounts only. That presentation/registration implementation must not leak elevated-role selection into Application contracts.

Do not duplicate role strings, managed-subject claim types, or authorization rules inside application services.

## Current integration boundaries

### Flow 1

```text
subject-aware MVC action
   -> persist Document/Chapter
   -> IDocumentIndexingQueue.EnqueueAsync(documentId)
   -> DocumentIndexingWorker
   -> IDocumentIndexingService.IndexAsync(documentId)
```

Indexing remains subject-agnostic at service invocation because the persisted `Document` supplies `SubjectId`.

PR #19 adds UI/filter behavior in the presentation layer only; it does not move parsing/indexing into MVC or Application contracts.

### Flow 2

Required direction:

```text
MVC subject/session context
   -> subject-scoped RAG application boundary
   -> question embedding
   -> pgvector retrieval restricted to Documents of selected Subject
   -> grounded generation
   -> message/citation persistence bound to the same Subject/session
```

Member 4 owns backend implementation. Member 5 owns MVC presentation/evaluation. Member 1 coordinates shared contract/entity/migration changes needed to establish subject ownership.

Member 5 should reuse Member 3's PR #19 visual system, but that visual system remains outside the Application layer.

### Flow 3

No reporting-specific shared contract is required. Flow 3 reads persistence directly with aggregate EF queries and is subject-scoped for document/index metrics.

PR #19 report redesign remains presentation-only.

## Shared contracts

- `IDocumentIndexingQueue`: request-to-background handoff.
- `IDocumentIndexingService`: one-document indexing pipeline.
- `ITextEmbeddingService`: provider-neutral single/batch embedding.
- `IChatCompletionService`: provider-neutral generation boundary.
- `IRagQueryService`: presentation-facing grounded Q&A boundary; update before implementation if its current signature cannot enforce subject context.
- `RagAnswer` / `RagCitation`: presentation-safe RAG result models.

Prefer additive changes. If a signature must change, update all affected producers/consumers in the same integration window and report the change to Member 1 for documentation synchronization.

## Ownership

- Member 1: shared contracts, Core/Data/Identity/RBAC/multi-subject, schema coordination, all docs.
- Member 2: Flow 1 request/business behavior + Flow 3 reporting behavior.
- Member 3: indexing implementation + completed cross-app UI/UX redesign from PR #19.
- Member 4: pending Flow 2 backend.
- Member 5: pending Flow 2 MVC/history/citations/evaluation.

Members 2-5 do not independently edit README/AGENTS/docs.

## Dependency rules

- Application abstractions do not depend on MVC Controller, Razor PageModel, HttpContext, Ollama DTOs, Npgsql query types, CSS, JS, or front-end component libraries.
- Infrastructure may implement Application abstractions.
- Flow 1 controllers do not parse/chunk/embed/call providers.
- Flow 2 MVC does not call Ollama or pgvector directly.
- Flow 3 does not mutate workflow state or call provider/retrieval code.
- Do not create duplicate contracts inside feature folders.
