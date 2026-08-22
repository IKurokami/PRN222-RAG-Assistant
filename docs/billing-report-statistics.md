# Billing Report & Statistics

> Added on 2026-08-22 after the VNPay quota-purchase integration landed on `master` in PR #53, and synchronized with the verified Return fallback semantics from PR #56.

## Goal

Payment data creates a new class of operational/business signals that should not be mixed blindly into the subject-scoped academic report.

The existing `/Reports?subjectId=...` report remains subject-scoped. Billing purchases currently grant account-level RAG quota and the checkout page creates `PaymentOrder` with `SubjectId = null`, so the application must not label global revenue as revenue for whichever subject an Admin happens to be viewing.

Billing analytics therefore lives at:

```text
Pages/Reports/Billing.cshtml.cs
 -> IBillingReportQueryService
 -> BillingReportQueryService
 -> ApplicationDbContext
```

Route: `/Reports/Billing`

The page is read-only and **Admin-only**. Subject Leaders keep access to their authorized subject reports but do not receive system-wide revenue, payment-channel or user-quota aggregates.

## Metrics

| Metric | Formula / source | What it tells us | Important limitation |
|---|---|---|---|
| Confirmed revenue | Sum `PaymentOrder.Amount` where persisted `Status == Paid` | Revenue represented by orders finalized through verified VNPay callback processing; IPN is preferred and a verified successful Return may finalize as fallback | Sandbox transactions are demo data, not accounting statements |
| Paid / Pending / Failed | Count by persisted order status | Checkout/payment ledger health | Pending may still be in an active checkout window |
| Stale pending | Pending orders older than 30 minutes | Likely abandoned/unreconciled checkout attempts after the 15-minute VNPay expiry window plus safety buffer | Does not prove why the user abandoned payment |
| Settled payment success | `Paid / (Paid + Failed)` | Success among orders that reached a terminal state | Excludes pending/abandoned attempts |
| Checkout completion | `Paid / (Paid + Failed + stale pending)` | Stricter proxy including likely abandonment | Stale pending is a heuristic, not a VNPay terminal status |
| Unique paying users | Distinct `UserId` among Paid orders | Breadth of monetized usage | Not retention or lifetime value |
| Purchased quota | Sum immutable `MetadataJson.quotaUnits` for Paid orders | Capacity sold through successful purchases | Paid rows with malformed/missing quota metadata are excluded and surfaced separately |
| Effective revenue per quota | Revenue from Paid orders with valid quota metadata / purchased quota units | Detects price/discount changes and effective unit price | Not provider inference cost or profit margin |
| Average Paid order value | Confirmed revenue / Paid orders | Typical transaction size | Sensitive to sample size and package mix |
| Package mix | Paid orders grouped by immutable `quotaUnits` | Which quota package sizes users actually purchase | Does not infer a plan name that was not persisted |
| Bank / card-type mix | Paid orders grouped by callback `BankCode` / `CardType` | Which VNPay channels are used | Values depend on what VNPay returns in sandbox/production |
| 7-day billing series | Created orders, Paid orders, confirmed revenue and purchased quota by UTC day | Short-term checkout and revenue trend | UTC presentation can differ from Vietnam business-day reporting |
| Current outstanding quota | Sum current positive `ApplicationUser.QuotaRemaining` | Current system capacity available to users | Includes free/seed quota; it is **not** “unspent purchased quota” |
| Subject attribution coverage | Paid orders with vs without `SubjectId` | Whether the ledger can support subject-level revenue analysis | Current checkout buys global quota and normally leaves `SubjectId = null` |
| Paid rows with invalid quota metadata | Paid orders whose `quotaUnits` cannot be parsed | Ledger/data-integrity warning | Revenue remains counted; quota/package metrics exclude these rows |

## Why the scope split matters

`PaymentOrder` has an optional `SubjectId`, but the current checkout intentionally passes `null` because quota is account-level rather than subject-specific. A report that grouped these purchases under the currently selected subject would create false academic/business attribution.

If product requirements later introduce subject-specific quota or a checkout that deliberately records the subject being purchased for, subject-level financial metrics can be added only after the semantics are explicit and tested.

## Security and privacy

- `/Reports/Billing` uses `[Authorize(Roles = AppRoles.Admin)]`.
- The page does not display user IDs, emails or names in the recent-order table.
- The query is read-only and does not call VNPay, mutate order state or grant/consume quota.
- Only persisted `Paid` state is treated as confirmed revenue. A browser-only display result is not counted; however, after PR #56 a fully verified successful VNPay Return may atomically persist the same `Pending -> Paid` transition as a fallback when IPN is missing.
- No VNPay secret or callback signature data is exposed.

## Data-quality rules

- A fresh Pending order is not counted as failed.
- A Pending order becomes a reporting warning only after 30 minutes; the checkout itself still uses the configured 15-minute VNPay expiry.
- Malformed quota metadata never makes the report fail. The order remains in financial counts, while quota-dependent metrics omit it and increment `PaidOrdersMissingQuotaMetadata`.
- Effective revenue per quota uses only Paid rows with valid quota metadata, avoiding a misleading denominator when ledger metadata is damaged.

## Regression coverage

`BillingReportQueryServiceTests` verifies:

- the Billing report PageModel is Admin-only;
- the PageModel depends on `IBillingReportQueryService`, not `ApplicationDbContext`;
- Paid-only revenue and quota aggregation;
- settled success versus stale-pending-aware checkout completion;
- package mix, bank-channel mix and subject-attribution coverage;
- malformed quota metadata handling;
- current outstanding quota aggregation;
- seven-day billing activity.
