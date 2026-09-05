using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Applies the Main Menu's mode choice (<see cref="LudoGameModeSelection"/>) to the board scene right
    /// as it loads. The scene is always built the same way (1 human + 3 AI bots, see
    /// <see cref="LudoBoardSceneBuilder"/>) - for Pass &amp; Play this simply disables every
    /// <see cref="LudoAIBot"/> so nothing auto-plays, and switches <see cref="LudoRollButtonController"/>/
    /// <see cref="LudoHudBinder"/> into "every color is human" mode so the Roll button enables (and the
    /// HUD reads "You") on whoever's turn it currently is, not just one fixed color. Player vs AI is a
    /// complete no-op - the board's default setup already is that mode. Runs early
    /// (<see cref="DefaultExecutionOrder"/> -50) so this resolves well before any AI bot could ever react
    /// to a turn change; <see cref="LudoBoardSceneBuilder"/> wires this up automatically via
    /// <see cref="Configure"/>.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    public class LudoGameModeController : MonoBehaviour
    {
        [SerializeField] private LudoRollButtonController rollButtonController;
        [SerializeField] private LudoHudBinder hudBinder;
        [SerializeField] private LudoHumanTurnAutoResolver autoResolver;

        public void Configure(LudoRollButtonController targetRollButtonController, LudoHudBinder targetHudBinder, LudoHumanTurnAutoResolver targetAutoResolver)
        {
            rollButtonController = targetRollButtonController;
            hudBinder = targetHudBinder;
            autoResolver = targetAutoResolver;
        }

        private void Awake()
        {
            if (LudoGameModeSelection.SelectedMode != LudoGameMode.PassAndPlay)
            {
                return; // default Player vs AI setup - nothing to change
            }

            foreach (LudoAIBot bot in FindObjectsByType<LudoAIBot>(FindObjectsSortMode.None))
            {
                bot.enabled = false;
            }

            if (rollButtonController != null) rollButtonController.SetAllHumanMode(true);
            if (hudBinder != null) hudBinder.SetAllHumanMode(true);
            if (autoResolver != null) autoResolver.SetAllHumanMode(true);
        }
    }
}
