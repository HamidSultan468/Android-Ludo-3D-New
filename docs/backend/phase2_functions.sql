-- =============================================================================
--  TANGENT LUDO EMPIRE  —  PHASE 2  BACKEND LOGIC (RPC functions)
--  Target: Supabase (PostgreSQL). Run AFTER phase1_schema.sql.
--
--  These are the only safe way the app changes money/state.
--  The app calls them with:  supabase.rpc('function_name', { ...params })
--
--  All money functions are SECURITY DEFINER: they run with full rights but
--  check the caller (auth.uid()) and permissions INSIDE the function.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 0. CORE HELPER  —  every balance change goes through this one function
-- -----------------------------------------------------------------------------
create or replace function public._apply_wallet_txn(
    p_user_id        uuid,
    p_wallet_type    wallet_type,
    p_direction      txn_direction,
    p_category       txn_category,
    p_amount         numeric,
    p_currency       text  default 'PKR',
    p_reference_type text  default null,
    p_reference_id   text  default null,
    p_description    text  default null,
    p_created_by     uuid  default null,
    p_allow_frozen   boolean default false
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    v_wallet   public.wallets%rowtype;
    v_before   numeric(18,4);
    v_after    numeric(18,4);
    v_txn_id   bigint;
begin
    if p_amount is null or p_amount <= 0 then
        raise exception 'amount must be > 0';
    end if;

    -- lock this wallet row so two requests cannot race
    select * into v_wallet
    from public.wallets
    where user_id = p_user_id and wallet_type = p_wallet_type
    for update;

    if not found then
        raise exception 'wallet %/% not found', p_user_id, p_wallet_type;
    end if;

    if v_wallet.is_frozen and not p_allow_frozen then
        raise exception 'wallet is frozen';
    end if;

    v_before := v_wallet.balance;
    if p_direction = 'credit' then
        v_after := v_before + p_amount;
    else
        v_after := v_before - p_amount;
        if v_after < 0 then
            raise exception 'insufficient balance: have %, need %', v_before, p_amount;
        end if;
    end if;

    insert into public.wallet_transactions (
        wallet_id, user_id, direction, category, amount,
        balance_before, balance_after, currency,
        reference_type, reference_id, description, created_by
    ) values (
        v_wallet.id, p_user_id, p_direction, p_category, p_amount,
        v_before, v_after, p_currency,
        p_reference_type, p_reference_id, p_description, p_created_by
    ) returning id into v_txn_id;

    update public.wallets
    set balance = v_after, updated_at = now()
    where id = v_wallet.id;

    return v_txn_id;
end $$;
revoke all on function public._apply_wallet_txn(uuid,wallet_type,txn_direction,txn_category,numeric,text,text,text,text,uuid,boolean) from public, anon, authenticated;

-- small util: throw if the caller's account is frozen
create or replace function public._assert_not_frozen(p_user_id uuid)
returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if exists (select 1 from public.profiles where id = p_user_id and status <> 'active') then
        raise exception 'account is not active';
    end if;
end $$;
revoke all on function public._assert_not_frozen(uuid) from public, anon, authenticated;

-- small util: write an audit row
create or replace function public._audit(
    p_action text, p_entity_type text, p_entity_id text,
    p_before jsonb default null, p_after jsonb default null
) returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    insert into public.audit_logs (actor_id, actor_role, action, entity_type, entity_id, before, after)
    values (auth.uid(), public.current_role_code(), p_action, p_entity_type, p_entity_id, p_before, p_after);
end $$;
revoke all on function public._audit(text,text,text,jsonb,jsonb) from public, anon, authenticated;

-- -----------------------------------------------------------------------------
-- 1. AD ENGINE
-- -----------------------------------------------------------------------------

-- 1.1 current per-ad rate (newest config row that is already in effect)
create or replace function public.get_current_ad_rate()
returns table (rate_per_ad numeric, currency text)
language sql
stable
security definer
set search_path = public
as $$
    select rate_per_ad, currency
    from public.ad_rate_config
    where effective_from <= now()
    order by effective_from desc, id desc
    limit 1;
$$;
grant execute on function public.get_current_ad_rate() to authenticated;

-- 1.2 log one watched ad. Does NOT pay cash now — the daily payout (Phase 3) does.
--     Returns: { impression_id, counted, reason }
create or replace function public.log_ad_impression(
    p_provider_code text,
    p_game_code     text default null,
    p_placement     text default null,
    p_dedupe_key    text default null,
    p_duration_seconds int default 0,
    p_device_id     text default null
) returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user     uuid := auth.uid();
    v_provider int;
    v_game     int;
    v_rate     numeric(18,6);
    v_currency text;
    v_cap      int;
    v_today    int;
    v_id       bigint;
begin
    if v_user is null then raise exception 'not authenticated'; end if;
    perform public._assert_not_frozen(v_user);

    select id into v_provider from public.ad_providers where code = p_provider_code and is_active;
    if v_provider is null then raise exception 'unknown ad provider %', p_provider_code; end if;

    if p_game_code is not null then
        select id into v_game from public.games where code = p_game_code;
    end if;

    select rate_per_ad, currency into v_rate, v_currency from public.get_current_ad_rate();
    v_currency := coalesce(v_currency, 'PKR');

    -- daily anti-abuse cap
    select coalesce((value)::int, 80) into v_cap from public.admin_settings where key = 'max_ads_per_day';
    select count(*) into v_today
    from public.ad_impressions
    where user_id = v_user
      and watched_at >= date_trunc('day', now())
      and earned_status = 'earned';

    begin
        insert into public.ad_impressions (
            user_id, ad_provider_id, game_id, placement, watched_at,
            duration_seconds, earned_status, rate_snapshot, currency,
            device_id, dedupe_key
        ) values (
            v_user, v_provider, v_game, p_placement, now(),
            greatest(p_duration_seconds, 0),
            case when v_today >= v_cap then 'rejected' else 'earned' end,
            v_rate, v_currency, p_device_id, p_dedupe_key
        ) returning id into v_id;
    exception when unique_violation then
        return jsonb_build_object('impression_id', null, 'counted', false, 'reason', 'duplicate');
    end;

    return jsonb_build_object(
        'impression_id', v_id,
        'counted', (v_today < v_cap),
        'reason', case when v_today >= v_cap then 'daily_cap_reached' else 'ok' end
    );
end $$;
grant execute on function public.log_ad_impression(text,text,text,text,int,text) to authenticated;

-- 1.3 CEO / Manager sets a new per-ad rate (takes effect now or in the future)
create or replace function public.set_ad_rate(
    p_rate numeric, p_currency text default 'PKR',
    p_effective_from timestamptz default now(), p_note text default null
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare v_id bigint;
begin
    if not public.has_permission('ad_rate.update') then
        raise exception 'permission denied: ad_rate.update';
    end if;
    if p_rate < 0 then raise exception 'rate must be >= 0'; end if;

    insert into public.ad_rate_config (rate_per_ad, currency, effective_from, note, created_by)
    values (p_rate, coalesce(p_currency,'PKR'), coalesce(p_effective_from, now()), p_note, auth.uid())
    returning id into v_id;

    perform public._audit('ad_rate.update', 'ad_rate_config', v_id::text,
        null, jsonb_build_object('rate', p_rate, 'currency', p_currency));
    return v_id;
end $$;
grant execute on function public.set_ad_rate(numeric,text,timestamptz,text) to authenticated;

-- -----------------------------------------------------------------------------
-- 2. COIN ECONOMY
-- -----------------------------------------------------------------------------

-- 2.1 convert CASH coins into BUILDING or HERO coins at the manager-set rate
--     Returns: { conversion_id, from_amount, to_amount, rate }
create or replace function public.convert_coins(
    p_to_wallet wallet_type,
    p_amount    numeric
) returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user   uuid := auth.uid();
    v_rate   public.conversion_rates%rowtype;
    v_to_amt numeric(18,4);
    v_out    bigint;
    v_in     bigint;
    v_conv   bigint;
begin
    if v_user is null then raise exception 'not authenticated'; end if;
    perform public._assert_not_frozen(v_user);

    if p_to_wallet not in ('building','hero') then
        raise exception 'can only convert cash -> building or hero';
    end if;
    if p_amount is null or p_amount <= 0 then raise exception 'amount must be > 0'; end if;

    select * into v_rate
    from public.conversion_rates
    where from_wallet = 'cash' and to_wallet = p_to_wallet
      and is_active and effective_from <= now()
    order by effective_from desc, id desc
    limit 1;
    if not found then raise exception 'no active rate for cash -> %', p_to_wallet; end if;

    if p_amount < v_rate.min_amount then
        raise exception 'minimum is %', v_rate.min_amount;
    end if;
    if v_rate.max_amount is not null and p_amount > v_rate.max_amount then
        raise exception 'maximum is %', v_rate.max_amount;
    end if;

    v_to_amt := round(p_amount * v_rate.rate, 4);

    v_out := public._apply_wallet_txn(v_user, 'cash', 'debit', 'conversion_out', p_amount,
             'PKR', 'coin_conversion', null, format('convert to %s', p_to_wallet), v_user);
    v_in  := public._apply_wallet_txn(v_user, p_to_wallet, 'credit', 'conversion_in', v_to_amt,
             'PKR', 'coin_conversion', null, 'convert from cash', v_user);

    insert into public.coin_conversions (user_id, from_wallet, to_wallet, from_amount, rate_used, to_amount, from_txn_id, to_txn_id)
    values (v_user, 'cash', p_to_wallet, p_amount, v_rate.rate, v_to_amt, v_out, v_in)
    returning id into v_conv;

    update public.wallet_transactions set reference_id = v_conv::text where id in (v_out, v_in);

    return jsonb_build_object('conversion_id', v_conv, 'from_amount', p_amount,
                              'to_amount', v_to_amt, 'rate', v_rate.rate);
end $$;
grant execute on function public.convert_coins(wallet_type,numeric) to authenticated;

-- 2.2 user requests a cash-out. Cash is held (debited) immediately; staff pays or rejects.
create or replace function public.request_withdrawal(
    p_amount numeric,
    p_payment_account_id bigint
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    v_user   uuid := auth.uid();
    v_min    numeric;
    v_feepct numeric;
    v_fee    numeric(18,4);
    v_net    numeric(18,4);
    v_method withdrawal_method;
    v_txn    bigint;
    v_id     bigint;
begin
    if v_user is null then raise exception 'not authenticated'; end if;
    perform public._assert_not_frozen(v_user);

    select method into v_method from public.payment_accounts
    where id = p_payment_account_id and user_id = v_user;
    if v_method is null then raise exception 'payment account not found'; end if;

    select coalesce((value)::numeric, 200) into v_min    from public.admin_settings where key = 'min_withdrawal_pkr';
    select coalesce((value)::numeric, 0)   into v_feepct from public.admin_settings where key = 'withdrawal_fee_pct';
    if p_amount < v_min then raise exception 'minimum withdrawal is %', v_min; end if;

    v_fee := round(p_amount * v_feepct / 100.0, 4);
    v_net := p_amount - v_fee;

    v_txn := public._apply_wallet_txn(v_user, 'cash', 'debit', 'withdrawal', p_amount,
             'PKR', 'withdrawal', null, 'withdrawal request hold', v_user);

    insert into public.withdrawals (user_id, payment_account_id, method, amount, fee, net_amount, status, wallet_txn_id)
    values (v_user, p_payment_account_id, v_method, p_amount, v_fee, v_net, 'requested', v_txn)
    returning id into v_id;

    update public.wallet_transactions set reference_id = v_id::text where id = v_txn;
    return v_id;
end $$;
grant execute on function public.request_withdrawal(numeric,bigint) to authenticated;

-- 2.3 staff reviews a withdrawal: action = 'pay' | 'reject'
create or replace function public.review_withdrawal(
    p_withdrawal_id bigint, p_action text, p_note text default null, p_gateway_ref text default null
) returns void
language plpgsql
security definer
set search_path = public
as $$
declare w public.withdrawals%rowtype;
begin
    if not public.has_permission('withdrawal.review') then
        raise exception 'permission denied: withdrawal.review';
    end if;

    select * into w from public.withdrawals where id = p_withdrawal_id for update;
    if not found then raise exception 'withdrawal not found'; end if;
    if w.status not in ('requested','under_review','approved','processing') then
        raise exception 'withdrawal already finalised (%).', w.status;
    end if;

    if p_action = 'pay' then
        update public.withdrawals
        set status = 'paid', reviewed_by = auth.uid(), reviewed_at = now(),
            note = p_note, gateway_ref = p_gateway_ref
        where id = p_withdrawal_id;
        perform public._audit('withdrawal.pay', 'withdrawals', p_withdrawal_id::text, to_jsonb(w), null);

    elsif p_action = 'reject' then
        -- refund the held cash
        perform public._apply_wallet_txn(w.user_id, 'cash', 'credit', 'withdrawal_refund', w.amount,
                'PKR', 'withdrawal', p_withdrawal_id::text, 'withdrawal rejected - refund', auth.uid(), true);
        update public.withdrawals
        set status = 'rejected', reviewed_by = auth.uid(), reviewed_at = now(), note = p_note
        where id = p_withdrawal_id;
        perform public._audit('withdrawal.reject', 'withdrawals', p_withdrawal_id::text, to_jsonb(w), null);
    else
        raise exception 'action must be pay or reject';
    end if;
end $$;
grant execute on function public.review_withdrawal(bigint,text,text,text) to authenticated;

-- -----------------------------------------------------------------------------
-- 3. ESCROW P2P  (no direct user->user transfer anywhere in the system)
-- -----------------------------------------------------------------------------

-- 3.1 seller creates a listing and their coins are LOCKED into escrow immediately
create or replace function public.create_listing(
    p_wallet_type wallet_type, p_coin_amount numeric, p_price_cash numeric, p_expires_at timestamptz default null
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare v_user uuid := auth.uid(); v_id bigint;
begin
    if v_user is null then raise exception 'not authenticated'; end if;
    perform public._assert_not_frozen(v_user);
    if p_coin_amount <= 0 or p_price_cash <= 0 then raise exception 'amounts must be > 0'; end if;

    insert into public.marketplace_listings (seller_id, wallet_type, coin_amount, price_cash, expires_at)
    values (v_user, p_wallet_type, p_coin_amount, p_price_cash, p_expires_at)
    returning id into v_id;
    return v_id;
end $$;
grant execute on function public.create_listing(wallet_type,numeric,numeric,timestamptz) to authenticated;

-- 3.2 buyer accepts a listing -> both sides are locked into an escrow_trade
--     seller's coins locked + buyer's cash locked. status = 'funded'.
create or replace function public.open_escrow_trade(p_listing_id bigint)
returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare
    v_buyer  uuid := auth.uid();
    l        public.marketplace_listings%rowtype;
    v_seller_hold bigint;
    v_buyer_hold  bigint;
    v_trade  bigint;
begin
    if v_buyer is null then raise exception 'not authenticated'; end if;
    perform public._assert_not_frozen(v_buyer);

    select * into l from public.marketplace_listings where id = p_listing_id for update;
    if not found or l.status <> 'active' then raise exception 'listing not available'; end if;
    if l.seller_id = v_buyer then raise exception 'cannot buy your own listing'; end if;

    v_seller_hold := public._apply_wallet_txn(l.seller_id, l.wallet_type, 'debit', 'escrow_hold', l.coin_amount,
                     'PKR', 'marketplace_listing', p_listing_id::text, 'coins locked in escrow', v_buyer);
    v_buyer_hold  := public._apply_wallet_txn(v_buyer, 'cash', 'debit', 'escrow_hold', l.price_cash,
                     'PKR', 'marketplace_listing', p_listing_id::text, 'cash locked in escrow', v_buyer);

    insert into public.escrow_trades (
        listing_id, buyer_id, seller_id, wallet_type, coin_amount, price_cash, status,
        buyer_cash_hold_txn_id, seller_coin_hold_txn_id
    ) values (p_listing_id, v_buyer, l.seller_id, l.wallet_type, l.coin_amount, l.price_cash, 'funded',
        v_buyer_hold, v_seller_hold)
    returning id into v_trade;

    update public.marketplace_listings set status = 'sold' where id = p_listing_id;
    insert into public.escrow_events (trade_id, event, actor_id) values (v_trade, 'funded', v_buyer);
    return v_trade;
end $$;
grant execute on function public.open_escrow_trade(bigint) to authenticated;

-- 3.3 staff (or automated rule) finalises a trade: action = 'release' | 'refund'
create or replace function public.settle_escrow_trade(
    p_trade_id bigint, p_action text, p_note text default null
) returns void
language plpgsql
security definer
set search_path = public
as $$
declare t public.escrow_trades%rowtype; v_fee_pct numeric; v_fee numeric(18,4); v_pay numeric(18,4);
begin
    if not public.has_permission('escrow.oversee') then
        raise exception 'permission denied: escrow.oversee';
    end if;

    select * into t from public.escrow_trades where id = p_trade_id for update;
    if not found then raise exception 'trade not found'; end if;
    if t.status <> 'funded' then raise exception 'trade not in funded state'; end if;

    if p_action = 'release' then
        select coalesce((value)::numeric, 0) into v_fee_pct from public.admin_settings where key = 'escrow_fee_pct';
        v_fee := round(t.price_cash * v_fee_pct / 100.0, 4);
        v_pay := t.price_cash - v_fee;

        -- coins -> buyer, cash (minus fee) -> seller
        update public.escrow_trades set
            release_coin_txn_id = public._apply_wallet_txn(t.buyer_id, t.wallet_type, 'credit', 'escrow_release', t.coin_amount,
                'PKR', 'escrow_trade', p_trade_id::text, 'coins released to buyer', auth.uid(), true),
            release_cash_txn_id = public._apply_wallet_txn(t.seller_id, 'cash', 'credit', 'escrow_release', v_pay,
                'PKR', 'escrow_trade', p_trade_id::text, 'cash released to seller', auth.uid(), true),
            status = 'released', manager_id = auth.uid(), notes = p_note, updated_at = now()
        where id = p_trade_id;
        insert into public.escrow_events (trade_id, event, actor_id, detail)
        values (p_trade_id, 'released', auth.uid(), jsonb_build_object('fee', v_fee));

    elsif p_action = 'refund' then
        -- give everything back to both parties
        perform public._apply_wallet_txn(t.seller_id, t.wallet_type, 'credit', 'escrow_refund', t.coin_amount,
            'PKR', 'escrow_trade', p_trade_id::text, 'coins refunded to seller', auth.uid(), true);
        perform public._apply_wallet_txn(t.buyer_id, 'cash', 'credit', 'escrow_refund', t.price_cash,
            'PKR', 'escrow_trade', p_trade_id::text, 'cash refunded to buyer', auth.uid(), true);
        update public.escrow_trades set status = 'refunded', manager_id = auth.uid(), notes = p_note, updated_at = now()
        where id = p_trade_id;
        insert into public.escrow_events (trade_id, event, actor_id) values (p_trade_id, 'refunded', auth.uid());
    else
        raise exception 'action must be release or refund';
    end if;
end $$;
grant execute on function public.settle_escrow_trade(bigint,text,text) to authenticated;

-- -----------------------------------------------------------------------------
-- 4. SECURITY CONTROLS  (managers)
-- -----------------------------------------------------------------------------

-- 4.1 freeze / unfreeze an account or just its coins
create or replace function public.set_account_freeze(
    p_user_id uuid, p_freeze_type freeze_type, p_active boolean, p_reason text default null
) returns void
language plpgsql
security definer
set search_path = public
as $$
begin
    if not public.has_permission('user.freeze') then
        raise exception 'permission denied: user.freeze';
    end if;

    if p_active then
        insert into public.account_freezes (user_id, freeze_type, is_active, reason, frozen_by)
        values (p_user_id, p_freeze_type, true, p_reason, auth.uid());
    else
        update public.account_freezes
        set is_active = false, unfrozen_by = auth.uid(), unfrozen_at = now()
        where user_id = p_user_id and freeze_type = p_freeze_type and is_active;
    end if;

    if p_freeze_type = 'account' then
        update public.profiles set status = case when p_active then 'frozen' else 'active' end
        where id = p_user_id;
    end if;

    update public.wallets set is_frozen = p_active where user_id = p_user_id;
    perform public._audit(case when p_active then 'user.freeze' else 'user.unfreeze' end,
        'profiles', p_user_id::text, null, jsonb_build_object('type', p_freeze_type));
end $$;
grant execute on function public.set_account_freeze(uuid,freeze_type,boolean,text) to authenticated;

-- 4.2 staff add/remove matrix: grant or revoke ONE permission for ONE user
create or replace function public.set_staff_permission(
    p_user_id uuid, p_permission_code text, p_allowed boolean, p_reason text default null
) returns void
language plpgsql
security definer
set search_path = public
as $$
declare v_perm int;
begin
    if not public.has_permission('staff.permissions.manage') then
        raise exception 'permission denied: staff.permissions.manage';
    end if;
    select id into v_perm from public.permissions where code = p_permission_code;
    if v_perm is null then raise exception 'unknown permission %', p_permission_code; end if;

    insert into public.staff_permission_overrides (user_id, permission_id, allowed, reason, set_by)
    values (p_user_id, v_perm, p_allowed, p_reason, auth.uid())
    on conflict (user_id, permission_id)
    do update set allowed = excluded.allowed, reason = excluded.reason,
                  set_by = excluded.set_by, set_at = now();

    perform public._audit('staff.permission.set', 'profiles', p_user_id::text,
        null, jsonb_build_object('permission', p_permission_code, 'allowed', p_allowed));
end $$;
grant execute on function public.set_staff_permission(uuid,text,boolean,text) to authenticated;

-- =============================================================================
--  END OF PHASE 2
--  Phase 3 will add: daily payout function + pg_cron schedule,
--                    global analytics views for the CEO dashboard.
-- =============================================================================
