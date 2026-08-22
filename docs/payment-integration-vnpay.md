# VNPay billing integration

## Scope

The application uses VNPay Payment Gateway sandbox for one-time purchases of RAG query quota. Billing is optional and is disabled by default outside configured deployments.

## Configuration

Billing is controlled by:

- `Billing:Enabled` — enables payment creation and callback processing.
- `Billing:BaseUrl` — public merchant base URL used to build the Return URL.
- `Billing:VnPay:TmnCode` — merchant terminal code.
- `Billing:VnPay:HashSecret` — HMAC secret; store only in environment/secret storage.
- `Billing:VnPay:BaseUrl` — sandbox or production payment endpoint.
- `Billing:VnPay:Version`, `Command`, `Locale`, `OrderType` — VNPay payment parameters.

There is no separate `Sandbox` switch: environment selection is explicit through `BaseUrl` and credentials. The IPN route is fixed at `/Billing/Webhook`, so there is no unused webhook-path setting.

Local development keeps `Billing:Enabled=false`, which means VNPay credentials are not required just to start the application. Render enables billing and supplies the hash secret as a secret environment variable.

## Payment creation

`/Billing/Create` exposes server-defined plans. A plan contains both its VND price and its query quota. The selected `QuotaUnits` is persisted immutably in `PaymentOrder.MetadataJson` when the order is created. Payment completion never derives quota from price, so discounts or future price changes cannot silently alter the purchased quota.

The generated VNPay request follows the PAY specification:

- `vnp_Amount = order.Amount * 100`.
- `vnp_CreateDate` and `vnp_ExpireDate` use GMT+7 and `yyyyMMddHHmmss`.
- `vnp_ExpireDate` is 15 minutes after creation.
- `vnp_OrderInfo` is ASCII/no-diacritics, excludes special characters, and is limited to 255 characters.
- `vnp_ReturnUrl` is an absolute URL.
- `vnp_IpAddr` uses ASP.NET Core's resolved remote address; Render forwarded headers are processed before HTTPS redirection.
- HMAC-SHA512 signatures are generated server-side only.

## Return URL versus IPN

The two callbacks have deliberately different responsibilities.

### Return URL

`/Billing/Return` is presentation-only. It verifies the VNPay signature, terminal code and exact amount, then displays the reported result. It does **not** update `PaymentOrder` and does **not** grant quota. This follows VNPay's guidance that Return URL should check integrity and display the payment result only.

### IPN

`/Billing/Webhook` is authoritative. It:

1. verifies HMAC using a fixed-time comparison;
2. validates merchant terminal code;
3. finds the order by `vnp_TxnRef`;
4. requires exact `vnp_Amount = Amount * 100`;
5. treats payment as successful only when both `vnp_ResponseCode` and `vnp_TransactionStatus` are `00`;
6. atomically claims `Pending -> Paid` and grants the stored quota in the same PostgreSQL transaction;
7. handles failed transactions by atomically claiming `Pending -> Failed`;
8. rejects duplicate/concurrent callbacks after the first state transition.

The IPN response codes follow the VNPay sample contract:

- `00` — recorded successfully;
- `01` — order not found;
- `02` — already confirmed/processed;
- `04` — invalid amount;
- `97` — invalid signature;
- `99` — other/internal input or processing error.

VNPay retries IPN for error responses, so transactional rollback leaves a recoverable `Pending` order if quota credit cannot be completed (for example, if the user record is unexpectedly missing).

## Quota concurrency

Interactive RAG queries are exposed through `QuotaAwareRagQueryService`. Before a query starts, `UserQuotaService.ReserveQuotaAsync` performs an atomic PostgreSQL update:

`QuotaRemaining = QuotaRemaining - 1 WHERE UserId = ... AND QuotaRemaining > 0`.

Only one request can reserve the last quota unit. The existing `RagQueryService` marks the reservation committed through `ConsumeQuotaAsync` only after the answer is persisted successfully. If provider/retrieval/streaming fails or enumeration is cancelled before completion, disposing the reservation atomically restores one quota unit.

Quota grants also use database-side increments instead of read-modify-write, avoiding lost updates when payments or refunds happen concurrently.

## Data invariants

`PaymentOrder.UserId` is intentionally kept as a scalar ledger identifier rather than an Identity navigation. The service enforces the important payment invariant at both boundaries: the user must exist when an order is created, and quota grant must affect exactly one user during the same transaction that marks the order paid. If the user disappears, the transaction rolls back and IPN returns an error instead of recording a paid order without quota.

## Security

- Never commit `vnp_HashSecret`.
- Rotate any secret that has previously appeared in Git history.
- Return/IPN error pages do not expose raw exception messages.
- Callback hashes are compared in constant time.
- Payment state is never trusted from browser-controlled Return URL alone.
- Development seed users are disabled by default and no fixed demo password is committed.

## CI coverage

Normal unit tests cover signing, Return-vs-IPN behavior, immutable quota metadata, amount validation and sequential idempotency. CI then starts a real PostgreSQL database and runs `PostgresConcurrencyTests` to verify two production-critical races:

- two simultaneous RAG quota reservations with one unit remaining allow exactly one request;
- two simultaneous successful IPNs for one order result in one `00`, one `02`, and exactly one quota credit.

References: VNPay PAY sandbox documentation at `https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html`.
