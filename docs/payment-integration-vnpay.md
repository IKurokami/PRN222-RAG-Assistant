# Payment Integration — VNPay (Sandbox)

## 1. Purpose

Users purchase query quota / premium access by paying through VNPay. This document describes a sandbox-grade integration using Razor Pages that preserves the existing layered architecture of PRN222.RagAssistant.

## 2. Scope

- **Provider**: VNPay (Sandbox only for demo; production needs merchant contract).
- **Use case**: one-time purchase of additional query quota tied to a subject or to the user account. For v1, quota is represented as extra allowed queries credited after successful payment; no subscription/renewal for now.
- **UI**: Razor Pages under `/Billing`.
- **Constraint**: no architecture breakage — keep provider-specific logic in Infrastructure, keep Application contracts provider-neutral, keep domain entities navigation-free, keep secrets out of code/git.

## 3. External references (read before implementation)

- VNPay sandbox registration: `https://sandbox.vnpayment.vn/devreg/`
- VNPay sandbox API docs: `https://sandbox.vnpayment.vn/apis/` and `https://sandbox.vnpayment.vn/apis/docs/thanh-toan-pay/pay.html`
- Payments techspec 2.1.0 references (parameter list, HMAC-SHA512, IPN): VNPay Techspec 2.1.0 as referenced by community docs.

## 4. VNPay integration essentials (summary from official sandbox docs)

### 4.1 Credentials

Upon sandbox registration, VNPay provides:

- `vnp_TmnCode` — merchant/terminal code
- `vnp_HashSecret` — secret for signing payment URL and verifying callbacks
- `vnp_Url` — sandbox payment URL: `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html`

Production requires a signed contract and separate credentials.

### 4.2 Create payment URL (Pay)

Build a redirect URL to `vnp_Url` with query parameters, then generate `vnp_SecureHash` using HMAC-SHA512 over the sorted parameter string (excluding `vnp_SecureHash` itself), signed by `vnp_HashSecret`, output as lowercase hex.

Key parameters:

- `vnp_Version` = `2.1.0`
- `vnp_Command` = `pay`
- `vnp_TmnCode`
- `vnp_Amount` = amount in VND * 100 (integer, no decimals)
- `vnp_CurrCode` = `VND`
- `vnp_TxnRef` = unique merchant order reference, unique per day
- `vnp_OrderInfo` = short description (no special chars ideally)
- `vnp_IpAddr` = client IP
- `vnp_ReturnUrl` = URL in this app that VNPay will redirect to after payment
- `vnp_CreateDate` = `yyyyMMddHHmmss`
- `vnp_Locale` = `vn`
- `vnp_OrderType` = `other` (for non-topup/non-billpayment)

### 4.3 Callbacks

Two channels:

- **ReturnUrl (GET)**: user browser redirected back to your site after payment. Used for UX; do not trust it alone for authoritative state change.
- **IPN (Instant Payment Notification, server-to-server POST)**: reliable channel. Verify hash, verify order ref + amount, apply idempotent state change, respond JSON `{ "RspCode": "00", "Message": "Confirm Success" }`.

Common IPN parameters include `vnp_TxnRef`, `vnp_Amount`, `vnp_ResponseCode` (`00` = success), `vnp_TransactionNo`, `vnp_BankCode`, `vnp_CardType`, `vnp_SecureHash`, etc. Exact list per Techspec 2.1.0; implementation should verify all parameters present in callback.

### 4.4 Security notes

- Never put `vnp_HashSecret` in client code.
- Generate hash server-side only.
- Process authoritative state change from IPN, not only from ReturnUrl.
- Verify hash on every callback.
- Make webhook handler idempotent.

## 5. Architecture fit

### 5.1 Principles

- **Application.Abstractions**: provider-neutral contract `IBillingService` + result/status models. No VNPay DTOs here.
- **Domain**: optional entity `PaymentOrder` with scalar FKs only, no navigation property; stored status + external reference + amount + currency + related subject id if needed.
- **Infrastructure**: `VnPayBillingService` implements `IBillingService`; contains hash creation/verification, IPN handling, return handling.
- **Presentation**: Razor Pages `/Billing/Create`, `/Billing/Return`, `/Billing/Webhook`, `/Billing/History`, possibly `/Billing/Details`.
- **Configuration**: `Billing:VnPay` section plus webhook URL config; secrets from environment/user secrets.

### 5.2 Why not break architecture

- Existing domain entities unchanged.
- New entity `PaymentOrder` follows existing convention (scalar FKs only, configuration in `IEntityTypeConfiguration`).
- Application contract does not leak VNPay types.
- Infrastructure service is the only place that knows VNPay parameter names and hash algorithm.
- Razor Pages flow uses `IBillingService`, not direct VNPay calls from page model except via service.

## 6. Data model

### 6.1 PaymentOrder entity

Namespace: `PRN222.RagAssistant.Domain.Entities`.

Fields:

