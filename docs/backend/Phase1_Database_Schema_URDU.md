# فیز 1 — ڈیٹابیس نقشہ (Database Schema)
### Tangent Ludo Empire — Supabase / PostgreSQL

> یہ فائل صرف **سمجھانے** کے لیے ہے۔
> اصل چلانے والا کوڈ اس فائل میں ہے: `phase1_schema.sql`

---

## 0) پہلے چند لفظ آسان زبان میں

| لفظ | آسان مطلب |
|-----|-----------|
| **Database** | ڈیٹا رکھنے کی الماری |
| **Table (ٹیبل)** | الماری کے اندر ایک "میز" — جیسے Excel کی ایک شیٹ۔ اس میں rows (قطاریں) اور columns (خانے) ہوتے ہیں |
| **Row (قطار)** | ایک ریکارڈ — جیسے ایک یوزر، ایک اشتہار، ایک لین دین |
| **Column (خانہ)** | ایک خاصیت — جیسے نام، بیلنس، تاریخ |
| **Primary Key** | ہر قطار کا یونیک شناختی نمبر (جیسے شناختی کارڈ نمبر) |
| **Foreign Key** | ایک ٹیبل کی قطار کو دوسری ٹیبل کی قطار سے جوڑنے والا نمبر |
| **Enum** | پہلے سے طے شدہ چند جائز الفاظ کی فہرست (غلط لفظ داخل ہی نہیں ہو سکتا) |
| **RLS (Row Level Security)** | Supabase کا حفاظتی پہرہ — طے کرتا ہے کون سی قطار کون دیکھ/بدل سکتا ہے |
| **Ledger (لیجر)** | حساب کتاب کی کاپی جس میں کبھی کچھ مٹایا نہیں جاتا، صرف نئی انٹری جُڑتی ہے |
| **Cron Job** | خودکار کام جو مقررہ وقت پر خود چلتا ہے (جیسے روز رات 12 بجے) |

---

## 1) اس کوڈ کو Supabase میں کیسے چلائیں (step by step)

1. اپنے براؤزر میں **supabase.com** کھولیں اور لاگ اِن کریں۔
2. اگر پروجیکٹ نہیں بنایا تو **New Project** پر کلک کریں → نام رکھیں → ڈیٹابیس کا پاسورڈ رکھیں (نوٹ کر لیں) → **Create**۔
3. بائیں طرف مینو میں **SQL Editor** پر کلک کریں۔
4. اوپر **+ New query** پر کلک کریں۔
5. فائل `phase1_schema.sql` کو کھول کر **سارا** کوڈ کاپی کریں اور یہاں پیسٹ کریں۔
6. نیچے دائیں **Run** (یا `Ctrl + Enter`) دبائیں۔
7. "Success. No rows returned" آ جائے تو نقشہ بن گیا۔
8. بائیں مینو میں **Table Editor** کھولیں — آپ کو تمام ٹیبلز نظر آئیں گی۔

> اگر کوئی error آئے تو پورا error متن مجھے بھیج دیں، میں ٹھیک کروا دوں گا۔

---

## 2) نقشہ ایک نظر میں — 9 حصے

```
1. RBAC            -> roles, permissions, role_permissions
2. Users/Privacy   -> profiles, user_roles, staff_permission_overrides,
                      privacy_settings, payment_accounts, pro_subscriptions
3. Games           -> games, linked_apps, game_sessions, player_stats
4. Ad Engine       -> ad_providers, ad_rate_config, payout_runs,
                      ad_impressions, daily_earnings
5. Wallets         -> wallets (3 per user), wallet_transactions (ledger)
6. Coin Economy    -> conversion_rates, coin_conversions, deposits, withdrawals
7. Escrow P2P      -> marketplace_listings, escrow_trades, escrow_events
8. Security/Admin  -> account_freezes, audit_logs, admin_settings
9. Support         -> support_tickets, support_ticket_messages
```

---

## 3) حصہ بہ حصہ تفصیل

### 🔹 حصہ 1: RBAC (کون کیا کر سکتا ہے)

- **roles** — 5 کردار پہلے سے بھر دیے گئے ہیں:
  `ceo`, `manager_t1`, `manager_t2`, `pro`, `free`
- **permissions** — ہر چھوٹا اختیار الگ (جیسے `ad_rate.update` = اشتہار کا ریٹ بدلنا)۔
- **role_permissions** — کون سے کردار کے پاس کون سی permission ہے (میٹرکس)۔
- **staff_permission_overrides** — یہی آپ کے پلان کا **"Staff Add / Remove matrix"** ہے۔
  اس سے CEO/Manager کسی ایک ملازم کو صرف ایک اختیار دے یا چھین سکتا ہے، پورا کردار بدلے بغیر۔

