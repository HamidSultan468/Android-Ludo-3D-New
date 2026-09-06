-- =============================================================================
--  TANGENT LUDO EMPIRE  —  PHASE 3  DAILY PAYOUT CRON + ANALYTICS
--  Run AFTER phase1_schema.sql and phase2_functions.sql.
-- =============================================================================

-- -----------------------------------------------------------------------------
-- 1. DAILY PAYOUT JOB
--    Sums each user's "earned" ad impressions for one day, credits the CASH
--    wallet (via the Phase 2 ledger helper), records payout_runs + daily_earnings,
--    and marks those impressions as counted. Safe to re-run: a completed day
--    is skipped; a failed/partial day is resumed (only uncounted rows are paid).
-- -----------------------------------------------------------------------------
create or replace function public.run_daily_payout(
    p_date date default ((now() at time zone 'utc')::date - 1)
) returns jsonb
language plpgsql
security definer
set search_path = public
as $$
declare
    v_run       public.payout_runs%rowtype;
    v_rate      numeric(18,6);
    v_currency  text;
    v_cap       int;
    r           record;
    v_ads_user  int;
    v_gross     numeric(18,4);
    v_txn       bigint;
    v_users     int    := 0;
    v_ads       bigint := 0;
    v_amount    numeric(18,4) := 0;
begin
    -- manual runs need the permission; the cron (auth.uid() is null) does not
    if auth.uid() is not null and not public.has_permission('payout.run') then
        raise exception 'permission denied: payout.run';
    end if;

    -- rate that was in effect at the END of that day
    select rate_per_ad, currency into v_rate, v_currency
    from public.ad_rate_config
    where effective_from <= (p_date + 1)::timestamptz
    order by effective_from desc, id desc
    limit 1;
    v_rate     := coalesce(v_rate, 0);
    v_currency := coalesce(v_currency, 'PKR');

    select coalesce((value)::int, 80) into v_cap
    from public.admin_settings where key = 'max_ads_per_day';

    -- get / create the run row
    select * into v_run from public.payout_runs where run_date = p_date;
    if found and v_run.status = 'completed' then
        return jsonb_build_object('status', 'already_completed', 'run_date', p_date,
                                  'users', v_run.total_users, 'amount', v_run.total_amount);
    elsif found then
        update public.payout_runs
        set status = 'processing', started_at = now(), rate_used = v_rate,
            currency = v_currency, triggered_by = auth.uid()
        where id = v_run.id
        returning * into v_run;
    else
        insert into public.payout_runs (run_date, status, rate_used, currency, started_at, triggered_by)
        values (p_date, 'processing', v_rate, v_currency, now(), auth.uid())
        returning * into v_run;
    end if;

    for r in
        select user_id, count(*)::int as ads
        from public.ad_impressions
        where earned_status = 'earned'
          and is_counted_for_payout = false
          and watched_at >= p_date::timestamptz
          and watched_at <  (p_date + 1)::timestamptz
        group by user_id
    loop
        v_ads_user := least(r.ads, v_cap);
        v_gross    := round(v_ads_user * v_rate, 4);

        if v_gross > 0 then
            v_txn := public._apply_wallet_txn(
                r.user_id, 'cash', 'credit', 'daily_payout', v_gross,
                v_currency, 'payout_run', v_run.id::text,
                format('daily ad payout for %s', p_date), null, true);
        else
            v_txn := null;
        end if;

        insert into public.daily_earnings (
            payout_run_id, user_id, run_date, ads_counted, rate_used, currency, gross_amount, wallet_txn_id
        ) values (
            v_run.id, r.user_id, p_date, v_ads_user, v_rate, v_currency, v_gross, v_txn
        )
        on conflict (user_id, run_date) do update
            set ads_counted   = excluded.ads_counted,
                gross_amount  = excluded.gross_amount,
                payout_run_id = excluded.payout_run_id,
                wallet_txn_id = coalesce(public.daily_earnings.wallet_txn_id, excluded.wallet_txn_id);

        update public.ad_impressions
        set is_counted_for_payout = true, payout_run_id = v_run.id
        where user_id = r.user_id
          and earned_status = 'earned'
          and is_counted_for_payout = false
          and watched_at >= p_date::timestamptz
          and watched_at <  (p_date + 1)::timestamptz;

        v_users  := v_users + 1;
        v_ads    := v_ads + v_ads_user;
        v_amount := v_amount + v_gross;
    end loop;

    update public.payout_runs
    set status = 'completed', finished_at = now(),
        total_users = v_users, total_ads = v_ads, total_amount = v_amount
    where id = v_run.id;

    perform public._audit('payout.run', 'payout_runs', v_run.id::text, null,
        jsonb_build_object('date', p_date, 'users', v_users, 'ads', v_ads, 'amount', v_amount, 'rate', v_rate));

    return jsonb_build_object('status', 'completed', 'run_date', p_date,
        'users', v_users, 'ads', v_ads, 'amount', v_amount, 'rate', v_rate);
