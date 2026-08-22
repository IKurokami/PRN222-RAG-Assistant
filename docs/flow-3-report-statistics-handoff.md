# Flow 3 handoff - Report & Statistics

> Updated on 2026-08-22 for the academic dashboard plus Admin-only billing analytics added after the VNPay quota-purchase integration.

## Status

Flow 3 remains read-only and Razor Pages-based under `Pages/Reports/`, with two deliberately different scopes:

- academic/RAG reporting is subject-scoped;
- billing/quota analytics is system-wide and Admin-only.

This split prevents account-level quota purchases from being falsely attributed to whichever Subject happens to be open in the academic report.

## Architecture

PageModels do not access `ApplicationDbContext`/EF Core directly.

```text
Academic report
Pages/Reports/Index.cshtml.cs
 -> IReportQueryService
 -> ReportQueryService
 -> ApplicationDbContext

Billing analytics
Pages/Reports/Billing.cshtml.cs
 -> IBillingReportQueryService
 -> BillingReportQueryService
 -> ApplicationDbContext
```

Application provides presentation-safe read models through `SubjectReportSnapshot` and `BillingReportSnapshot`.

`Program.cs` registers both boundaries with `AddReporting()`.

## Authorization and scope

### Academic report

Requires:

1. coarse `ManageDocuments` policy; and
2. `ISubjectAccessService.CanManageSubjectAsync` for the concrete Subject.

Admin can view any Subject; a Subject Leader can view only assigned Subjects; Student has no report access.

### Billing analytics

`/Reports/Billing` requires the `Admin` role. It does not expose system-wide revenue, payment-channel or quota aggregates to Subject Leaders.

The current checkout purchases account-level RAG quota and passes `SubjectId = null` into `CreateBillingOrderRequest`. Although `PaymentOrder.SubjectId` is available for future explicit attribution, current global purchases must not be presented as Subject revenue.

## Subject-scoped academic metrics

The academic snapshot includes:

- total Chapters and Documents;
- unassigned Documents and Documents by Chapter;
- Uploaded/Processing/Indexed/Failed counts;
- total DocumentChunks and average chunks per indexed Document;
- recent indexing failures and recently indexed Documents;
- ChatSession/ChatMessage activity for the Subject;
- user question and assistant response counts;
- active sessions in the last 7 and 30 days;
- daily message/citation trend;
- citation coverage and average citations per assistant response;
- unique cited Documents and cited indexed-document coverage;
- indexed-but-never-cited Documents;
- top cited Documents and Chapters;
- top-three citation concentration.

Chat aggregates remain explicitly constrained through `ChatSession.SubjectId`.

## Academic interpretation rule

Observable citation/usage statistics are **not** renamed into semantic RAG quality metrics.

- citation coverage is not faithfulness;
- cited-source coverage is not context recall;
- citation count is not correctness;
- high source popularity is not proof of source quality.

Faithfulness, context precision/recall, answer relevance/correctness and similar semantic metrics require the Evaluation workflow with suitable ground truth and/or judge-based scoring.

Detailed academic metric definitions remain in `report-statistics-metrics.md`.

## Billing and quota metrics

The Admin billing snapshot adds operational/business signals made possible by `PaymentOrder` and user quota persistence:

- total/Paid/Pending/Failed orders;
- confirmed Paid revenue and 30-day Paid revenue;
- average Paid order value;
- settled payment success rate;
- checkout completion proxy including stale Pending attempts;
- Pending orders older than 30 minutes;
- unique paying users;
- purchased quota units from immutable `MetadataJson.quotaUnits`;
- average quota units per Paid order;
- effective revenue per quota unit;
- Paid rows with missing/malformed quota metadata;
- quota package mix;
- BankCode and CardType mix for Paid transactions;
- seven-day order/Paid/revenue/quota trend;
- Subject-attributed vs unattributed Paid orders;
- current users with positive quota and total outstanding quota;
- recent order ledger without user IDs, names or emails.

Only persisted `Status == Paid` is counted as confirmed revenue/quota sales. Fresh Pending orders are not treated as failures. A 30-minute stale-Pending threshold is a reporting heuristic, not a VNPay terminal state.

Current outstanding quota includes free/initial quota as well as purchased quota, so it is intentionally **not** labeled “unused purchased quota”.

Detailed billing formulas and limitations are documented in `billing-report-statistics.md`.

## UI behavior

The academic page keeps its existing corpus/RAG dashboard and shows an Admin-only navigation button to Billing Analytics.

The Billing Analytics page presents:

- revenue/order/quota KPI cards;
- data-integrity warnings for invalid quota metadata;
- seven-day billing activity bars;
- checkout/quota health summary;
- quota package mix;
- payment bank/card-type mix;
- Subject-attribution coverage;
- recent non-PII order rows.

Both pages use the existing Bootstrap/project design system and Razor Page routing; no client-side charting dependency is added.

## Provider and mutation boundary

Reports remain provider-independent and read-only. They do not:

- call VNPay;
- confirm/fail an order;
- grant or consume quota;
- perform embedding/retrieval/chat completion;
- mutate academic content.

The VNPay IPN/payment service remains the authoritative write path for Paid state and quota grants.

## Tests

Academic regression coverage continues to verify subject isolation and report aggregation.

`BillingReportQueryServiceTests` additionally verifies:

- Billing report is Admin-only;
- PageModel depends on `IBillingReportQueryService`, not `ApplicationDbContext`;
- Paid-only revenue aggregation;
- settled success vs stale-Pending-aware checkout completion;
- quota metadata parsing and damaged-metadata handling;
- package and payment-channel mix;
- Subject-attribution coverage;
- current quota aggregation;
- seven-day activity aggregation without UTC date-boundary flakiness.

## Ownership / contribution

- Member 2 retains Flow 3 reporting behavior ownership.
- Member 1 owns cross-cutting subject/RBAC/shared-contract/documentation coordination.
- Billing integration/reporting work should be credited from actual merged PR authorship/review history rather than inferred from nominal ownership.

Canonical contribution accounting: `member-contributions.md`.
