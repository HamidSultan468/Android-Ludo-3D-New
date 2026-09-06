-- =============================================================================
--  TANGENT LUDO EMPIRE  —  PHASE 1 DATABASE SCHEMA
--  Target: Supabase (PostgreSQL 15+)
--  Covers: RBAC, Users/Privacy, Games, Ad Tracking + Daily Payout,
--          3 Segregated Wallets + Ledger, Coin Economy, Escrow P2P,
--          Security Freeze, Audit Logs, Admin Settings, Support.
--
--  HOW TO RUN:
--   1. Supabase Dashboard -> SQL Editor -> New query
--   2. Paste this whole file -> RUN
--   3. Then run the "SEED DATA" block at the bottom (already included here).
--
--  NOTE: Supabase Auth already creates and manages the `auth.users` table.
--        We only build `public.*` tables and link them to `auth.users(id)`.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 0. EXTENSIONS
-- -----------------------------------------------------------------------------
create extension if not exists "pgcrypto";      -- gen_random_uuid()
create extension if not exists "pg_trgm";        -- text search on usernames (optional)

-- -----------------------------------------------------------------------------
-- 1. ENUM TYPES  (fixed list of allowed values -> stops typos & bad data)
-- -----------------------------------------------------------------------------
do $$ begin
  create type user_status        as enum ('active','frozen','suspended','deleted');
  create type wallet_type        as enum ('cash','building','hero');
  create type txn_direction      as enum ('credit','debit');
  create type txn_category       as enum (
      'ad_earning','daily_payout','conversion_in','conversion_out',
      'deposit','withdrawal','withdrawal_refund',
      'escrow_hold','escrow_release','escrow_refund',
      'tournament_fee','tournament_prize','referral_bonus','admin_adjustment'
  );
  create type ad_earned_status   as enum ('pending','earned','rejected','duplicate');
  create type payout_run_status  as enum ('pending','processing','completed','failed');
  create type withdrawal_method  as enum ('jazzcash','easypaisa','bank_card','internal');
  create type withdrawal_status  as enum ('requested','under_review','approved','processing','paid','rejected','cancelled');
  create type deposit_status     as enum ('initiated','pending','completed','failed','cancelled');
  create type escrow_status      as enum ('created','funded','released','refunded','disputed','cancelled');
  create type listing_status     as enum ('active','sold','cancelled','expired');
  create type freeze_type        as enum ('account','coins_only');
  create type subscription_status as enum ('trial','active','expired','cancelled');
  create type ticket_status      as enum ('open','pending','resolved','closed');
exception
  when duplicate_object then null;
end $$;

-- -----------------------------------------------------------------------------
-- 2. RBAC  (Roles Based Access Control)
-- -----------------------------------------------------------------------------

-- 2.1 roles: CEO, Manager Tier 1/2, Pro, Free
create table if not exists public.roles (
    id           serial primary key,
    code         text not null unique,          -- 'ceo','manager_t1','manager_t2','pro','free'
    name         text not null,
    tier         int  not null default 0,       -- higher tier = more power
    description  text,
    created_at   timestamptz not null default now()
);

-- 2.2 permissions: every fine-grained action in the system
create table if not exists public.permissions (
    id           serial primary key,
    code         text not null unique,          -- e.g. 'ad_rate.update', 'user.freeze'
    category     text not null,                 -- 'ads','wallet','users','escrow','system'
    description  text
);

-- 2.3 role_permissions: which role has which permission (base matrix)
create table if not exists public.role_permissions (
    role_id        int not null references public.roles(id)       on delete cascade,
    permission_id  int not null references public.permissions(id) on delete cascade,
    primary key (role_id, permission_id)
);

-- -----------------------------------------------------------------------------
-- 3. USERS / PROFILES / PRIVACY
-- -----------------------------------------------------------------------------

-- 3.1 profiles: 1 row per auth.users row (extra info Supabase Auth does not store)
create table if not exists public.profiles (
    id               uuid primary key references auth.users(id) on delete cascade,
    username         text unique,
    full_name        text,
    avatar_url       text,
    country          text,
    timezone         text not null default 'Asia/Karachi',   -- for World Clock feature
    primary_role_id  int  not null references public.roles(id) default 5,  -- 5 = free (see seed)
    status           user_status not null default 'active',
    is_pro           boolean not null default false,
    referred_by      uuid references public.profiles(id),
    last_login_at    timestamptz,
    created_at       timestamptz not null default now(),
    updated_at       timestamptz not null default now()
);
create index if not exists idx_profiles_role   on public.profiles(primary_role_id);
create index if not exists idx_profiles_status on public.profiles(status);