- `Guid Id`
- `Guid UserId`
- `Guid? SubjectId` (optional; if quota is per subject)
- `string Provider` (for example `"VNPay"`)
- `string ExternalOrderId` (VNPay `vnp_TxnRef`)
- `long Amount` (smallest currency unit, e.g., VND * 100)
- `string Currency` (e.g., `"VND"`)
- `string Status` (for example `"Pending"`, `"Paid"`, `"Failed"`, `"Cancelled"`)
- `string? ExternalResponseCode`
- `string? ErrorMessage`
- `DateTime CreatedUtc`
- `DateTime? PaidUtc`
- `string MetadataJson` (optional; store provider callback payload summary)

No navigation property. Foreign keys as scalar only.

### 6.2 Configuration

`Data.Configurations.PaymentOrderConfiguration`:
- Table `PaymentOrders`
- PK `Id`
- Required fields with max lengths
- Unique index on `ExternalOrderId` (since it must be unique)
- Optional index on `UserId` + `Status`

### 6.3 EF behavior

- `SubjectId` FK: `DeleteBehavior.Restrict` (consistent with existing subject references).
- `UserId` does not FK to IdentityUser in this design to keep domain entities without navigation; storing Guid is acceptable and matches other entities that reference users by scalar FK.

## 7. Application contract

Namespace: `PRN222.RagAssistant.Application.Abstractions`.

`IBillingService`:

- `Task<BillingOrderResult> CreateOrderAsync(CreateBillingOrderRequest request, CancellationToken cancellationToken)`
- `Task<BillingOrderStatus> GetOrderAsync(Guid orderId, CancellationToken cancellationToken)`
- `Task<BillingOrderStatus> ProcessReturnAsync(ProcessReturnRequest request, CancellationToken cancellationToken)`
- `Task<BillingWebhookResult> ProcessWebhookAsync(ProcessWebhookRequest request, CancellationToken cancellationToken)`

Models (in `Application.Models`):

- `CreateBillingOrderRequest`: `UserId`, `SubjectId?`, `Amount`, `Currency`, `Description`, `ReturnUrl`, `IpAddress`
- `BillingOrderResult`: `OrderId`, `ExternalOrderId`, `CheckoutUrl`
- `BillingOrderStatus`: `OrderId`, `UserId`, `SubjectId?`, `Provider`, `ExternalOrderId`, `Amount`, `Currency`, `Status`, `CreatedUtc`, `PaidUtc`, `ErrorMessage`
- `ProcessReturnRequest`: `OrderId`, `CallbackQueryParameters` (dictionary of string->string from VNPay return)
- `ProcessWebhookRequest`: `Provider`, `CallbackParameters` (dictionary)
- `BillingWebhookResult`: `Success`, `Message`

### 7.1 Return vs webhook responsibilities

- `ProcessReturnAsync` verifies hash from return query string, then marks order as confirmed (if valid). This is convenience UX path.
- `ProcessWebhookAsync` is authoritative: verifies hash, checks order exists, checks amount matches (within tolerance), ensures idempotency, updates status to Paid, records external response code.

## 8. Infrastructure service

Namespace: `PRN222.RagAssistant.Infrastructure.Billing`.

`VnPayBillingService` implements `IBillingService`.

### 8.1 Configuration binding

Section `Billing:VnPay`:

- `TmnCode`
- `HashSecret`
- `BaseUrl` (sandbox: `https://sandbox.vnpayment.vn/paymentv2/vpcpay.html`)
- `Version` = `2.1.0`
- `Command` = `pay`
- `Locale` = `vn`
- `OrderType` = `other`
- `WebhookPath` (app-relative path for IPN, e.g. `/Billing/Webhook`)
- `Sandbox` = `true`

Also need `Billing:BaseUrl` for constructing `ReturnUrl` and `Webhook` absolute URLs (used for VNPay).

Secret handling: `HashSecret` and `TmnCode` should come from environment variables / user secrets / Render config in production; for sandbox demo they can be in `appsettings.Development.json` but never committed in real credentials.

### 8.2 Order creation

Steps:

1. Generate `ExternalOrderId` unique per day, e.g. `{prefix}-{yyyyMMdd}-{guid}` or similar; ensure uniqueness.
2. Create `PaymentOrder` with status `Pending`.
3. Build VNPay query parameters map.
4. Compute `vnp_SecureHash`.
5. Build absolute `ReturnUrl` and ensure webhook URL is absolute and publicly reachable in sandbox (ngrok/Render).
6. Return `BillingOrderResult` with `CheckoutUrl`.

### 8.3 Hash creation

- Collect parameters into a list of `(key, value)`.
- Remove empty values if VNPay spec requires; standard practice is to include only non-empty parameters.
- Sort by key alphabetically.
- Concatenate `key=value&...`.
- Sign with HMAC-SHA512 using `HashSecret` (as UTF-8 bytes).
- Output lowercase hex.

### 8.4 Return verification

- Receive query parameters from Razor Page `Return`.
- Recompute expected hash from parameters (excluding `vnp_SecureHash`) using same algorithm and secret.
- Compare with `vnp_SecureHash` (case-sensitive hex comparison).
- If mismatch, treat as failed/invalid.
- If match, read `vnp_ResponseCode`; if `00`, mark order Paid, otherwise Failed.

