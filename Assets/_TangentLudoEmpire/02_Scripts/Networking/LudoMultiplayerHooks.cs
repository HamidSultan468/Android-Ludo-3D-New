using UnityEngine;

namespace TangentLudoEmpire.Bridge
{
    /// <summary>
    /// Task D.1. Same self-wiring pattern as <see cref="LudoWalletHooks"/> (Phase 3.4) - reuses its
    /// <see cref="DecimalUnityEvent"/> subclass rather than redefining an identical one.
    ///
    /// The <c>decimal</c> argument is accepted (as specified) but NOT used as the actual payout - real
    /// money always comes from <see cref="TangentLudoEmpire.Tournament.TournamentManager.DeclareWinner"/>'s
    /// own PrizePool * WinnerPercent math (config-driven, never a UI/gameplay-supplied figure - "never
    /// trust client" applies here exactly like every other credit path in this project). The reported
    /// amount is logged for reference only (TOURNAMENT_LOCAL_WIN_SIGNAL in AuditLog).
    ///
    /// MANUAL STEPS STILL NEEDED (deferred - LudoBoardLogic.cs / LudoVictoryScreenController.cs are
    /// LudoEmpire.Ludo, off-limits to this session):
    ///   1. Drag this script onto the LudoManager empty object (same object LudoWalletHooks is on).
    ///   2. In the Victory Screen's tournament-win path: call
    ///      FindAnyObjectByType&lt;LudoMultiplayerHooks&gt;()?.OnTournamentWin.Invoke(reportedAmount);
    ///   3. (optional) Inspector: add extra listeners to OnTournamentWin for UI/SFX - the tournament
    ///      payout itself already happens via the code-level AddListener in Awake().
    /// </summary>
    [DisallowMultipleComponent]
    public class LudoMultiplayerHooks : MonoBehaviour
    {
        [Tooltip("Invoke(reportedAmount) from the Victory Screen's tournament-win path - see class remarks.")]
        public DecimalUnityEvent OnTournamentWin = new DecimalUnityEvent();

        private void Awake()
        {
            OnTournamentWin.AddListener(amount =>
            {
                if (TangentLudoEmpire.Tournament.TournamentManager.Instance != null)
                    TangentLudoEmpire.Tournament.TournamentManager.Instance.NotifyLocalPlayerWon(amount);
                else
                    Debug.LogWarning("[LudoMultiplayerHooks] OnTournamentWin fired before TournamentManager exists.");
            });
        }
    }
}