-- 3.2 user_roles: optional EXTRA roles beyond the primary one (a manager who is also pro)
create table if not exists public.user_roles (
    user_id      uuid not null references public.profiles(id) on delete cascade,
    role_id      int  not null references public.roles(id)    on delete cascade,
    assigned_by  uuid references public.profiles(id),
    assigned_at  timestamptz not null default now(),
    primary key (user_id, role_id)
);

-- 3.3 staff_permission_overrides: the "Staff Add / Remove" matrix.
--     Lets a CEO/Manager grant or revoke a single permission for ONE staff user,
--     without changing that user's whole role.
create table if not exists public.staff_permission_overrides (
    id             bigserial primary key,
    user_id        uuid not null references public.profiles(id)   on delete cascade,
    permission_id  int  not null references public.permissions(id) on delete cascade,
    allowed        boolean not null,             -- true = add, false = remove
    reason         text,
    set_by         uuid references public.profiles(id),
    set_at         timestamptz not null default now(),
    unique (user_id, permission_id)
);

-- 3.4 privacy_settings: the "security shield" toggles from the Personal Dashboard
create table if not exists public.privacy_settings (
    user_id            uuid primary key references public.profiles(id) on delete cascade,
    payment_shield_on  boolean not null default true,   -- hide payment numbers in UI
    hide_profile       boolean not null default false,
    hide_earnings      boolean not null default false,
    updated_at         timestamptz not null default now()
);

-- 3.5 payment_accounts: JazzCash / EasyPaisa / card details (per user)
create table if not exists public.payment_accounts (
    id             bigserial primary key,
    user_id        uuid not null references public.profiles(id) on delete cascade,
    method         withdrawal_method not null,
    account_title  text not null,
    account_number text not null,                -- store masked / encrypted in app layer if possible
    is_default     boolean not null default false,
    is_verified    boolean not null default false,
    shield_on      boolean not null default true,
    created_at     timestamptz not null default now()
);
create index if not exists idx_payment_accounts_user on public.payment_accounts(user_id);

-- 3.6 pro_subscriptions: paid Pro membership (ad-free + pro services)
create table if not exists public.pro_subscriptions (
    id            bigserial primary key,
    user_id       uuid not null references public.profiles(id) on delete cascade,
    status        subscription_status not null default 'active',
    started_at    timestamptz not null default now(),
    expires_at    timestamptz not null,
    price         numeric(18,4) not null default 0,
    currency      text not null default 'PKR',
    payment_ref   text,
    created_at    timestamptz not null default now()
);
create index if not exists idx_pro_sub_user on public.pro_subscriptions(user_id);

-- -----------------------------------------------------------------------------
-- 4. GAMES (multi-project: Ludo, Goli Danda, Kali Bandar, Smart Flow, Study)
-- -----------------------------------------------------------------------------
create table if not exists public.games (
    id          serial primary key,
    code        text not null unique,           -- 'ludo','goli_danda','kali_bandar','smart_flow','study'
    name        text not null,
    category    text,                           -- 'board','arcade','tycoon','education'
    is_active   boolean not null default true,
    created_at  timestamptz not null default now()
);

-- 4.1 linked_apps: which external game accounts a user has connected
create table if not exists public.linked_apps (
    id                bigserial primary key,
    user_id           uuid not null references public.profiles(id) on delete cascade,
    game_id           int  not null references public.games(id)    on delete cascade,
    external_user_ref text,
    linked_at         timestamptz not null default now(),
    unique (user_id, game_id)
);

-- 4.2 game_sessions: one row per match / play session
create table if not exists public.game_sessions (
    id                bigserial primary key,
    user_id           uuid not null references public.profiles(id) on delete cascade,
    game_id           int  not null references public.games(id),
    started_at        timestamptz not null default now(),
    ended_at          timestamptz,
    result            text,                     -- 'win','loss','draw','abandoned'
    score             int  not null default 0,
    hero_coins_earned numeric(18,4) not null default 0,
    xp_earned         int  not null default 0
);
create index if not exists idx_game_sessions_user on public.game_sessions(user_id, started_at desc);

-- 4.3 player_stats: rolled-up stats per (user, game) -> feeds leaderboards
create table if not exists public.player_stats (
    user_id     uuid not null references public.profiles(id) on delete cascade,
    game_id     int  not null references public.games(id)    on delete cascade,
    level       int  not null default 1,
    xp          int  not null default 0,
    wins        int  not null default 0,
    losses      int  not null default 0,
    matches     int  not null default 0,
    updated_at  timestamptz not null default now(),
    primary key (user_id, game_id)
);

