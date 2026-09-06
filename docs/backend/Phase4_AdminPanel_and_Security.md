# Phase 4 — Admin Panel Layout + Security / Privacy Guide

Covers deliverables **3** (Admin Panel control layout) and **4** (Secure Auth + Privacy Shield).
SQL for this phase: `phase4_privacy.sql` (run after phase 1–3).

---

## PART A — ADMIN PANEL

### A1. Who sees what (from the seeded permission matrix)

| Screen | CEO | Manager T1 | Manager T2 |
|---|---|---|---|
| Overview / Dashboard | ✅ | ✅ | view only |
| Ad Rate Control | ✅ | ✅ | ❌ |
| Ad Logs & Payout Runs | ✅ | ✅ | view logs |
| Users | ✅ | ✅ | view + freeze |
| Staff & Permissions Matrix | ✅ | ✅ | ❌ |
| Withdrawals Queue | ✅ | ✅ | ✅ |
| Escrow / Marketplace | ✅ | ✅ | ✅ |
| Conversion Rates | ✅ | ✅ | ❌ |
| Master Settings | ✅ | ❌ | ❌ |
| Audit Logs | ✅ | ✅ | ❌ |
| Support Tickets | ✅ | ✅ | ✅ |
| Global Analytics / Revenue | ✅ | ✅ | ❌ |

Gate each route in the admin app with `supabase.rpc('current_role_code')` and, per action,
`supabase.rpc('has_permission', { p_code: '...' })`. Never rely on hiding the button alone —
every function re-checks the permission server-side.

### A2. Screens, components and button → RPC mapping

**1. Overview**
- Cards: users, DAU today, ads today, current rate, pending withdrawals, wallet totals, open/disputed escrow, last payout → `get_dashboard_summary()`
- Revenue line chart (date range) → `get_global_revenue(from, to)`
- Top earners table → `get_top_earners(days, limit)`
- Reconciliation alert (rows where ledger ≠ balance) → `get_wallet_reconciliation()`

**2. Ad Rate Control**
- Shows current rate → `get_current_ad_rate()`
- History list → select from `ad_rate_config` (ordered by `effective_from desc`)
- Form: rate, currency, effective-from, note → **Save** → `set_ad_rate(p_rate, p_currency, p_effective_from, p_note)`

**3. Ad Logs & Payout Runs**
- Impressions table with filters (user, provider, date, earned_status) → select `ad_impressions`
- Payout runs list → select `payout_runs`
- Per-user daily earnings → select `daily_earnings`
- **Run payout now** (pick a date) → `run_daily_payout(p_date)`  *(needs `payout.run`)*

**4. Users**
- List / search → select `profiles` (+ join `roles`)
- Row → drawer: profile, wallets (`wallets`), recent ledger (`wallet_transactions`), ad stats
- **Freeze account** / **Freeze coins** / **Unfreeze** → `set_account_freeze(p_user_id, p_freeze_type, p_active, p_reason)`
- **Set role / Make Pro**: update `profiles.primary_role_id` / `is_pro` (staff-write policy, or add a small RPC later)

**5. Staff & Permissions Matrix**
- Rows = staff users, columns = `permissions.code`
- Cell toggle → `set_staff_permission(p_user_id, p_permission_code, p_allowed, p_reason)`
- Current effective state = role matrix (`role_permissions`) overlaid with `staff_permission_overrides`

**6. Withdrawals Queue**
- List `withdrawals` where status in (requested, under_review, approved, processing)
- **Pay** (with gateway ref) → `review_withdrawal(id, 'pay', note, gateway_ref)`
- **Reject** (auto-refunds the held cash) → `review_withdrawal(id, 'reject', note)`

**7. Escrow / Marketplace**
- `escrow_trades` filtered by status (funded / disputed)
- Timeline per trade → select `escrow_events`
- **Release** (coins→buyer, cash−fee→seller) → `settle_escrow_trade(id, 'release', note)`
- **Refund** (everything back) → `settle_escrow_trade(id, 'refund', note)`
- Active listings → select `marketplace_listings`

**8. Conversion Rates**
- Table of `conversion_rates`
- Form: from, to, rate, min, max, note → **Save** → `set_conversion_rate(p_from, p_to, p_rate, p_min, p_max, p_note)`

**9. Master Settings**
- Key/value editor over `admin_settings` (min_withdrawal_pkr, withdrawal_fee_pct, escrow_fee_pct, daily_payout_time_utc, max_ads_per_day)
- Write allowed only with `system.settings.update` (RLS policy already set) → CEO only

**10. Audit Logs**
- Read `audit_logs` (filter by actor, action, date) — needs `system.audit.read`

**11. Support Tickets**
- `support_tickets` list + `support_ticket_messages` thread; assign to staff; change status

### A3. Compact action → RPC table

