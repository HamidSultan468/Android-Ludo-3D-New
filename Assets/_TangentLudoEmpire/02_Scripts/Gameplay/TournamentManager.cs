using System;
using System.Collections.Generic;
using UnityEngine;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Tournament
{
    /// <summary>
    /// Task B.1. Single "current" tournament model (mock backend, single local player) - registrations
    /// accumulate into one <see cref="TournamentData"/> until it fills, then it starts; a new one opens
    /// automatically the next time someone registers after that.
    /// </summary>
    [DisallowMultipleComponent]
    public class TournamentManager : MonoBehaviour
    {
        public static TournamentManager Instance { get; private set; }

        public event Action<TournamentData> OnTournamentStarted;
        public event Action<TournamentData> OnTournamentCompleted;

        /// <summary>Phase 5.2: playerId, fee, tournamentId. Fired once per successful registration (not
        /// on the idempotent "already registered" early-return) - <see cref="TangentLudoEmpire.Analytics.AnalyticsBridge"/>
        /// hooks this to log the "tournament_join" analytics event.</summary>
        public event Action<string, decimal, string> OnPlayerRegistered;

        private TournamentData _current;
        public TournamentData Current => _current;
        public IReadOnlyList<string[]> Bracket { get; private set; } = Array.Empty<string[]>();

        public string LocalPlayerId => BackendService.Instance != null ? BackendService.Instance.UserId : "local";

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
        }

        /// <summary>Literal Task B.1 signature.</summary>
        public bool RegisterTournament(decimal fee) => RegisterTournament(fee, out _);

        /// <summary>MoneyWallet.DeductFunds (spec said "RemoveFunds" - MoneyWallet has no such method;
        /// DeductFunds is the real one, same RBAC/balance/audit checks as everything else) -&gt; Add
        /// Player -&gt; PrizePool += fee. Auto-starts once full.</summary>
        public bool RegisterTournament(decimal fee, out string error)
        {
            error = null;
            var cfg = TournamentConfig.Load();

            if (_current == null || _current.Status != TournamentStatus.Registering)
                _current = TournamentData.New(fee, cfg.DefaultMaxPlayers);
            else if (_current.EntryFee != fee)
            {
                error = "A different tournament is currently registering.";
                return false;
            }

            string playerId = LocalPlayerId;
            if (_current.Players.Contains(playerId)) return true; // already registered - idempotent

            if (MoneyWallet.Instance == null || !MoneyWallet.Instance.DeductFunds(fee, "TOURNAMENT_ENTRY"))
            {
                error = "Low Balance";
                AuditLog.Log("TOURNAMENT_REGISTER_DENIED", _current.Id, error);
                return false;
            }

            _current.Players.Add(playerId);
            _current.PrizePool += fee;
            AuditLog.Log("TOURNAMENT_REGISTER", _current.Id, $"{playerId} fee={fee:0.00} pool={_current.PrizePool:0.00}");
            OnPlayerRegistered?.Invoke(playerId, fee, _current.Id);

            if (_current.IsFull) StartTournament();
            return true;
        }

        /// <summary>Task B.1: when full, create an 8-player bracket (here: 4 simple Round-1 pairs).</summary>
        public void StartTournament()
        {
            if (_current == null || _current.Players.Count == 0) return;
            _current.Status = TournamentStatus.Active;

            var pairs = new List<string[]>();
            for (int i = 0; i + 1 < _current.Players.Count; i += 2)
                pairs.Add(new[] { _current.Players[i], _current.Players[i + 1] });
            if (_current.Players.Count % 2 == 1) pairs.Add(new[] { _current.Players[_current.Players.Count - 1], "(bye)" });
            Bracket = pairs;

            AuditLog.Log("TOURNAMENT_START", _current.Id, $"{_current.Players.Count} players, pool={_current.PrizePool:0.00}");
            OnTournamentStarted?.Invoke(_current);
        }

        /// <summary>Task B.1: payout = PrizePool * WinnerPercent (from TournamentConfig, not hardcoded).
        /// Optional runnerUpId also gets paid PrizePool * RunnerUp - the config reserves that slice, so
        /// it should not go unused. Only the LOCAL player's own wallet is ever credited (mock/single-
        /// local-player - a real backend would settle every player server-side).</summary>
        public void DeclareWinner(string winnerId, string runnerUpId = null)
        {
            if (_current == null) { Debug.LogWarning("[TournamentManager] DeclareWinner with no current tournament."); return; }

            var cfg = TournamentConfig.Load();
            decimal winnerPayout = decimal.Round(_current.PrizePool * (decimal)cfg.WinnerPercent, 2);
            _current.WinnerId = winnerId;
            _current.Status = TournamentStatus.Completed;
            AuditLog.Log("TOURNAMENT_WINNER", _current.Id, $"{winnerId} payout={winnerPayout:0.00}");
            if (winnerId == LocalPlayerId) MoneyWallet.Instance?.AddFunds(winnerPayout, "TOURNAMENT_WIN");

            if (!string.IsNullOrEmpty(runnerUpId) && runnerUpId != "(bye)")
            {
                decimal runnerUpPayout = decimal.Round(_current.PrizePool * (decimal)cfg.RunnerUp, 2);
                _current.RunnerUpId = runnerUpId;
                AuditLog.Log("TOURNAMENT_RUNNERUP", _current.Id, $"{runnerUpId} payout={runnerUpPayout:0.00}");
                if (runnerUpId == LocalPlayerId) MoneyWallet.Instance?.AddFunds(runnerUpPayout, "TOURNAMENT_WIN");
            }

            OnTournamentCompleted?.Invoke(_current);
        }

        /// <summary>Task D integration target for LudoMultiplayerHooks.OnTournamentWin(decimal). The
        /// amount from gameplay is logged for reference only - real money uses DeclareWinner's own
        /// config-driven math, never a UI/gameplay-supplied figure ("never trust client" applies here too).</summary>
        public void NotifyLocalPlayerWon(decimal gameplayReportedAmount)
        {
            AuditLog.Log("TOURNAMENT_LOCAL_WIN_SIGNAL", _current?.Id ?? "", $"gameplay-reported={gameplayReportedAmount:0.00}");
            DeclareWinner(LocalPlayerId);
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build. Adds a fake OTHER player + their
        /// fee to the pool WITHOUT touching any wallet (they aren't the local player) - lets a headless
        /// test fill an 8-player tournament without needing 7 real accounts.</summary>
        public void Editor_AddMockPlayer(string fakePlayerId, decimal fee)
        {
            if (_current == null || _current.Status != TournamentStatus.Registering) return;
            if (_current.Players.Contains(fakePlayerId)) return;
            _current.Players.Add(fakePlayerId);
            _current.PrizePool += fee;
            if (_current.IsFull) StartTournament();
        }

        public void Editor_Reset() { _current = null; Bracket = Array.Empty<string[]>(); }
#endif
    }
}
