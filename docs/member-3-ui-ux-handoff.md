# Member 3 handoff - UI/UX Redesign

> Status: **Complete / merged in PR #19** on 2026-08-15.

## Assignment

The application-wide UI/UX redesign delivered in PR #19 is assigned to **Member 3**.

This task is completed. It must not appear in future planning as unassigned work unless a new UI task is explicitly created.

## Completed scope

Member 3 delivered the current presentation baseline across the application:

- redesigned landing page and application shell;
- shared `wwwroot/css/design-tokens.css` design tokens;
- shared `wwwroot/css/components.css` component primitives;
- refreshed `wwwroot/css/site.css` integration;
- Bootstrap Icons through `libman.json`;
- redesigned Login, Register, Logout, AccessDenied, Error, and Privacy screens;
- public Student registration introduced with the redesigned auth experience;
- refreshed Subject catalogue;
- refreshed Admin User and Admin Subject management screens;
- refreshed Chapter and Document MVC screens;
- refreshed Flow 3 Reports presentation;
- document title/file-name search and index-status filtering UI;
- preservation of current search/status/chapter filters after delete/re-index actions;
- landing showcase carousel, testimonials, FAQ, CTA/support presentation, and local image/video assets.

## Ownership boundary

This is a presentation/UI assignment. Existing functional ownership remains:

- Member 1: Identity/RBAC, multi-subject authorization, shared contracts/schema coordination, docs;
- Member 2: Flow 1 request/business behavior and Flow 3 reporting behavior;
- Member 3: indexing implementation plus this completed cross-app UI/UX redesign;
- Member 4: pending Flow 2 backend;
- Member 5: pending Flow 2 MVC/history/citations/evaluation.

PR #19 may contain small supporting behavior required by the redesigned experience, but it does not transfer authorization or workflow ownership away from the members above.

## Public registration rule

The public registration flow creates only `Student` accounts.

Do not expose Admin or SubjectLeader role selection in public registration. Elevated roles remain Admin-managed.

## Design-system rule for future work

Future UI work should reuse the PR #19 baseline before creating new patterns:

```text
wwwroot/css/design-tokens.css
wwwroot/css/components.css
wwwroot/css/site.css
libman.json -> bootstrap-icons
```

Guidelines:

- prefer reusable tokens/components over one-off CSS;
- preserve responsive behavior;
- preserve semantic/accessibility attributes;
- do not hide authorization logic in UI-only checks;
- keep MVC/Razor allocation unchanged by styling decisions;
- future Flow 2 MVC screens should follow this visual language.

## Flow-specific notes

### Flow 1

Visual changes must preserve subject context, upload/CRUD/re-index semantics, authorization, and indexing queue boundaries.

### Flow 3

Visual changes must preserve the read-only, subject-scoped report behavior. Chat metrics remain transitional/global until Flow 2 persists subject ownership.

### Flow 2

Member 5 owns future Flow 2 MVC presentation. Member 5 should integrate with this design system rather than create a parallel UI framework.

## Documentation

Member 3 reports future UI changes to Member 1. Member 1 remains the sole editor of README/AGENTS/docs.
