using UnityEngine;
using LudoEmpire.Ludo; // FlowManager, GameConfig - called, never modified.

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Parameterless button entry points. A UnityEvent persistent listener (a wired Button.onClick)
    /// cannot carry a <see cref="GameConfig"/> argument, so the MainMenu buttons call one of these
    /// instead of <c>FlowManager.StartMatch(config)</c> directly. Put this on the MainMenu Canvas
    /// (the <c>MainMenuWirer</c> editor tool does it for you).
    /// </summary>
    [DisallowMultipleComponent]
    public class MenuMatchStarter : MonoBehaviour
    {
        /// <summary>Start today's default match: Red human vs 3 Medium AI, Classic rules.</summary>
        public void StartPlayVsAI()
        {
            Debug.Log("[MenuMatchStarter] StartPlayVsAI");
            FlowManager.Instance.StartMatch(GameConfig.Default());
        }

        /// <summary>Start a local hot-seat match: all four colours human.</summary>
        public void StartPassAndPlay()
        {
            Debug.Log("[MenuMatchStarter] StartPassAndPlay");
            var cfg = GameConfig.Default();
            for (int i = 0; i < cfg.slots.Length; i++)
                cfg.slots[i].controller = LudoSlotController.Human;
            FlowManager.Instance.StartMatch(cfg.Validated());
        }
    }
}