end $$;
revoke all on function public.run_daily_payout(date) from public, anon;
grant execute on function public.run_daily_payout(date) to authenticated;

-- -----------------------------------------------------------------------------
-- 2. SCHEDULE IT  (pg_cron)
--    Supabase: Database -> Extensions -> enable "pg_cron", then run this once.
--    '0 19 * * *' = every day 19:00 UTC (matches admin_settings.daily_payout_time_utc).
-- -----------------------------------------------------------------------------
-- create extension if not exists pg_cron;
-- select cron.schedule('tle-daily-payout', '0 19 * * *', $$ select public.run_daily_payout(); $$);
--
-- change time later:
-- select cron.alter_job((select jobid from cron.job where jobname='tle-daily-payout'), schedule => '30 18 * * *');
-- stop:
-- select cron.unschedule('tle-daily-payout');

-- -----------------------------------------------------------------------------
-- 3. CEO / MANAGER ANALYTICS  (all gated by permission 'analytics.global.read')
-- -----------------------------------------------------------------------------

-- 3.1 one call for the top of the CEO dashboard
create or replace function public.get_dashboard_summary()
returns jsonb
language plpgsql
stable
security definer
set search_path = public
as $$
begin
    if not public.has_permission('analytics.global.read') then
        raise exception 'permission denied: analytics.global.read';
    end if;

    return jsonb_build_object(
        'total_users',   (select count(*) from public.profiles where status <> 'deleted'),
        'pro_users',     (select count(*) from public.profiles where is_pro),
        'frozen_users',  (select count(*) from public.profiles where status = 'frozen'),
        'dau_today',     (select count(distinct user_id) from public.ad_impressions
                            where watched_at >= date_trunc('day', now())),
        'ads_today',     (select count(*) from public.ad_impressions
                            where earned_status = 'earned' and watched_at >= date_trunc('day', now())),
        'current_ad_rate', (select rate_per_ad from public.get_current_ad_rate()),
        'pending_withdrawals', (select jsonb_build_object('count', count(*), 'amount', coalesce(sum(amount), 0))
                            from public.withdrawals
                            where status in ('requested','under_review','approved','processing')),
        'wallet_totals', (select coalesce(jsonb_object_agg(wallet_type, bal), '{}'::jsonb)
                            from (select wallet_type, sum(balance) bal from public.wallets group by wallet_type) s),
        'escrow_open',      (select count(*) from public.escrow_trades where status = 'funded'),
        'escrow_disputed',  (select count(*) from public.escrow_trades where status = 'disputed'),
        'last_payout',      (select jsonb_build_object('date', run_date, 'amount', total_amount, 'users', total_users)
                            from public.payout_runs order by run_date desc limit 1)
    );
end $$;
revoke all on function public.get_dashboard_summary() from public, anon;
grant execute on function public.get_dashboard_summary() to authenticated;

