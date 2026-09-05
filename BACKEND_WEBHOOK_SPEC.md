# Tangent Ludo Empire — Payment Webhook API (Phase 3.5)

For the backend team. Nothing in this document is implemented client-side beyond building the request
that reaches this endpoint and reacting to the redirect/push it produces — the endpoint itself, its hash
verification, and the Firebase send are all **backend work**, not covered by the Unity project.

## Why this exists

`Assets/_TangentLudoEmpire/Phase3/Payments/PaymentCallbackListener.cs` (a `localhost:8081` listener) only
works in the Unity Editor / a Standalone dev build — a real phone's browser cannot reach `localhost` back
into a different app's process. On Android/iOS, the gateway must redirect to a **public HTTPS URL you
control**, which then tells the app the result through a channel that actually works cross-process:

1. **Deep link (primary, fast path)** — your endpoint 302-redirects the user's browser to
   `<DeepLinkScheme>://callback?orderId=...&status=...` immediately after verifying the gateway. Android
   routes that to the app via the intent-filter in `Assets/Plugins/Android/AndroidManifest.xml`; the app
   handles it in `DeepLinkManager.OnDeepLink`. Near-instant, works only while the browser tab is still
   open and the redirect actually fires.
2. **Push notification (fallback / reconciliation)** — for the case the redirect never reaches the app
   (browser closed early, backgrounded at the wrong moment, flaky connection): send an FCM data message so
   the app can reconcile the order even if it missed the deep link. Not built in Phase 3.5 — this document
   specifies the contract; wiring an FCM receiver in Unity is a follow-up.

Both channels are just **notifications to go check** — see the Security section. Neither one is ever
trusted as proof of payment by itself.

---

## `POST /api/payment/callback`

Called by the gateway's own server-to-server webhook (JazzCash/Easypaisa hit this directly), **not** by
the client. This is the source of truth; the deep link / push are just how the *result* reaches the app.

### Request body

```json
{
  "orderId": "TLE_<userId>_<ticks>",
  "amount": "100.00",
  "statusCode": "000",
  "secureHash": "<hex>"
}
```

| Field | Meaning |
|---|---|
| `orderId` | The `TLE_<userId>_<ticks>` value the app generated in `JazzCashGateway`/`EasypaisaGateway.MakeOrderId()`. Anti-replay: a re-used `orderId` can only ever confirm the ONE still-`Pending` client-side row with that ref (see `MoneyWallet.ConfirmPendingDeposit`'s idempotency), so a replayed webhook call is a safe no-op on the client even if it somehow reaches the app twice. |
| `amount` | The provider's own reported amount (decimal string, 2dp). Cross-check against what your own order record says was requested — mismatch = reject, don't forward. |
| `statusCode` | The gateway's **raw** code: JazzCash `pp_ResponseCode` (success = `"000"`), Easypaisa `responseCode` (success = `"0000"`). Normalise to the client-facing sentinel below before it reaches the app. |
| `secureHash` | The gateway's own signature on its callback payload — verify this FIRST, before touching anything else. |

### Hash verification (must match the client's math exactly)

The client signs with the same two formulas the app itself uses (see
`Assets/_TangentLudoEmpire/Phase3/Payments/JazzCashGateway.cs` / `EasypaisaGateway.cs` — treat those two
files as the reference implementation, this section is a description of them, not a separate spec):

- **JazzCash**: HMAC-SHA256, key = `JazzCash_IntegritySalt` (the merchant's integrity salt — **never** the
  one baked into the mobile app's `PaymentConfig.asset`; the backend must hold its own copy of this secret,
  server-side only). Message = every `pp_*` field **except** `pp_SecureHash` itself, sorted by key
  (ordinal), values joined with `&` (no key names, no separators between kv pairs beyond the `&`), hex
  lowercase output.
- **Easypaisa**: `SHA256(storeId + orderId + amount + password)`, hex lowercase. `password` is
  `Easypaisa_StorePassword`, server-side only, never shipped in the app.

If the hash doesn't match: **reject** (HTTP 400, do not credit, do not redirect, do not push). Log it as a
tamper/fraud signal — this is the same event class the client logs as `CALLBACK_TAMPERED` when it catches
a forged *local* callback.

### Logic

1. Verify `secureHash` (above). Reject on mismatch.
2. Look up `orderId` in your own order ledger (created when the app called your API to start the deposit,
   or reconstructed from what you know about `orderId`'s `<userId>_<ticks>` shape — either way, the
   backend must independently know this order exists and what it's for; never trust the webhook body
   alone for "this order is real").
3. Compare `amount` against the ledger's expected amount. Reject on mismatch.
4. Normalise `statusCode` to `"000"` (success) or anything else (failure) — this is the value the deep
   link's `status` param and the FCM payload both carry, so the CLIENT never has to know JazzCash uses
   3 zeros and Easypaisa uses 4.
5. **Call your reward-validation cloud function** (the same kind of check
   `TangentLudoEmpire.Services.AntiCheatManager.ValidatePayment` calls into on the client via
   `BackendService.ValidateReward` — this webhook handler *is* that server-side authority) to mark the
   order settled in your ledger.
6. Redirect the browser: `302 -> <pp_ReturnURL_Backup or postBackURL_Backup value>?orderId=<orderId>&status=<normalised>`
   (the app sent you this base URL — `tle://callback` by default — in the original `pp_ReturnURL_Backup` /
   `postBackURL_Backup` field; append the query params yourself, don't expect the app to have pre-filled
   them).
7. **Also** send an FCM data message to the user's registered device(s) with the same
   `{orderId, status}` payload, as a fallback in case the deep-link redirect never reaches the app (step 6
   depends on the browser tab still being open and the OS actually routing the Intent - not guaranteed).

### Response

```json
{ "success": true }
```

Return `success: false` (still HTTP 200, so the gateway doesn't endlessly retry a webhook you've already
understood and rejected) with an `error` field for a verified-but-rejected callback (amount mismatch,
unknown order, etc.); return a 4xx/5xx only for a genuinely malformed/unverifiable request.

---

## Security notes (read before implementing)

- **Never trust `amount` or `statusCode` from the request body alone** — they're what the *gateway* claims,
  which is one step better than what the *client* claims, but still not authoritative until your own
  ledger + the secure-hash check agree.
- **`JazzCash_IntegritySalt` / `Easypaisa_StorePassword` must exist server-side, independently of the
  mobile app's `PaymentConfig.asset`.** The copy in the app is for *outbound* request-signing only (so the
  gateway trusts requests coming from the app); the backend needs its own copy to verify *inbound*
  webhooks. They're the same secret value, but two separate deployments of it — rotate both together.
- **The deep link and FCM channels are notifications, not proof.** `MoneyWallet.ConfirmPendingDeposit` on
  the client re-runs `AntiCheatManager.ValidatePayment`, which (once wired to a real `apiBaseUrl`) asks
  *this same backend* to confirm the order before crediting anything — so even a forged deep link
  (another app registering the same custom scheme, for instance) can't move money on its own. Don't treat
  "the deep link fired" as a reason to relax verification on the webhook side; they're independent checks.
- Rate-limit / dedupe this endpoint per `orderId` — a gateway retrying its webhook (network hiccup on their
  end) should not be able to trigger more than one redirect/push per order.
