# Member 3 handoff - Cross-application UI/UX baseline

> Updated on 2026-08-21 for the Razor Pages + SignalR target architecture.

## Completed baseline

Member 3 delivered the shared application presentation baseline, including the application shell, shared design tokens/components, authentication presentation, refreshed workflow screens, and responsive polish.

## Presentation target

All normal HTTP UI/action surfaces must converge on Razor Pages.

```text
Account/authentication -> Razor Pages
Admin users/subjects    -> Razor Pages
Subject catalogue      -> Razor Pages
Documents/Chapters     -> Razor Pages
Chat                   -> Razor Pages + SSE
Evaluation             -> Razor Pages
Reports                -> Razor Pages
```

The follow-up implementation migration must preserve the existing design system while removing duplicate/legacy MVC product presentation after parity is verified.

## Flow 1 realtime UX

Document Management adds SignalR notifications for connected authorized browsers.

Expected UI behaviors:

- newly created Documents can appear without manual refresh;
- edited Document metadata/status can update the matching row;
- deleted Documents can disappear from the current list;
- indexing state transitions can update status indicators;
- the UI can fall back to re-fetch/reload when an event payload is intentionally minimal;
- reconnect behavior handles transient disconnects/deploys.

SignalR is not the write path. Forms/fetch operations still call Razor Page handlers; realtime events only reflect successful server-side changes.

## Flow 2

Chat is already Razor Pages after PR #42 and keeps its specialized full-screen layout and SSE progress/typewriter behavior.

Do not move Chat to SignalR merely because Document Management uses SignalR.

Evaluation should migrate to Razor Pages while preserving its existing visual/functionality baseline.

## Design-system rule

For normal application screens, reuse:

```text
wwwroot/css/design-tokens.css
wwwroot/css/components.css
wwwroot/css/site.css
libman.json -> bootstrap-icons
```

The Document SignalR JavaScript client is an additional runtime client dependency in the implementation PR; it should integrate with the existing page UX rather than introduce a separate SPA architecture.

## Accessibility/responsiveness

Realtime updates must not reduce usability:

- preserve keyboard/focus behavior;
- expose meaningful status changes accessibly where practical;
- do not reorder the page unexpectedly without a clear rule;
- keep no-JavaScript/server-rendered behavior functional enough to recover via normal navigation/refresh.

## Functional ownership

- Member 1: security/multi-subject/provider/shared integration/docs.
- Member 2: Flow 1 request behavior + Flow 3 reporting behavior.
- Member 3: indexing maintenance + cross-app visual baseline.
- Member 4: Flow 2 RAG backend maintenance.
- Member 5: Flow 2 product presentation/evaluation behavior.

See `razor-pages-signalr-architecture.md` for the migration contract.