-- 3.2 revenue / cash-flow per day for the global chart
create or replace function public.get_global_revenue(p_from date, p_to date)
returns table (day date, ad_payout numeric, withdrawals_paid numeric, deposits_completed numeric, net_cashflow numeric)
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
    select g::date as day,
        coalesce((select sum(pr.total_amount) from public.payout_runs pr where pr.run_date = g::date), 0)                              as ad_payout,
        coalesce((select sum(w.amount) from public.withdrawals w where w.status = 'paid' and w.reviewed_at::date = g::date), 0)        as withdrawals_paid,
        coalesce((select sum(d.amount) from public.deposits d where d.status = 'completed' and d.completed_at::date = g::date), 0)     as deposits_completed,
        coalesce((select sum(d.amount) from public.deposits d where d.status = 'completed' and d.completed_at::date = g::date), 0)
      - coalesce((select sum(w.amount) from public.withdrawals w where w.status = 'paid' and w.reviewed_at::date = g::date), 0)        as net_cashflow
    from generate_series(p_from, p_to, interval '1 day') g;
end $$;
revoke all on function public.get_global_revenue(date,date) from public, anon;
grant execute on function public.get_global_revenue(date,date) to authenticated;

-- 3.3 top earners over the last N days
create or replace function public.get_top_earners(p_days int default 30, p_limit int default 10)
returns table (user_id uuid, username text, total_earned numeric, total_ads bigint)
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
    select de.user_id, p.username, sum(de.gross_amount)::numeric, sum(de.ads_counted)::bigint
    from public.daily_earnings de
    join public.profiles p on p.id = de.user_id
    where de.run_date >= (now()::date - p_days)
    group by de.user_id, p.username
    order by sum(de.gross_amount) desc
    limit p_limit;
end $$;
revoke all on function public.get_top_earners(int,int) from public, anon;
grant execute on function public.get_top_earners(int,int) to authenticated;

-- -----------------------------------------------------------------------------
-- 4. PLAYER-FACING READS
-- -----------------------------------------------------------------------------

-- 4.1 everything the player's personal dashboard needs, in one call (own data only)
create or replace function public.get_user_dashboard()
returns jsonb
language plpgsql
stable
security definer
set search_path = public
as $$
declare v_user uuid := auth.uid(); v_rate numeric;
begin
    if v_user is null then raise exception 'not authenticated'; end if;
    select rate_per_ad into v_rate from public.get_current_ad_rate();

    return jsonb_build_object(
        'wallets', (select coalesce(jsonb_object_agg(wallet_type,
                        jsonb_build_object('balance', balance, 'frozen', is_frozen)), '{}'::jsonb)
                     from public.wallets where user_id = v_user),
        'ads_today', (select count(*) from public.ad_impressions
                        where user_id = v_user and earned_status = 'earned'
                          and watched_at >= date_trunc('day', now())),
        'est_today_earning', coalesce((select count(*) from public.ad_impressions
                        where user_id = v_user and earned_status = 'earned'
                          and watched_at >= date_trunc('day', now())), 0) * coalesce(v_rate, 0),
        'last_payout', (select jsonb_build_object('date', run_date, 'amount', gross_amount, 'ads', ads_counted)
                        from public.daily_earnings where user_id = v_user
                        order by run_date desc limit 1),
        'pending_withdrawals', (select coalesce(sum(amount), 0) from public.withdrawals
                        where user_id = v_user
                          and status in ('requested','under_review','approved','processing'))
    );
end $$;
grant execute on function public.get_user_dashboard() to authenticated;

-- 4.2 Top-5 rank holders for a game (dashboard scrolling list)
create or replace function public.get_leaderboard(p_game_code text, p_limit int default 5)
returns table (rank int, username text, level int, xp int, wins int)
language plpgsql
stable
security definer
set search_path = public
as $$
begin
    return query
    select row_number() over (order by ps.wins desc, ps.xp desc)::int,
           p.username, ps.level, ps.xp, ps.wins
    from public.player_stats ps
    join public.profiles p on p.id = ps.user_id
    join public.games g on g.id = ps.game_id
    where g.code = p_game_code
    order by ps.wins desc, ps.xp desc
    limit p_limit;
end $$;
grant execute on function public.get_leaderboard(text,int) to authenticated;

-- =============================================================================
--  END OF PHASE 3
--  Phase 4 will cover: Admin Panel screen layout + button -> RPC mapping,
--                      and the security / privacy-shield implementation guide.
-- =============================================================================