-- -----------------------------------------------------------------------------
-- 5. AD TRACKING ENGINE
-- -----------------------------------------------------------------------------

-- 5.1 ad_providers: AdMob, Unity Ads, ironSource, etc.
create table if not exists public.ad_providers (
    id          serial primary key,
    code        text not null unique,
    name        text not null,
    is_active   boolean not null default true
);

-- 5.2 ad_rate_config: the CEO/Manager "Per Ad Rate" control (kept as HISTORY).
--     The row with the newest effective_from that is <= now() is "current".
create table if not exists public.ad_rate_config (
    id             bigserial primary key,
    rate_per_ad    numeric(18,6) not null check (rate_per_ad >= 0),
    currency       text not null default 'PKR',
    effective_from timestamptz not null default now(),
    note           text,
    created_by     uuid references public.profiles(id),
    created_at     timestamptz not null default now()
);
create index if not exists idx_ad_rate_effective on public.ad_rate_config(effective_from desc);

-- 5.3 payout_runs: one row per day the cron job runs (spec section 2)
create table if not exists public.payout_runs (
    id           bigserial primary key,
    run_date     date not null unique,
    status       payout_run_status not null default 'pending',
    rate_used    numeric(18,6),
    currency     text not null default 'PKR',
    total_users  int not null default 0,
    total_ads    bigint not null default 0,
    total_amount numeric(18,4) not null default 0,
    triggered_by uuid references public.profiles(id),  -- null = automatic cron
    started_at   timestamptz,
    finished_at  timestamptz,
    created_at   timestamptz not null default now()
);

-- 5.4 ad_impressions: the Real-Time Impression Logger (spec section 2)
--     Required params: User_ID, Ad_Provider_ID, Timestamp, Earned_Status.
create table if not exists public.ad_impressions (
    id                     bigserial primary key,
    user_id                uuid not null references public.profiles(id) on delete cascade,
    ad_provider_id         int  not null references public.ad_providers(id),
    game_id                int  references public.games(id),
    placement              text,                       -- 'rewarded_spin','level_complete', ...
    watched_at             timestamptz not null default now(),
    duration_seconds       int not null default 0,
    earned_status          ad_earned_status not null default 'pending',
    rate_snapshot          numeric(18,6),              -- rate at the moment it was logged
    currency               text not null default 'PKR',
    device_id              text,
    ip_address             inet,
    dedupe_key             text,                       -- app-generated; blocks double logging
    is_counted_for_payout  boolean not null default false,
    payout_run_id          bigint references public.payout_runs(id),
    created_at             timestamptz not null default now()
);
create unique index if not exists uidx_ad_impressions_dedupe
    on public.ad_impressions(user_id, dedupe_key)
    where dedupe_key is not null;
create index if not exists idx_ad_impressions_user_day
    on public.ad_impressions(user_id, watched_at);
create index if not exists idx_ad_impressions_uncounted
    on public.ad_impressions(is_counted_for_payout)
    where is_counted_for_payout = false;

-- 5.5 daily_earnings: cron output -> per user, per day (ads * rate)
create table if not exists public.daily_earnings (
    id             bigserial primary key,
    payout_run_id  bigint not null references public.payout_runs(id) on delete cascade,
    user_id        uuid not null references public.profiles(id) on delete cascade,
    run_date       date not null,
    ads_counted    int not null default 0,
    rate_used      numeric(18,6) not null default 0,
    currency       text not null default 'PKR',
    gross_amount   numeric(18,4) not null default 0,
    wallet_txn_id  bigint,                              -- FK added after wallet_transactions
    created_at     timestamptz not null default now(),
    unique (user_id, run_date)
);

-- -----------------------------------------------------------------------------
-- 6. WALLETS  (3 SEGREGATED WALLETS + APPEND-ONLY LEDGER)
-- -----------------------------------------------------------------------------

-- 6.1 wallets: exactly one row per (user, wallet_type). balance can never go < 0.
create table if not exists public.wallets (
    id          bigserial primary key,
    user_id     uuid not null references public.profiles(id) on delete cascade,
    wallet_type wallet_type not null,
    balance     numeric(18,4) not null default 0 check (balance >= 0),
    currency    text not null default 'PKR',
    is_frozen   boolean not null default false,
    updated_at  timestamptz not null default now(),
    unique (user_id, wallet_type)
);
create index if not exists idx_wallets_user on public.wallets(user_id);

