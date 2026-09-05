using System;
using UnityEngine;
using UnityEngine.Events;

namespace TangentLudoEmpire.Bridge
{
    /// <summary><c>UnityEvent&lt;decimal&gt;</c> needs a concrete <see cref="Serializable"/> subclass to
    /// get a proper Inspector listener list - the raw generic type doesn't serialise correctly.</summary>
    [Serializable]
    public class DecimalUnityEvent : UnityEvent<decimal> { }

    /// <summary>
    /// Task 3: gives the <c>LudoEmpire.Ludo</c> gameplay layer a way to notify the wallet WITHOUT that
    /// layer ever referencing Tangent code directly - the coupling is one-directional and event-driven,
    /// not a code reference, so <c>LudoBoardLogic.cs</c> / <c>LudoVictoryScreenController.cs</c> (both
    /// off-limits) never need to know this class - or the wallet - exists.
    ///
    /// <b>Self-wires to <see cref="WalletGameBridge"/> in <see cref="Awake"/></b> - the wallet
    /// deduction/credit happens automatically the moment either event fires, in code, not via the
    /// Inspector. That's a deliberate choice, not a shortcut: <see cref="WalletGameBridge"/> is a static
    /// class (Task 1), and Unity's UnityEvent persistent-listener picker can only target INSTANCE methods
    /// on a dragged <c>Object</c> reference - a static class has no instance to drag in, so it can never
    /// appear in that dropdown. The public <see cref="OnEntryFeeRequired"/>/<see cref="OnWinAmount"/>
    /// fields stay open for the Ludo team to wire their OWN extra listeners (a UI popup, a sound, an
    /// analytics event) - those are additive, the wallet connection itself doesn't depend on them.
    ///
    /// MANUAL STEPS STILL NEEDED (deferred - the two files below are LudoEmpire.Ludo, this session
    /// cannot touch them):
    ///   1. Drag this script onto the LudoManager empty object (or another persistent object that lives
    ///      for the whole match, e.g. in SampleScene).
    ///   2. In LudoBoardLogic.StartGame(): call
    ///      FindAnyObjectByType&lt;LudoWalletHooks&gt;()?.OnEntryFeeRequired.Invoke(entryFee);
    ///   3. In LudoVictoryScreenController.ShowWin(): call
    ///      FindAnyObjectByType&lt;LudoWalletHooks&gt;()?.OnWinAmount.Invoke(winAmount);
    ///   4. (optional) In the Inspector: add extra listeners to OnEntryFeeRequired for your own UI/SFX -
    ///      WalletGameBridge.TryEnterGame is already wired in code (see class remarks above).
    ///   5. (optional) Same for OnWinAmount - WalletGameBridge.ReportWin is already wired in code.
    /// </summary>
    [DisallowMultipleComponent]
    public class LudoWalletHooks : MonoBehaviour
    {
        [Tooltip("Invoke(entryFee) from LudoBoardLogic.StartGame() - see class remarks, step 2.")]
        public DecimalUnityEvent OnEntryFeeRequired = new DecimalUnityEvent();

        [Tooltip("Invoke(winAmount) from LudoVictoryScreenController.ShowWin() - see class remarks, step 3.")]
        public DecimalUnityEvent OnWinAmount = new DecimalUnityEvent();

        private void Awake()
        {
            OnEntryFeeRequired.AddListener(fee => WalletGameBridge.TryEnterGame(fee));
            OnWinAmount.AddListener(amount => WalletGameBridge.ReportWin(amount));
        }
    }
}
