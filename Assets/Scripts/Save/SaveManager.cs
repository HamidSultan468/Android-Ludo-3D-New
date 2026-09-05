using UnityEngine;

namespace LudoGame.Save
{
    /// <summary>Key names used with PlayerPrefs, kept in one place to avoid typos.</summary>
    public static class SaveKeys
    {
        public const string SfxVolume = "sfx_volume";
        public const string MusicVolume = "music_volume";
        public const string SfxMuted = "sfx_muted";
        public const string MusicMuted = "music_muted";
        public const string GamesWon = "games_won";
    }

    /// <summary>
    /// Simple save/load wrapper around Unity's PlayerPrefs, for settings
    /// (volume, mute) and light progress (games won). PlayerPrefs already
    /// saves to disk on Android automatically, so no extra setup is needed.
    ///
    /// Note: this does NOT save a mid-match board state (token positions,
    /// whose turn it is). That is a bigger feature - ask if you want it added
    /// once the rest of the game is working end to end.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on an empty GameObject named "SaveManager".
    /// 2. Other scripts (e.g. SettingsUI) read/write through
    ///    SaveManager.Instance - no manual wiring needed beyond that.
    /// </summary>
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        public float GetSfxVolume(float defaultValue = 1f) => PlayerPrefs.GetFloat(SaveKeys.SfxVolume, defaultValue);

        public void SetSfxVolume(float value)
        {
            PlayerPrefs.SetFloat(SaveKeys.SfxVolume, value);
            PlayerPrefs.Save();
        }

        public float GetMusicVolume(float defaultValue = 1f) => PlayerPrefs.GetFloat(SaveKeys.MusicVolume, defaultValue);

        public void SetMusicVolume(float value)
        {
            PlayerPrefs.SetFloat(SaveKeys.MusicVolume, value);
            PlayerPrefs.Save();
        }

        public bool IsSfxMuted() => PlayerPrefs.GetInt(SaveKeys.SfxMuted, 0) == 1;

        public void SetSfxMuted(bool muted)
        {
            PlayerPrefs.SetInt(SaveKeys.SfxMuted, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public bool IsMusicMuted() => PlayerPrefs.GetInt(SaveKeys.MusicMuted, 0) == 1;

        public void SetMusicMuted(bool muted)
        {
            PlayerPrefs.SetInt(SaveKeys.MusicMuted, muted ? 1 : 0);
            PlayerPrefs.Save();
        }

        public int GetGamesWon() => PlayerPrefs.GetInt(SaveKeys.GamesWon, 0);

        public void AddGameWon()
        {
            PlayerPrefs.SetInt(SaveKeys.GamesWon, GetGamesWon() + 1);
            PlayerPrefs.Save();
        }

        /// <summary>Deletes all saved settings/progress. Show a confirmation dialog before calling this from UI.</summary>
        public void ResetAllData()
        {
            PlayerPrefs.DeleteAll();
        }
    }
}