-- 6.2 wallet_transactions: the LEDGER. Never UPDATE/DELETE rows here — only INSERT.
--     Every balance change in the whole system must create a row here.
create table if not exists public.wallet_transactions (
    id             bigserial primary key,
    wallet_id      bigint not null references public.wallets(id) on delete cascade,
    user_id        uuid   not null references public.profiles(id) on delete cascade,
    direction      txn_direction not null,
    category       txn_category  not null,
    amount         numeric(18,4) not null check (amount > 0),
    balance_before numeric(18,4) not null,
    balance_after  numeric(18,4) not null,
    currency       text not null default 'PKR',
    reference_type text,                          -- 'ad_impression','payout_run','escrow_trade', ...
    reference_id   text,
    description    text,
    created_by     uuid references public.profiles(id),  -- null = system
    created_at     timestamptz not null default now()
);
create index if not exists idx_wallet_txn_wallet on public.wallet_transactions(wallet_id, created_at desc);
create index if not exists idx_wallet_txn_user   on public.wallet_transactions(user_id, created_at desc);

-- now we can link daily_earnings -> wallet_transactions
alter table public.daily_earnings
    drop constraint if exists fk_daily_earnings_txn;
alter table public.daily_earnings
    add constraint fk_daily_earnings_txn
    foreign key (wallet_txn_id) references public.wallet_transactions(id);

-- -----------------------------------------------------------------------------
-- 7. INTERNAL COIN ECONOMY (conversion, deposit, withdrawal)
-- -----------------------------------------------------------------------------

-- 7.1 conversion_rates: manager-regulated exchange rates (cash -> building/hero)
create table if not exists public.conversion_rates (
    id             bigserial primary key,
    from_wallet    wallet_type not null,
    to_wallet      wallet_type not null,
    rate           numeric(18,6) not null check (rate > 0),  -- 1 from_coin = `rate` to_coins
    min_amount     numeric(18,4) not null default 0,
    max_amount     numeric(18,4),
    is_active      boolean not null default true,
    effective_from timestamptz not null default now(),
    set_by         uuid references public.profiles(id),
    note           text,
    check (from_wallet <> to_wallet)
);

-- 7.2 coin_conversions: a completed conversion (creates 2 ledger rows: out + in)
create table if not exists public.coin_conversions (
    id           bigserial primary key,
    user_id      uuid not null references public.profiles(id) on delete cascade,
    from_wallet  wallet_type not null,
    to_wallet    wallet_type not null,
    from_amount  numeric(18,4) not null check (from_amount > 0),
    rate_used    numeric(18,6) not null,
    to_amount    numeric(18,4) not null,
    from_txn_id  bigint references public.wallet_transactions(id),
    to_txn_id    bigint references public.wallet_transactions(id),
    created_at   timestamptz not null default now()
);
create index if not exists idx_coin_conversions_user on public.coin_conversions(user_id, created_at desc);

-- 7.3 deposits: money coming IN (JazzCash / EasyPaisa / card top-up)
create table if not exists public.deposits (
    id            bigserial primary key,
    user_id       uuid not null references public.profiles(id) on delete cascade,
    method        withdrawal_method not null,
    amount        numeric(18,4) not null check (amount > 0),
    currency      text not null default 'PKR',
    gateway_ref   text,
    status        deposit_status not null default 'initiated',
    wallet_txn_id bigint references public.wallet_transactions(id),
    created_at    timestamptz not null default now(),
    completed_at  timestamptz
);
create index if not exists idx_deposits_user on public.deposits(user_id, created_at desc);

-- 7.4 withdrawals: cash-out requests from the Cash wallet (needs manager review)
create table if not exists public.withdrawals (
    id                 bigserial primary key,
    user_id            uuid not null references public.profiles(id) on delete cascade,
    payment_account_id bigint references public.payment_accounts(id),
    method             withdrawal_method not null,
    amount             numeric(18,4) not null check (amount > 0),
    fee                numeric(18,4) not null default 0,
    net_amount         numeric(18,4) not null,
    currency           text not null default 'PKR',
    status             withdrawal_status not null default 'requested',
    gateway_ref        text,
    wallet_txn_id      bigint references public.wallet_transactions(id),
    reviewed_by        uuid references public.profiles(id),
    reviewed_at        timestamptz,
    note               text,
    requested_at       timestamptz not null default now()
);
create index if not exists idx_withdrawals_user   on public.withdrawals(user_id, requested_at desc);
create index if not exists idx_withdrawals_status on public.withdrawals(status);

-- -----------------------------------------------------------------------------
-- 8. SECURE ESCROW P2P MARKETPLACE
--    Direct user->user transfers are DISABLED. Every trade goes through escrow.
-- -----------------------------------------------------------------------------