| Button | RPC | Permission |
|---|---|---|
| Change ad rate | `set_ad_rate` | ad_rate.update |
| Run payout | `run_daily_payout` | payout.run |
| Freeze / unfreeze | `set_account_freeze` | user.freeze |
| Grant/revoke staff perm | `set_staff_permission` | staff.permissions.manage |
| Pay / reject withdrawal | `review_withdrawal` | withdrawal.review |
| Release / refund escrow | `settle_escrow_trade` | escrow.oversee |
| Set conversion rate | `set_conversion_rate` | conversion_rate.update |
| Edit master setting | direct update `admin_settings` | system.settings.update |
| Dashboard cards | `get_dashboard_summary` | analytics.global.read |
| Revenue chart | `get_global_revenue` | analytics.global.read |
| Top earners | `get_top_earners` | analytics.global.read |
| Reconciliation | `get_wallet_reconciliation` | analytics.global.read |

### A4. Suggested build
- **Fastest**: Retool / Appsmith pointed at the Supabase Postgres + RPCs.
- **Custom**: Next.js + `@supabase/supabase-js`, admin login via Supabase Auth, route guard on `current_role_code`. Deploy on Vercel.
- The Unity game uses the **same RPC URLs** (`{SUPABASE_URL}/rest/v1/rpc/<fn>`) from `BackendService.cs`.

---

## PART B — SECURE AUTH & PRIVACY SHIELD

### B1. Authentication
- Use **Supabase Auth**: email + password (email confirm ON) and/or phone OTP.
- **Staff (ceo / manager_*) must enable MFA/TOTP** (Supabase MFA). Enforce in the admin app: block sensitive screens until `aal2`.
- Password policy: min 8, block leaked passwords (Supabase setting).
- **JWT**: short-lived access token (1h) + refresh token. All RPC calls send `Authorization: Bearer <access token>`.
- **Unity session storage**: keep the refresh token in Android **EncryptedSharedPreferences** / Keystore — never `PlayerPrefs`, never a plain file.
- **Keys**: ship only the **anon** key in the app. The **service_role** key lives only on a server / Supabase Edge Function / cron — never in the APK, never in git.

### B2. Row Level Security checklist
- Every `public` table has RLS enabled (done in phase 1).
- Money tables (`wallets`, `wallet_transactions`, `deposits`, `withdrawals`, `coin_conversions`, `escrow_*`, `daily_earnings`, `payout_runs`): **no client INSERT/UPDATE/DELETE policy** → only SECURITY DEFINER functions and `service_role` can write.
- Test matrix before launch: call each table as (a) anon, (b) normal user, (c) other user's row, (d) staff. Only the intended ones should succeed.

### B3. Privacy Shield (payment info ON/OFF)
- Two flags: `privacy_settings.payment_shield_on` (per user) and `payment_accounts.shield_on` (per account). Shield is ON if **either** is true.
- The app must **never** `select payment_accounts` for display. It calls:
  - `get_payment_accounts()` → returns numbers **masked** (`****3456`) while shielded, plus a `masked` flag.
  - `reveal_payment_account(id)` → returns the full number **and writes an `audit_logs` row** (`payment_account.reveal`). In production, require a fresh step-up (re-enter password / OTP) in the client before calling this.
  - `set_privacy_shield(payment_shield_on, hide_profile, hide_earnings)` → toggle.
- UI: show masked by default everywhere; "Show" button triggers step-up → `reveal_payment_account`.

### B4. Data protection & anti-fraud
- Consider storing `account_number` encrypted (app-side or `pgcrypto`) and a separate `last4` for display.
- Abuse controls on `log_ad_impression`: `dedupe_key` unique (done), `max_ads_per_day` cap (done). Add a per-minute client throttle and a server velocity check (e.g. > N impressions / 60s from one `device_id` → flag).
- Fraud flag → `set_account_freeze(user, 'coins_only', true, 'velocity')` pending review.
- All privileged actions already write `audit_logs`; review it daily.

### B5. Money integrity
- Ledger is append-only; balance changes only via `_apply_wallet_txn` (row lock, no-negative, frozen check).
- Run `get_wallet_reconciliation()` on a schedule (add a cron like the payout job); any non-empty result = investigate immediately.
- Enable Supabase **PITR / daily backups**.

### B6. Secrets / environment
```
# app (Unity / public)
SUPABASE_URL=...
SUPABASE_ANON_KEY=...

# server / edge / cron only — NEVER in the app or git
SUPABASE_SERVICE_ROLE_KEY=...
```
Add `*.env` to `.gitignore`. Rotate keys if leaked. Security comes from RLS + functions, not from hiding the anon key.

---

## Full run order (all phases)
```
phase1_schema.sql
phase2_functions.sql
phase3_payout_analytics.sql   (+ enable pg_cron, schedule tle-daily-payout)
phase4_privacy.sql
```