> **کیسے کام کرتا ہے:** `has_permission('ad_rate.update')` نامی فنکشن پہلے override دیکھتا ہے،
> پھر کردار کی میٹرکس۔ override میں `false` ہو تو اختیار بند، `true` ہو تو کھلا۔

---

### 🔹 حصہ 2: Users / Privacy

- **profiles** — ہر یوزر کی ایک قطار۔ Supabase کا اپنا `auth.users` صرف ای میل/پاسورڈ رکھتا ہے؛
  باقی معلومات (username, timezone, کردار, status) یہاں آتی ہیں۔
- **timezone** خانہ آپ کے **World Clock** فیچر کے لیے ہے (ڈیفالٹ `Asia/Karachi` = PST)۔
- **status** — `active / frozen / suspended / deleted` (Enum)۔
- **privacy_settings** — آپ کا **"security shield" ON/OFF** ٹوگل (`payment_shield_on`)۔
- **payment_accounts** — JazzCash / EasyPaisa / کارڈ کی تفصیل، ہر ایک پر الگ `shield_on`۔
- **pro_subscriptions** — Pro ممبرشپ کی مدت اور ادائیگی۔

> **خودکار سہولت:** جیسے ہی کوئی نیا یوزر sign-up کرے، ایک trigger خود بخود:
> (1) `profiles` قطار، (2) `privacy_settings` قطار، (3) **تینوں wallets** بنا دیتا ہے۔

---

### 🔹 حصہ 3: Games

- **games** — 5 گیمیں پہلے سے بھری ہیں: Ludo, Goli Danda, Kali Bandar, Smart Flow, Study۔
- **linked_apps** — یوزر نے کون سی گیم اپنے اکاؤنٹ سے جوڑی۔
- **game_sessions** — ہر میچ کی ایک قطار (نتیجہ, سکور, کمائے گئے hero coins, XP)۔
- **player_stats** — ہر (یوزر + گیم) کا مجموعی حساب (level, wins, losses) → یہی **Leaderboard / Top 5** کو ڈیٹا دیتا ہے۔

---

### 🔹 حصہ 4: Ad Tracking & Daily Revenue Engine (آپ کے پلان کا Section 2)

| ٹیبل | کام |
|------|-----|
| **ad_providers** | AdMob, Unity Ads وغیرہ کی فہرست |
| **ad_rate_config** | **"Per Ad Rate"** — CEO/Manager یہاں نیا ریٹ ڈالتا ہے۔ پرانی قطاریں مٹتی نہیں (تاریخ محفوظ)۔ سب سے نئی قطار = موجودہ ریٹ |
| **ad_impressions** | **Real-Time Impression Logger** — ہر دیکھا گیا اشتہار: `user_id`, `ad_provider_id`, `watched_at` (Timestamp), `earned_status` |
| **payout_runs** | روز کا ایک ریکارڈ جب **Cron Job** چلے (کتنے یوزر, کتنے اشتہار, کل رقم) |
| **daily_earnings** | ہر یوزر کی روزانہ کمائی = `ads_counted × rate_used` |

**دھوکہ روکنے کے دو حفاظتی بندوبست:**
- `dedupe_key` — ایپ ہر اشتہار کو ایک یونیک چابی دیتی ہے؛ ایک ہی اشتہار دو بار لاگ نہیں ہو سکتا۔
- `admin_settings` میں `max_ads_per_day` = 80 (روزانہ گنتی کی حد)۔

> **ابھی صرف نقشہ بنا ہے۔** خودکار روزانہ ادائیگی کا اصل کوڈ (Cron Job + فنکشن) **فیز 3** میں بنے گا۔

---

### 🔹 حصہ 5: 3 الگ الگ Wallets + Ledger (آپ کے پلان کا Section 3)

- **wallets** — ہر یوزر کی **بالکل 3 قطاریں**:
  1. `cash` — اشتہار/کمائی والا (قابلِ کیش آؤٹ)
  2. `building` — ٹائیکون/فیکٹری اپگریڈ (کیش آؤٹ نہیں ہو سکتا)
  3. `hero` — کردار/سکن اپگریڈ (گیم پلے سے ملتا ہے)
  - `unique (user_id, wallet_type)` → ہر قسم کا صرف ایک wallet۔
  - `check (balance >= 0)` → بیلنس کبھی منفی نہیں ہو سکتا۔
  - `is_frozen` → **"Freeze Coins"** کے لیے۔

- **wallet_transactions** — **لیجر**۔ اصول:
  > اس ٹیبل میں کبھی `UPDATE` یا `DELETE` نہیں — صرف نئی قطار `INSERT`۔
  - پورے سسٹم میں پیسوں کی ہر حرکت یہاں ایک قطار بناتی ہے۔
  - ہر قطار میں `balance_before` اور `balance_after` محفوظ → مکمل آڈٹ ٹریل۔