### 8.5 Webhook verification

- Receive POST parameters (query string or form, per VNPay IPN behavior).
- Verify hash same way.
- Find order by `ExternalOrderId`.
- Verify amount matches (converted to same unit).
- If already Paid, respond success without re-processing (idempotent).
- Update status to Paid, set `PaidUtc`, store external response code and metadata.
- Return JSON `{ "RspCode": "00", "Message": "Confirm Success" }`.

### 8.6 Quota crediting (v1)

For v1, on successful payment:
- Deduct/issue additional query quota for the user (implementation of quota itself is outside scope of payment plumbing; this is a hook point).
- If `SubjectId` is provided, credit is scoped; otherwise credit user-wide.
- Implementation: define an internal hook or domain service `IQuotaService` (not part of VNPay integration, but the point where payment success triggers quota grant). For now, store the paid order and leave quota logic as a separate concern; document the hook.

## 9. Razor Pages flow

### 9.1 `/Billing/Create`

GET:
- Require auth.
- Optionally require a subject context.
- Show a form with amount/plan selection (for v1, could be simple fixed plans like "50000 VND for 50 extra queries").

POST:
- Validate model.
- Call `IBillingService.CreateOrderAsync`.
- If result has `CheckoutUrl`, redirect to it.
- Otherwise show error.

### 9.2 `/Billing/Return`

GET:
- Receive query parameters from VNPay.
- Call `IBillingService.ProcessReturnAsync`.
- Show result: success/failed, link to history.

### 9.3 `/Billing/Webhook`

POST:
- Require appropriate verification (signature) and optionally binding to a known path.
- Call `IBillingService.ProcessWebhookAsync`.
- Return JSON response to VNPay.

Note: Webhook should not require auth cookie; it must be publicly reachable and verified by hash.

### 9.4 `/Billing/History`

GET:
- Require auth.
- List user's `PaymentOrder` records with status.
- Show external order id, amount, status, paid time.

### 9.5 `/Billing/Details`

GET with `orderId`:
- Require auth and ownership (only own orders).
- Show details.

## 10. Configuration example

`appsettings.Development.json` (sandbox demo only):

```json
{
  "Billing": {
    "BaseUrl": "https://localhost:7001",
    "VnPay": {
      "TmnCode": "YOUR_TMN_CODE",
      "HashSecret": "YOUR_HASH_SECRET",
      "BaseUrl": "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html",
      "Version": "2.1.0",
      "Command": "pay",
      "Locale": "vn",
      "OrderType": "other",
      "WebhookPath": "/Billing/Webhook",
      "Sandbox": true
    }
  }
}
```

Production overrides via environment variables / Render config, and `Sandbox` = `false`, production `BaseUrl` and credentials.

## 11. Test plan

### 11.1 Unit tests

- Hash creation: verify parameter sorting, hex output, expected hash for known inputs (mock secret).
- Hash verification: valid case, invalid case.
- Order creation: verify external order id uniqueness approach and pending status.
- Webhook idempotency: second call does not change state from Paid.

### 11.2 Integration/smoke tests

- Create order → get checkout URL → (manual) sandbox payment → return page shows success → history shows paid.
- Trigger webhook manually (via script/postman) with valid params → order becomes paid.
- Trigger webhook with invalid hash → not applied.
- Trigger webhook with mismatched amount → not applied or flagged.

### 11.3 Security checks

- Ensure `HashSecret` not in client-side code.
- Ensure webhook does not depend on authentication cookie.
- Ensure return/webhook paths are not open to order manipulation beyond hash verification.

## 12. Risks and notes

- Sandbox IPN may need URL configured in merchant portal; sandbox portal allows IPN URL configuration.
- In local dev, webhook URL must be public; use ngrok or Render deploy for testing IPN.
- `vnp_TxnRef` must be unique per day; use date-scoped uniqueness.
- Amount unit conversion (VND * 100) is a common source of bugs; assert in tests.
- VNPay documentation can be updated; always refer to official sandbox docs and merchant-provided techspec for final parameter list.
- Production requires merchant contract, KYC, and separate credentials; sandbox is only for demo.
- Quota logic itself is not part of VNPay integration plumbing; treat it as downstream hook.

## 13. Implementation checklist

- [ ] Register sandbox account, obtain credentials.
- [ ] Add `PaymentOrder` entity + configuration + migration.
- [ ] Add `IBillingService` + models.
- [ ] Add `VnPayBillingService`.
- [ ] Add Razor Pages `/Billing/*`.
- [ ] Wire DI and config.
- [ ] Add unit tests for hash and order logic.
- [ ] Run smoke test with sandbox payment.
- [ ] Document webhook URL setup.

## 14. Out of scope for v1

- Subscription/recurring payments.
- Refund handling.
- Multi-provider abstraction beyond VNPay.
- Quota engine implementation (only hook point documented).
- Production merchant onboarding (requires contract).
