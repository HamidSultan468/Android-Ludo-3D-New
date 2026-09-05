namespace LudoEmpire.Ludo
{
    /// <summary>The two match setups the Main Menu offers.</summary>
    public enum LudoGameMode
    {
        /// <summary>One human (Red, by convention) versus 3 AI bots - the board scene's default setup.</summary>
        PlayerVsAI,
        /// <summary>All 4 colors are human, taking turns on the same device (local hotseat play).</summary>
        PassAndPlay
    }

    /// <summary>
    /// Tiny in-memory holder carrying the player's Main Menu mode choice across the scene load into the
    /// board scene. Deliberately not PlayerPrefs-backed - it only needs to survive a single same-session
    /// scene transition (Main Menu -> board), not an app restart, and defaults to the board's normal
    /// Player vs AI setup so the board scene still works correctly if loaded directly (e.g. in the Editor)
    /// without ever going through the Main Menu at all.
    /// </summary>
    public static class LudoGameModeSelection
    {
        public static LudoGameMode SelectedMode { get; private set; } = LudoGameMode.PlayerVsAI;

        public static void Select(LudoGameMode mode)
        {
            SelectedMode = mode;
        }
    }
}
