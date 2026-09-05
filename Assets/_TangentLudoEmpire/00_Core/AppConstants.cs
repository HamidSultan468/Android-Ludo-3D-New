namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// One place for every string constant that used to be hardcoded around the project
    /// (scene names, save keys). Nothing else in the new code should type these literally.
    /// The values match the current project's Build Settings and PlayerPrefs exactly, so
    /// switching call sites to these constants is a no-op at runtime.
    /// </summary>
    public static class AppConstants
    {
        // ---- Scenes (must match Build Settings names, no path / extension) ----
        public const string SCENE_BOOT          = "Boot";
        public const string SCENE_DASHBOARD     = "Dashboard";
        public const string SCENE_MAINMENU_NEW  = "MainMenu-new";
        public const string SCENE_SAMPLE        = "SampleScene";

        // Minigame scenes (created later; names are here so routing has one source of truth).
        public const string SCENE_KILLA_BANDAR  = "KillaBandarScene";
        public const string SCENE_GULLI_DANDA   = "GulliDandaScene";
        public const string SCENE_EMPIRE        = "EmpireScene";
        public const string SCENE_THIRD_PERSON  = "ThirdPersonGameScene";
        public const string SCENE_PLAYER_BASE   = "PlayerBaseScene";

        // ---- Save keys ----
        /// <summary>New JSON profile (file in persistentDataPath + PlayerPrefs mirror key).</summary>
        public const string SAVE_KEY_PROFILE    = "TLE_Profile_v1";
        public const string PROFILE_FILE_NAME   = "tangent_profile_v1.json";

        /// <summary>One-shot flag: PlayerPrefs -> JSON migration has run.</summary>
        public const string SAVE_KEY_MIGRATED   = "TLE_Migrated_v1";

        // ---- Legacy PlayerPrefs keys the migration reads then clears ----
        public const string LEGACY_KEY_COINS       = "LudoEmpire_Coins";     // CurrencyManager (string long)
        public const string LEGACY_KEY_GAMES_WON   = "games_won";            // old SaveManager (int)
        public const string LEGACY_KEY_SELECTED_THEME = "LudoEmpire_SelectedTheme"; // kept, not deleted (LudoThemeManager still owns it)

        // ---- Misc ----
        public const string DEFAULT_PROFILE_NAME = "Player";
        public const float  BOOT_HOLD_SECONDS    = 1.0f;
    }
}