---

### 🔹 حصہ 6: Internal Exchange + Deposit + Withdraw

- **conversion_rates** — Manager کے طے کردہ ریٹ (مثلاً 1 cash coin = 10 building coins)۔ Seed میں 2 ریٹ پہلے سے موجود۔
- **coin_conversions** — ایک مکمل تبادلہ (لیجر میں 2 قطاریں بناتا ہے: ایک نکلی، ایک آئی)۔
- **deposits** — باہر سے پیسہ آنا (JazzCash/EasyPaisa/کارڈ ٹاپ اپ)۔
- **withdrawals** — Cash wallet سے کیش آؤٹ کی درخواست (Manager کی منظوری درکار — `status` اور `reviewed_by`)۔

---

### 🔹 حصہ 7: Secure Escrow P2P Marketplace (آپ کے پلان کا Section 3)

> براہِ راست یوزر → یوزر ٹرانسفر **بند** ہے۔ ہر سودا Escrow سے گزرتا ہے۔

- **marketplace_listings** — بیچنے والا کہتا ہے: "اتنے coins، اتنے cash میں"۔
- **escrow_trades** — روکنے کا نظام۔ coins اور cash **روکے** جاتے ہیں جب تک دونوں فریق کام مکمل نہ کریں۔
  - `buyer_cash_hold_txn_id`, `seller_coin_hold_txn_id`, `release_*`, `refund_txn_id` — ہر مرحلہ لیجر سے جُڑا۔
  - `manager_id` — نگرانی کے لیے۔
  - `status`: `created → funded → released` (یا `refunded / disputed / cancelled`)۔
- **escrow_events** — ہر سودے کی مکمل ٹائم لائن (کس نے، کب، کیا کیا)۔

---

### 🔹 حصہ 8: Security Freeze + Audit + Master Settings

- **account_freezes** — Manager کا **"Freeze Account / Freeze Coins"** ٹوگل، پوری تاریخ کے ساتھ۔
- **audit_logs** — پورے سسٹم کا آڈٹ ریکارڈ (CEO کے **"system audit logs"** کے لیے)۔
- **admin_settings** — **Master rate controls** (key/value)۔ Seed میں موجود:
  `min_withdrawal_pkr`, `withdrawal_fee_pct`, `escrow_fee_pct`, `daily_payout_time_utc`, `max_ads_per_day`۔

---

### 🔹 حصہ 9: Support

- **support_tickets** + **support_ticket_messages** — Manager کے "handle support tickets" کام کے لیے۔

---

## 4) حفاظت (RLS) — مختصر اصول

فائل کے حصہ 12 میں یہ پہرہ لگا ہوا ہے:

| کون | کیا کر سکتا ہے |
|-----|----------------|
| عام یوزر | **صرف اپنی** قطاریں پڑھ سکتا ہے (اپنا wallet, اپنے اشتہار, اپنے لین دین) |
| عام یوزر | صرف `privacy_settings` اور `payment_accounts` خود بدل سکتا ہے |
| Staff (ceo/manager) | سب کچھ پڑھ سکتا ہے (`is_staff()` فنکشن) |
| پیسوں والی ٹیبلز | **کلائنٹ سے کوئی بھی براہِ راست لکھ نہیں سکتا** — صرف سرور (service_role) یا فیز 2 کے محفوظ فنکشن |

> اسی لیے پیسوں کا کام ہمیشہ سرور کی طرف سے ہوگا، ایپ سے کبھی نہیں۔ یہ جان بوجھ کر ہے۔

---

## 5) اگلا قدم (فیز 2)

1. **Auth** سیٹ اپ (ای میل/فون OTP) + `profiles` سے کردار جوڑنا۔
2. اشتہار لاگ کرنے کا **محفوظ فنکشن** `log_ad_impression(...)`۔
3. **Coin conversion** فنکشن (لیجر کی 2 قطاریں + بیلنس اپڈیٹ ایک ساتھ)۔
4. یہ سب مکمل ہونے پر Unity کے `BackendService.cs` کو اصل Supabase سے جوڑنا۔

> جب آپ کہیں گے، میں فیز 2 اسی آسان انداز میں شروع کر دوں گا۔

---

## 6) ٹیبلز کی مکمل فہرست (کل 30)

```
roles, permissions, role_permissions,
profiles, user_roles, staff_permission_overrides,
privacy_settings, payment_accounts, pro_subscriptions,
games, linked_apps, game_sessions, player_stats,
ad_providers, ad_rate_config, payout_runs, ad_impressions, daily_earnings,
wallets, wallet_transactions,
conversion_rates, coin_conversions, deposits, withdrawals,
marketplace_listings, escrow_trades, escrow_events,
account_freezes, audit_logs, admin_settings,
support_tickets, support_ticket_messages
```
