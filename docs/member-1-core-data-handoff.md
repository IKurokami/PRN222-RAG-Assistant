# Member 1 handoff - Core/Data/RBAC/Multi-subject/AI Providers/Documentation

> Updated on 2026-08-21 after PR #42/#43 and the accepted Razor Pages + SignalR target architecture.

## Ownership

Member 1 owns the cross-cutting platform/integration scope:

- Domain/Data/Security baseline;
- Identity roles and policies;
- shared Application contracts and schema/migration coordination;
- Admin user/role behavior;
- Subject catalogue + Admin Subject behavior;
- Subject Leader assignment;
- subject-specific authorization service;
- cross-workflow subject-context integration;
- AI provider selection/configuration;
- deployment/configuration integration;
- Data Protection persistence coordination;
- repository-wide documentation synchronization.

## Presentation architecture coordination

The accepted target is:

```text
HTTP UI/actions               -> Razor Pages only
Chat progress/result          -> SSE
Document Management realtime -> SignalR notifications
```

Chat is already Razor Pages after PR #42/#43. Remaining legacy MVC product/admin surfaces are code migration debt and must not be described as the final architecture.

Member 1 coordinates the cross-cutting rules for the implementation migration:

- preserve role/subject authorization;
- avoid duplicate MVC + Razor Page product surfaces;
- remove controller routing only after parity is verified;
- keep PageModels behind purpose-specific application boundaries where practical;
- keep SignalR as realtime fan-out rather than the CRUD API;
- keep Chat SSE separate from Document SignalR;
- reconcile canonical docs after implementation merges.

## Provider/runtime milestones

Representative provider/infrastructure work includes PR #21, #28, #37, #38 and #39.

Current Render provider split:

```text
Chat:       Gemini / gemini-3.6-flash
Embedding:  OpenRouter / nvidia/llama-nemotron-embed-vl-1b-v2:free
Dimensions: 1024
```

## Data Protection

ASP.NET Core Data Protection keys persist in PostgreSQL through `DataProtectionKeyDbContext`. This should not be reverted to filesystem-only storage for Render.

## Embedding migration rule

Changing embedding provider/model/dimension requires complete corpus re-indexing. PR #37 allows different-dimension rows to coexist temporarily during transition but does not create semantic compatibility between different embedding models.

## Multi-subject baseline

```text
Subject
  -> Chapters
  -> Documents
  -> ChatSessions
  -> Reports
  -> Document realtime group
```

Roles:

```text
Admin
SubjectLeader
Student
```

Policies:

```text
ManageUsers
ManageSubjects
ManageDocuments
```

Document SignalR subscriptions must enforce the same concrete subject boundary as Razor Page management actions.

## Report integration

Reports remain:

```text
Report PageModel
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext
```

Chat/report totals stay subject-scoped.

## Cross-workflow boundary

- Member 2: Flow 1 request/business behavior + Flow 3 reporting behavior.
- Member 3: indexing/ingestion maintenance + cross-app UI baseline.
- Member 4: Flow 2 RAG backend maintenance.
- Member 5: Flow 2 product presentation/evaluation behavior.
- Member 1: shared contracts/schema/security/provider/deployment/docs coordination.

Historical MVC implementation credit remains in `member-contributions.md`; historical credit does not define the future presentation architecture.

## Documentation responsibility

Canonical docs must distinguish implemented runtime state from accepted target state. This docs PR does not count as completing the code migration.