-- 8.1 marketplace_listings: a seller offers X coins for Y cash
create table if not exists public.marketplace_listings (
    id           bigserial primary key,
    seller_id    uuid not null references public.profiles(id) on delete cascade,
    wallet_type  wallet_type not null,                 -- which coin is being sold
    coin_amount  numeric(18,4) not null check (coin_amount > 0),
    price_cash   numeric(18,4) not null check (price_cash > 0),
    currency     text not null default 'PKR',
    status       listing_status not null default 'active',
    created_at   timestamptz not null default now(),
    expires_at   timestamptz
);
create index if not exists idx_listings_status on public.marketplace_listings(status);

-- 8.2 escrow_trades: the holding protocol. Coins/cash are HELD until both sides done.
create table if not exists public.escrow_trades (
    id                     bigserial primary key,
    listing_id             bigint references public.marketplace_listings(id),
    buyer_id               uuid not null references public.profiles(id),
    seller_id              uuid not null references public.profiles(id),
    wallet_type            wallet_type not null,
    coin_amount            numeric(18,4) not null check (coin_amount > 0),
    price_cash             numeric(18,4) not null check (price_cash > 0),
    currency               text not null default 'PKR',
    status                 escrow_status not null default 'created',
    buyer_cash_hold_txn_id bigint references public.wallet_transactions(id),
    seller_coin_hold_txn_id bigint references public.wallet_transactions(id),
    release_coin_txn_id    bigint references public.wallet_transactions(id),
    release_cash_txn_id    bigint references public.wallet_transactions(id),
    refund_txn_id          bigint references public.wallet_transactions(id),
    manager_id             uuid references public.profiles(id),   -- oversight
    notes                  text,
    created_at             timestamptz not null default now(),
    updated_at             timestamptz not null default now()
);
create index if not exists idx_escrow_status on public.escrow_trades(status);
create index if not exists idx_escrow_buyer  on public.escrow_trades(buyer_id);
create index if not exists idx_escrow_seller on public.escrow_trades(seller_id);

-- 8.3 escrow_events: full timeline of every escrow trade (who did what, when)
create table if not exists public.escrow_events (
    id         bigserial primary key,
    trade_id   bigint not null references public.escrow_trades(id) on delete cascade,
    event      text not null,                    -- 'created','buyer_funded','seller_locked','released','refunded','disputed'
    actor_id   uuid references public.profiles(id),
    detail     jsonb,
    created_at timestamptz not null default now()
);

-- -----------------------------------------------------------------------------
-- 9. SECURITY, AUDIT, ADMIN SETTINGS, SUPPORT
-- -----------------------------------------------------------------------------

-- 9.1 account_freezes: manager "Freeze Account / Freeze Coins" toggle history
create table if not exists public.account_freezes (
    id           bigserial primary key,
    user_id      uuid not null references public.profiles(id) on delete cascade,
    freeze_type  freeze_type not null,
    is_active    boolean not null default true,
    reason       text,
    frozen_by    uuid references public.profiles(id),
    frozen_at    timestamptz not null default now(),
    unfrozen_by  uuid references public.profiles(id),
    unfrozen_at  timestamptz
);
create index if not exists idx_freezes_active on public.account_freezes(user_id) where is_active;

-- 9.2 audit_logs: system-wide audit trail (spec: CEO "system audit logs")
create table if not exists public.audit_logs (
    id          bigserial primary key,
    actor_id    uuid references public.profiles(id),
    actor_role  text,
    action      text not null,                   -- 'ad_rate.update','user.freeze','escrow.release', ...
    entity_type text,
    entity_id   text,
    before      jsonb,
    after       jsonb,
    ip_address  inet,
    user_agent  text,
    created_at  timestamptz not null default now()
);
create index if not exists idx_audit_actor  on public.audit_logs(actor_id, created_at desc);
create index if not exists idx_audit_action on public.audit_logs(action, created_at desc);

-- 9.3 admin_settings: master controls (key/value). e.g. min_withdrawal, escrow_fee_pct
create table if not exists public.admin_settings (
    key         text primary key,
    value       jsonb not null,
    description text,
    updated_by  uuid references public.profiles(id),
    updated_at  timestamptz not null default now()
);

-- 9.4 support_tickets + messages
create table if not exists public.support_tickets (
    id          bigserial primary key,
    user_id     uuid not null references public.profiles(id) on delete cascade,
    subject     text not null,
    status      ticket_status not null default 'open',
    priority    text not null default 'normal',
    assigned_to uuid references public.profiles(id),
    created_at  timestamptz not null default now(),
    updated_at  timestamptz not null default now()
);
create table if not exists public.support_ticket_messages (
    id         bigserial primary key,
    ticket_id  bigint not null references public.support_tickets(id) on delete cascade,
    sender_id  uuid references public.profiles(id),
    body       text not null,
    created_at timestamptz not null default now()
);

