-- =============================================================================
--  TANGENT LUDO EMPIRE  —  PHASE 4  PRIVACY SHIELD + RECONCILIATION
--  Run AFTER phase1..phase3.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. PRIVACY SHIELD for payment account numbers
--    Rule: the app NEVER selects payment_accounts directly for display.
--    It calls get_payment_accounts(); numbers come back masked while the
--    shield is ON. Full number requires an explicit reveal (audited).
-- -----------------------------------------------------------------------------

create or replace function public._mask_account(p_num text)
returns text
language sql immutable
as $$
    select case
        when p_num is null or length(p_num) <= 4 then '****'
        else repeat('*', greatest(length(p_num) - 4, 0)) || right(p_num, 4)
    end;
$$;

-- 1.1 list the caller's own payment accounts (masked unless their shield is OFF)
create or replace function public.get_payment_accounts()
returns table (id bigint, method withdrawal_method, account_title text,
               account_number text, is_default boolean, is_verified boolean, masked boolean)
language plpgsql
stable
security definer
set search_path = public
as $$
declare v_user uuid := auth.uid(); v_shield boolean;
begin
    if v_user is null then raise exception 'not authenticated'; end if;

    select coalesce(payment_shield_on, true) into v_shield
    from public.privacy_settings where user_id = v_user;
    v_shield := coalesce(v_shield, true);

    return query
    select pa.id, pa.method, pa.account_title,
           case when v_shield or pa.shield_on
                then public._mask_account(pa.account_number)
                else pa.account_number end,
           pa.is_default, pa.is_verified,
           (v_shield or pa.shield_on) as masked
    from public.payment_accounts pa
    where pa.user_id = v_user
    order by pa.is_default desc, pa.id;
end $$;
grant execute on function public.get_payment_accounts() to authenticated;

-- 1.2 reveal ONE full number on demand. Every reveal is written to audit_logs.
--     NOTE: for production, require a fresh step-up (re-enter password / OTP) in
--     the client and pass proof; here we at least log who revealed what & when.
create or replace function public.reveal_payment_account(p_id bigint)
returns text
language plpgsql
security definer
set search_path = public
as $$
declare v_user uuid := auth.uid(); v_num text;
begin
    if v_user is null then raise exception 'not authenticated'; end if;

    select account_number into v_num
    from public.payment_accounts
    where id = p_id and user_id = v_user;
    if v_num is null then raise exception 'account not found'; end if;

    perform public._audit('payment_account.reveal', 'payment_accounts', p_id::text, null, null);
    return v_num;
end $$;
grant execute on function public.reveal_payment_account(bigint) to authenticated;

-- 1.3 toggle the shield
create or replace function public.set_privacy_shield(p_payment_shield_on boolean,
                                                    p_hide_profile boolean default null,
                                                    p_hide_earnings boolean default null)
returns void
language plpgsql
security definer
set search_path = public
as $$
declare v_user uuid := auth.uid();
begin
    if v_user is null then raise exception 'not authenticated'; end if;
    insert into public.privacy_settings (user_id, payment_shield_on, hide_profile, hide_earnings)
    values (v_user, p_payment_shield_on, coalesce(p_hide_profile, false), coalesce(p_hide_earnings, false))
    on conflict (user_id) do update
        set payment_shield_on = excluded.payment_shield_on,
            hide_profile  = coalesce(p_hide_profile,  public.privacy_settings.hide_profile),
            hide_earnings = coalesce(p_hide_earnings, public.privacy_settings.hide_earnings),
            updated_at = now();
end $$;
grant execute on function public.set_privacy_shield(boolean,boolean,boolean) to authenticated;

-- -----------------------------------------------------------------------------
-- 2. MONEY RECONCILIATION  (CEO tool: ledger must always equal wallet balance)
-- -----------------------------------------------------------------------------
create or replace function public.get_wallet_reconciliation()
returns table (wallet_id bigint, user_id uuid, wallet_type wallet_type,
               stored_balance numeric, ledger_balance numeric, diff numeric)
language plpgsql
stable
security definer
set search_path = public
as $$
begin
    if not public.has_permission('analytics.global.read') then
        raise exception 'permission denied: analytics.global.read';
    end if;

    return query
    select w.id, w.user_id, w.wallet_type, w.balance,
           coalesce(sum(case when t.direction = 'credit' then t.amount
                             when t.direction = 'debit'  then -t.amount else 0 end), 0) as ledger_balance,
           w.balance - coalesce(sum(case when t.direction = 'credit' then t.amount
                             when t.direction = 'debit'  then -t.amount else 0 end), 0) as diff
    from public.wallets w
    left join public.wallet_transactions t on t.wallet_id = w.id
    group by w.id, w.user_id, w.wallet_type, w.balance
    having w.balance <> coalesce(sum(case when t.direction = 'credit' then t.amount
                             when t.direction = 'debit'  then -t.amount else 0 end), 0);
end $$;
revoke all on function public.get_wallet_reconciliation() from public, anon;
grant execute on function public.get_wallet_reconciliation() to authenticated;

-- -----------------------------------------------------------------------------
-- 3. MANAGER: set a coin conversion rate (fills the Phase 2 gap)
-- -----------------------------------------------------------------------------
create or replace function public.set_conversion_rate(
    p_from wallet_type, p_to wallet_type, p_rate numeric,
    p_min numeric default 0, p_max numeric default null, p_note text default null
) returns bigint
language plpgsql
security definer
set search_path = public
as $$
declare v_id bigint;
begin
    if not public.has_permission('conversion_rate.update') then
        raise exception 'permission denied: conversion_rate.update';
    end if;
    if p_from = p_to then raise exception 'from and to must differ'; end if;
    if p_rate <= 0 then raise exception 'rate must be > 0'; end if;

    update public.conversion_rates set is_active = false
    where from_wallet = p_from and to_wallet = p_to and is_active;

    insert into public.conversion_rates (from_wallet, to_wallet, rate, min_amount, max_amount, is_active, set_by, note)
    values (p_from, p_to, p_rate, p_min, p_max, true, auth.uid(), p_note)
    returning id into v_id;

    perform public._audit('conversion_rate.update', 'conversion_rates', v_id::text, null,
        jsonb_build_object('from', p_from, 'to', p_to, 'rate', p_rate));
    return v_id;
end $$;
grant execute on function public.set_conversion_rate(wallet_type,wallet_type,numeric,numeric,numeric,text) to authenticated;

-- =============================================================================
--  END OF PHASE 4 SQL
-- =============================================================================
