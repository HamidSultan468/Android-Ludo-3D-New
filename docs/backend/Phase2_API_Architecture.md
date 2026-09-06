# Phase 2 — Backend Logic / API Architecture

Run order: `phase1_schema.sql` → `phase2_functions.sql` (both in Supabase SQL Editor).

Everything here is a **Postgres function** exposed automatically by Supabase as an RPC
endpoint: `POST /rest/v1/rpc/<function_name>` (the client SDK calls `supabase.rpc(...)`).
No separate Node server is needed for Phase 2.

---

## 1. Auth setup (Supabase dashboard, one time)

1. **Authentication → Providers**: enable **Email** (and **Phone / OTP** if you want).
2. **Authentication → URL Configuration**: add your app deep-link redirect URL.
3. Nothing else to build — `phase1_schema.sql` already installed a trigger
   (`on_auth_user_created`) that, for every new signup, creates:
   `profiles` row + `privacy_settings` row + the **3 wallets** (`cash`, `building`, `hero`).
4. To make someone staff: in **Table Editor → profiles**, set their `primary_role_id`
   to `1` (CEO), `2` (Manager T1) or `3` (Manager T2).

---

## 2. RPC endpoints

| Function | Who can call | Purpose | Params |
|---|---|---|---|
| `get_current_ad_rate()` | any user | read the live per-ad rate | – |
| `log_ad_impression(...)` | any user | log one watched ad (Real-Time Impression Logger) | `p_provider_code`, `p_game_code?`, `p_placement?`, `p_dedupe_key?`, `p_duration_seconds?`, `p_device_id?` |
| `set_ad_rate(...)` | perm `ad_rate.update` | CEO/Manager changes the per-ad rate | `p_rate`, `p_currency?`, `p_effective_from?`, `p_note?` |
| `convert_coins(...)` | any user | cash → building/hero at manager rate | `p_to_wallet` (`building`\|`hero`), `p_amount` |
| `request_withdrawal(...)` | any user | cash-out request; holds the cash | `p_amount`, `p_payment_account_id` |
| `review_withdrawal(...)` | perm `withdrawal.review` | staff pays or rejects (reject = auto refund) | `p_withdrawal_id`, `p_action` (`pay`\|`reject`), `p_note?`, `p_gateway_ref?` |
| `create_listing(...)` | any user | list coins for sale (coins locked at once) | `p_wallet_type`, `p_coin_amount`, `p_price_cash`, `p_expires_at?` |
| `open_escrow_trade(...)` | any user | buyer accepts a listing; cash+coins locked | `p_listing_id` |
| `settle_escrow_trade(...)` | perm `escrow.oversee` | release (coins→buyer, cash→seller) or refund | `p_trade_id`, `p_action` (`release`\|`refund`), `p_note?` |
| `set_account_freeze(...)` | perm `user.freeze` | Freeze Account / Freeze Coins toggle | `p_user_id`, `p_freeze_type` (`account`\|`coins_only`), `p_active`, `p_reason?` |
| `set_staff_permission(...)` | perm `staff.permissions.manage` | Staff Add/Remove matrix (one perm, one user) | `p_user_id`, `p_permission_code`, `p_allowed`, `p_reason?` |

Reads (no function needed — use PostgREST + RLS): `wallets`, `wallet_transactions`,
`ad_impressions`, `daily_earnings`, `player_stats`, `marketplace_listings` (active),
`escrow_trades` (own), `support_tickets` (own).

---

## 3. Key design rules (why it is safe)

- **One money path.** Every balance change goes through `_apply_wallet_txn()`, which locks
  the wallet row (`FOR UPDATE`), blocks negative balances and frozen wallets, writes a
  `wallet_transactions` ledger row, then updates `wallets.balance`. Nothing bypasses it.
- **Ledger is append-only.** Never `UPDATE`/`DELETE` `wallet_transactions`.
- **Ad views do not pay instantly.** `log_ad_impression` only records the view with
  `earned_status='earned'` and a `rate_snapshot`. Cash is credited once per day by the
  Phase 3 payout job (spec: "updating the user's Cash Wallet daily").
- **No direct P2P transfer.** The only way coins move between users is
  `open_escrow_trade` → `settle_escrow_trade`, both fully ledgered, with `manager_id` set.
- **Permissions checked inside the function**, not on the client, via `has_permission()`
  which honours both the role matrix and per-user `staff_permission_overrides`.
- **Audit.** `set_ad_rate`, `review_withdrawal`, `settle_escrow_trade`,
  `set_account_freeze`, `set_staff_permission` all write `audit_logs`.

---

## 4. Example calls (client SDK)

```js
// log a rewarded ad
const { data } = await supabase.rpc('log_ad_impression', {
  p_provider_code: 'admob',
  p_game_code: 'ludo',
  p_placement: 'rewarded_spin',
  p_dedupe_key: crypto.randomUUID(),
  p_duration_seconds: 30,
});
// -> { impression_id, counted, reason }

// convert 100 cash coins to hero coins
await supabase.rpc('convert_coins', { p_to_wallet: 'hero', p_amount: 100 });

// CEO raises the rate to 0.75 PKR/ad
await supabase.rpc('set_ad_rate', { p_rate: 0.75, p_note: 'Eid bonus' });
```

For Unity (`BackendService.cs`): call the same paths with `UnityWebRequest`:
`POST {SUPABASE_URL}/rest/v1/rpc/{fn}` with headers `apikey`, `Authorization: Bearer <user jwt>`,
`Content-Type: application/json`, body = the params object.

---

## 5. Next (Phase 3)

- `run_daily_payout(p_date)` function: sum `earned` impressions per user for the day →
  credit `cash` wallet via `_apply_wallet_txn` → write `payout_runs` + `daily_earnings`.
- Schedule it with `pg_cron` (Supabase: Database → Extensions → enable `pg_cron`).
- CEO dashboard views: global revenue, DAU, top earners, payout history.