-- -----------------------------------------------------------------------------
-- 10. HELPER FUNCTIONS  (used by Row Level Security policies)
-- -----------------------------------------------------------------------------

-- 10.1 the calling user's primary role code
create or replace function public.current_role_code()
returns text
language sql
stable
security definer
set search_path = public
as $$
    select r.code
    from public.profiles p
    join public.roles r on r.id = p.primary_role_id
    where p.id = auth.uid();
$$;

-- 10.2 is the caller staff (ceo or any manager)?
create or replace function public.is_staff()
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    select coalesce(public.current_role_code() in ('ceo','manager_t1','manager_t2'), false);
$$;

-- 10.3 does the caller have a permission? (role matrix + per-user overrides)
create or replace function public.has_permission(p_code text)
returns boolean
language sql
stable
security definer
set search_path = public
as $$
    with base as (
        select 1
        from public.profiles pr
        join public.role_permissions rp on rp.role_id = pr.primary_role_id
        join public.permissions pm on pm.id = rp.permission_id
        where pr.id = auth.uid() and pm.code = p_code
    ),
    ovr as (
        select spo.allowed
        from public.staff_permission_overrides spo
        join public.permissions pm on pm.id = spo.permission_id
        where spo.user_id = auth.uid() and pm.code = p_code
    )
    select case
        when exists (select 1 from ovr where allowed = false) then false
        when exists (select 1 from ovr where allowed = true)  then true
        else exists (select 1 from base)
    end;
$$;

-- -----------------------------------------------------------------------------
-- 11. updated_at TRIGGER  (auto-refresh the updated_at column on UPDATE)
-- -----------------------------------------------------------------------------
create or replace function public.set_updated_at()
returns trigger language plpgsql as $$
begin
    new.updated_at = now();
    return new;
end $$;

