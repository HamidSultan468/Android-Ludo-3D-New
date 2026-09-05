using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Bridge;

namespace TangentLudoEmpire.Security
{
    /// <summary>
    /// Task A.1. SPEC CORRECTION: the task named this class "AntiCheatManager", but
    /// <see cref="TangentLudoEmpire.Services.AntiCheatManager"/> already exists (Phase 2 - payment-
    /// callback validation, wallet-drift reconciliation, ad-cap speed-hack). That one is about PAYMENTS;
    /// this one is about GAMEPLAY (dice rolls, moves, per-move timing) - genuinely different concerns, so
    /// renamed rather than merged or silently shadowed (same resolution as MoneyWallet vs the existing
    /// coin WalletManager in Phase 3).
    ///
    /// Rule 1 (server-authoritative rolls): <see cref="RequestRoll"/> is the only legitimate way to get a
    /// dice value - it generates the roll ITSELF (never trusts a client-supplied value), simulating the
    /// server round-trip a real backend would own. TODO(Firebase Cloud Function / dedicated game server):
    /// swap the body of RequestRoll for a real network call; callers already treat it as async
    /// (Action&lt;int&gt; callback), so nothing downstream needs to change.
    ///
    /// Rule 2 (move validation) + Rule 3 (speed hack): see <see cref="ValidateMove"/>.
    ///
    /// Wiring note: this session cannot modify LudoEmpire.Ludo, so nothing here is wired to the real
    /// board yet - a human adds a small hook component (same pattern as LudoWalletHooks/
    /// LudoMultiplayerHooks) that calls RequestRoll/ValidateMove from LudoDiceRoller/LudoBoardLogic's
    /// existing call sites, and forwards their results here instead of trusting the client's own roll/move.
    /// </summary>
    [DisallowMultipleComponent]
    public class GameplayAntiCheatManager : MonoBehaviour
    {
        public static GameplayAntiCheatManager Instance { get; private set; }

        /// <summary>playerId, reason.</summary>
        public static event Action<string, string> OnCheatDetected;

        private const string LogFileName = "tle_anticheat_log.txt";
        private readonly Dictionary<string, float> _lastMoveTime = new Dictionary<string, float>();

        private SecurityConfig _cfg;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code would otherwise hit InvalidOperationException here and abort the rest of Awake()
            EnsureConfig();
            WalletSecurityBridge.EnsureSubscribed(); // Rule 2/3 violations flow: this -> WalletGameBridge.OnSuspiciousActivity -> WalletSecurityBridge.FreezeAccount
        }

        private void EnsureConfig() { if (_cfg == null) _cfg = SecurityConfig.Load(); }

        // ---------------------------------------------------------------- Rule 1: server-authoritative roll ----

        /// <summary>Client sends a RollRequest (this call); the "server" (mock - see class remarks)
        /// generates the value and returns it. The client NEVER supplies the roll itself.</summary>
        public void RequestRoll(string playerId, Action<int> onResult)
        {
            EnsureConfig();
            int roll = _cfg.EnforceServerRoll
                ? UnityEngine.Random.Range(1, 7)   // authoritative - generated here, not by the caller
                : UnityEngine.Random.Range(1, 7);  // EnforceServerRoll off: same mock generator, flagged as non-enforced in the log
            AuditLog.Log("DICE_ROLL_SERVER", playerId ?? "", roll.ToString());
            onResult?.Invoke(roll);
        }

        // ---------------------------------------------------------------- Rules 2 + 3: move validation ----

        /// <summary>
        /// Rule 2: rejects a move whose board delta doesn't match the dice value it claims (a simple,
        /// board-topology-agnostic sanity check - this session can't call into LudoBoardLogic's own real
        /// legality rules without touching LudoEmpire.Ludo, so this is the generic surface a human wires
        /// deeper later). Rule 3: rejects (and flags) a move that arrives &lt;<see cref="SecurityConfig.MinMoveTime"/>
        /// seconds after that same player's previous one.
        /// On either violation: audits, calls <see cref="WalletGameBridge.ReportSuspiciousActivity"/>,
        /// fires <see cref="OnCheatDetected"/>, and appends a line to <c>tle_anticheat_log.txt</c>.
        /// Returns true only if the move passes both checks.
        /// </summary>
        public bool ValidateMove(string playerId, int fromPosition, int toPosition, int diceValue)
        {
            EnsureConfig();
            if (!_cfg.EnableAntiCheat) return true;

            float now = Time.realtimeSinceStartup;
            if (_lastMoveTime.TryGetValue(playerId, out float last) && now - last < _cfg.MinMoveTime)
            {
                Reject(playerId, "SPEED_HACK", $"moves {now - last:0.000}s apart (min {_cfg.MinMoveTime:0.00}s)");
                _lastMoveTime[playerId] = now;
                return false;
            }
            _lastMoveTime[playerId] = now;

            if (diceValue < 1 || diceValue > 6 || (toPosition - fromPosition) != diceValue)
            {
                Reject(playerId, "ILLEGAL_MOVE", $"{fromPosition}->{toPosition} claims dice {diceValue}");
                return false;
            }

            return true;
        }

        private void Reject(string playerId, string reason, string detail)
        {
            AuditLog.Log("CHEAT_DETECTED", playerId ?? "", $"{reason}: {detail}");
            AppendToLogFile(playerId, reason, detail);
            WalletGameBridge.ReportSuspiciousActivity(playerId, reason);
            OnCheatDetected?.Invoke(playerId, reason);
            Debug.LogWarning($"[GameplayAntiCheatManager] {reason} - player={playerId} ({detail})");
        }

        private static void AppendToLogFile(string playerId, string reason, string detail)
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, LogFileName);
                string line = $"{DateTime.UtcNow:o}\t{playerId}\t{reason}\t{detail}{Environment.NewLine}";
                File.AppendAllText(path, line);
            }
            catch (Exception e)
            {
                Debug.LogError($"[GameplayAntiCheatManager] Failed to write {LogFileName}: {e.Message}");
            }
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build.</summary>
        public static string Editor_LogPath => Path.Combine(Application.persistentDataPath, LogFileName);
        public void Editor_Reset() { _lastMoveTime.Clear(); try { File.Delete(Editor_LogPath); } catch { } }
#endif
    }
}
