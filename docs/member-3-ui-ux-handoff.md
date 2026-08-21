# Member 3 handoff - Cross-application UI/UX baseline

> Cross-app baseline completed in PR #19; synchronized with later Flow 2/3 changes on 2026-08-21.

## Completed baseline

Member 3 delivered the shared application presentation baseline, including:

- landing page and application shell redesign;
- shared design tokens/components/site styling;
- Bootstrap Icons integration;
- authentication/account presentation;
- Subject/Admin/Chapter/Document/Report screen refresh;
- public Student registration presentation;
- responsive application-shell polish.

## Ownership boundary after Flow 2 completion

PR #34/#35 later added a specialized full-screen Flow 2 Chat experience and Evaluation UI under Member 5's product scope. That work builds on the application shell but introduces purpose-specific Chat layout/interaction patterns such as:

- session sidebar and subject switcher;
- borderless chat workspace;
- Markdown rendering and code copy;
- SSE progress/typewriter rendering;
- citation pills and citation reader;
- tool/progress timeline.

Those Flow 2 changes do not transfer the original cross-app UI baseline ownership away from Member 3.

## Current functional ownership

- Member 1: security/multi-subject/provider/shared integration/docs.
- Member 2: Flow 1 request behavior + Flow 3 reporting behavior.
- Member 3: indexing maintenance + cross-app visual baseline.
- Member 4: Flow 2 RAG backend maintenance.
- Member 5: completed Flow 2 MVC Chat/history/citations/evaluation product layer.

## Design-system rule

For normal application screens, reuse the shared design system before introducing one-off styles:

```text
wwwroot/css/design-tokens.css
wwwroot/css/components.css
wwwroot/css/site.css
libman.json -> bootstrap-icons
```

Flow 2 may preserve its specialized current chat layout where appropriate, but should still maintain responsive/accessibility behavior and application-wide identity.

## Flow-specific notes

### Flow 1

Visual changes must preserve subject context, CRUD/re-index semantics, authorization and indexing boundaries.

### Flow 2

Chat is now complete MVC. Do not recreate an internal `RagDemo` page. The current product uses SSE over fetch, not SignalR.

### Flow 3

Reports are read-only and subject-scoped. PR #40 also scopes Chat metrics to the report subject; the old transitional/global warning is obsolete.

## Documentation

Future UI changes with architecture/status impact should be reflected through the canonical documentation synchronization process.