do $$
declare t text;
begin
    foreach t in array array[
        'profiles','privacy_settings','wallets','player_stats',
        'escrow_trades','support_tickets'
    ]
    loop
        execute format('drop trigger if exists trg_%s_updated_at on public.%I;', t, t);
        execute format(
            'create trigger trg_%s_updated_at before update on public.%I
             for each row execute function public.set_updated_at();', t, t);
    end loop;
end $$;

-- 11.1 auto-create profile + 3 wallets + privacy row whenever Auth adds a user
create or replace function public.handle_new_user()
returns trigger
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into public.profiles (id, username, full_name)
    values (new.id, split_part(new.email, '@', 1), new.raw_user_meta_data->>'full_name')
    on conflict (id) do nothing;

    insert into public.privacy_settings (user_id) values (new.id)
    on conflict (user_id) do nothing;

    insert into public.wallets (user_id, wallet_type) values
        (new.id, 'cash'), (new.id, 'building'), (new.id, 'hero')
    on conflict (user_id, wallet_type) do nothing;

    return new;
end $$;

drop trigger if exists on_auth_user_created on auth.users;
create trigger on_auth_user_created
    after insert on auth.users
    for each row execute function public.handle_new_user();

-- -----------------------------------------------------------------------------
-- 12. ROW LEVEL SECURITY (RLS)
--     Rule of thumb:
--       * a normal user may READ their own rows only
--       * staff (ceo/managers) may read everything
--       * money tables: NO direct client writes -> only server (service_role) or
--         SECURITY DEFINER functions (built in Phase 2) may write.
-- -----------------------------------------------------------------------------
alter table public.profiles                  enable row level security;
alter table public.privacy_settings          enable row level security;
alter table public.payment_accounts          enable row level security;
alter table public.pro_subscriptions         enable row level security;
alter table public.user_roles                enable row level security;
alter table public.staff_permission_overrides enable row level security;
alter table public.linked_apps               enable row level security;
alter table public.game_sessions             enable row level security;
alter table public.player_stats              enable row level security;
alter table public.ad_impressions            enable row level security;
alter table public.daily_earnings            enable row level security;
alter table public.wallets                   enable row level security;
alter table public.wallet_transactions       enable row level security;
alter table public.coin_conversions          enable row level security;
alter table public.deposits                  enable row level security;
alter table public.withdrawals               enable row level security;
alter table public.marketplace_listings      enable row level security;
alter table public.escrow_trades             enable row level security;
alter table public.escrow_events             enable row level security;
alter table public.account_freezes           enable row level security;
alter table public.audit_logs                enable row level security;
alter table public.support_tickets           enable row level security;
alter table public.support_ticket_messages   enable row level security;

-- reference tables everyone signed-in may read
alter table public.roles            enable row level security;
alter table public.permissions      enable row level security;
alter table public.role_permissions enable row level security;
alter table public.games            enable row level security;
alter table public.ad_providers     enable row level security;
alter table public.ad_rate_config   enable row level security;
alter table public.conversion_rates enable row level security;
alter table public.admin_settings   enable row level security;

-- ---- profiles ----
drop policy if exists p_profiles_self_read on public.profiles;
create policy p_profiles_self_read on public.profiles
    for select using (id = auth.uid() or public.is_staff());

drop policy if exists p_profiles_self_update on public.profiles;
create policy p_profiles_self_update on public.profiles
    for update using (id = auth.uid())
    with check (id = auth.uid());

-- ---- generic "own row" read for user-owned tables ----
do $$
declare t text;
begin
    foreach t in array array[
        'privacy_settings','payment_accounts','pro_subscriptions','user_roles',
        'linked_apps','game_sessions','player_stats','ad_impressions',
        'daily_earnings','wallets','wallet_transactions','coin_conversions',
        'deposits','withdrawals','account_freezes','support_tickets'
    ]
    loop
        execute format('drop policy if exists p_%s_owner_read on public.%I;', t, t);
        execute format(
            'create policy p_%s_owner_read on public.%I
             for select using (user_id = auth.uid() or public.is_staff());', t, t);
    end loop;
end $$;

-- ---- user may edit only their own low-risk tables ----
drop policy if exists p_privacy_write on public.privacy_settings;
create policy p_privacy_write on public.privacy_settings
    for all using (user_id = auth.uid()) with check (user_id = auth.uid());

drop policy if exists p_payment_acc_write on public.payment_accounts;
create policy p_payment_acc_write on public.payment_accounts
    for all using (user_id = auth.uid()) with check (user_id = auth.uid());

-- ---- escrow: buyer, seller or staff may read ----
drop policy if exists p_escrow_read on public.escrow_trades;
create policy p_escrow_read on public.escrow_trades
    for select using (buyer_id = auth.uid() or seller_id = auth.uid() or public.is_staff());

drop policy if exists p_escrow_events_read on public.escrow_events;
create policy p_escrow_events_read on public.escrow_events
    for select using (
        public.is_staff() or exists (
            select 1 from public.escrow_trades t
            where t.id = escrow_events.trade_id
              and (t.buyer_id = auth.uid() or t.seller_id = auth.uid())
        ));

-- ---- marketplace listings: anyone signed in may browse active ones ----
drop policy if exists p_listings_read on public.marketplace_listings;
create policy p_listings_read on public.marketplace_listings
    for select using (status = 'active' or seller_id = auth.uid() or public.is_staff());

drop policy if exists p_listings_owner_write on public.marketplace_listings;
create policy p_listings_owner_write on public.marketplace_listings
    for all using (seller_id = auth.uid()) with check (seller_id = auth.uid());

-- ---- support messages ----
drop policy if exists p_ticket_msg_read on public.support_ticket_messages;
create policy p_ticket_msg_read on public.support_ticket_messages
    for select using (
        public.is_staff() or exists (
            select 1 from public.support_tickets s
            where s.id = support_ticket_messages.ticket_id and s.user_id = auth.uid()
        ));

-- ---- reference tables: readable by all signed-in users ----
do $$
declare t text;
begin
    foreach t in array array[
        'roles','permissions','role_permissions','games','ad_providers',
        'ad_rate_config','conversion_rates','admin_settings'
    ]
    loop
        execute format('drop policy if exists p_%s_read_all on public.%I;', t, t);
        execute format(
            'create policy p_%s_read_all on public.%I
             for select using (auth.role() = ''authenticated'');', t, t);
    end loop;
end $$;

-- ---- staff-only write on admin/config tables ----
drop policy if exists p_ad_rate_staff_write on public.ad_rate_config;
create policy p_ad_rate_staff_write on public.ad_rate_config
    for insert with check (public.has_permission('ad_rate.update'));

drop policy if exists p_admin_settings_write on public.admin_settings;
create policy p_admin_settings_write on public.admin_settings
    for all using (public.has_permission('system.settings.update'))
    with check (public.has_permission('system.settings.update'));

drop policy if exists p_freeze_staff_write on public.account_freezes;
create policy p_freeze_staff_write on public.account_freezes
    for all using (public.has_permission('user.freeze'))
    with check (public.has_permission('user.freeze'));

drop policy if exists p_audit_staff_read on public.audit_logs;
create policy p_audit_staff_read on public.audit_logs
    for select using (public.has_permission('system.audit.read'));

-- =============================================================================
--  SEED DATA
-- =============================================================================

-- roles (id order matters: profiles.primary_role_id defaults to 5 = free)
insert into public.roles (id, code, name, tier, description) values
    (1,'ceo',        'CEO / Super Admin', 100, 'Full system control'),
    (2,'manager_t1', 'Manager Tier 1',     60, 'Senior manager'),
    (3,'manager_t2', 'Manager Tier 2',     40, 'Junior manager / support'),
    (4,'pro',        'Pro Member',         20, 'Paid, ad-free'),
    (5,'free',       'Free User / Player',  10, 'Watches ads, earns revenue')
on conflict (id) do nothing;
select setval(pg_get_serial_sequence('public.roles','id'), 5, true);

-- permissions
insert into public.permissions (code, category, description) values
    ('ad_rate.update',            'ads',    'Set / change the per-ad rate'),
    ('ad_logs.read',              'ads',    'View ad impression logs'),
    ('payout.run',                'ads',    'Trigger / re-run daily payout'),
    ('user.read',                 'users',  'View any user account'),
    ('user.update',               'users',  'Edit user accounts'),
    ('user.freeze',               'users',  'Freeze account or coins'),
    ('staff.permissions.manage',  'users',  'Use the staff add/remove matrix'),
    ('wallet.adjust',             'wallet', 'Manual wallet credit / debit'),
    ('withdrawal.review',         'wallet', 'Approve / reject withdrawals'),
    ('conversion_rate.update',    'wallet', 'Change coin conversion rates'),
    ('escrow.oversee',            'escrow', 'Release / refund / resolve disputes'),
    ('system.settings.update',    'system', 'Change master admin settings'),
    ('system.audit.read',         'system', 'Read system audit logs'),
    ('analytics.global.read',     'system', 'View global revenue / analytics')
on conflict (code) do nothing;

-- CEO gets every permission
insert into public.role_permissions (role_id, permission_id)
select 1, id from public.permissions
on conflict do nothing;

-- Manager T1: everything except system settings
insert into public.role_permissions (role_id, permission_id)
select 2, id from public.permissions
where code in ('ad_rate.update','ad_logs.read','payout.run','user.read','user.update',
               'user.freeze','staff.permissions.manage','wallet.adjust','withdrawal.review',
               'conversion_rate.update','escrow.oversee','system.audit.read','analytics.global.read')
on conflict do nothing;

-- Manager T2: read + support level
insert into public.role_permissions (role_id, permission_id)
select 3, id from public.permissions
where code in ('ad_logs.read','user.read','user.freeze','withdrawal.review','escrow.oversee')
on conflict do nothing;

-- games
insert into public.games (code, name, category) values
    ('ludo',        'Ludo',              'board'),
    ('goli_danda',  'Goli Danda',        'arcade'),
    ('kali_bandar', 'Kali Bandar',       'arcade'),
    ('smart_flow',  'Tangent Smart Flow','tycoon'),
    ('study',       'Tangent Study',     'education')
on conflict (code) do nothing;

-- ad providers
insert into public.ad_providers (code, name) values
    ('admob','Google AdMob'),
    ('unity','Unity Ads'),
    ('ironsource','ironSource')
on conflict (code) do nothing;

-- starting ad rate (CEO can change later from dashboard)
insert into public.ad_rate_config (rate_per_ad, currency, note)
select 0.500000, 'PKR', 'Initial default rate'
where not exists (select 1 from public.ad_rate_config);

-- default conversion rates
insert into public.conversion_rates (from_wallet, to_wallet, rate, min_amount, note) values
    ('cash','building', 10.0, 1, 'Default: 1 cash coin = 10 building coins'),
    ('cash','hero',      8.0, 1, 'Default: 1 cash coin = 8 hero coins')
on conflict do nothing;

-- master settings
insert into public.admin_settings (key, value, description) values
    ('min_withdrawal_pkr',   '200',   'Minimum cash-out amount'),
    ('withdrawal_fee_pct',   '2.5',   'Percent fee on withdrawals'),
    ('escrow_fee_pct',       '3.0',   'Percent fee on escrow trades'),
    ('daily_payout_time_utc','"19:00"','When the payout cron runs (UTC)'),
    ('max_ads_per_day',      '80',    'Anti-abuse cap on counted ads/day')
on conflict (key) do nothing;

-- =============================================================================
--  END OF PHASE 1
-- =============================================================================
