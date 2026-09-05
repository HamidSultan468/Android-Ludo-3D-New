using UnityEngine;

namespace TangentLudoEmpire.Polish
{
    /// <summary>
    /// Task C.4. PlayerPrefs wrapper. SPEC-COMPLIANT EXCEPTION to the project's standing "no PlayerPrefs
    /// for storage of financial/progression data" rule: everything here (SoundVolume, MusicVolume,
    /// NotificationsOn) is a pure, non-exploitable client-side UI preference - none of it is money,
    /// currency, or gameplay progression, so there is nothing here for a save-file edit to exploit.
    /// Real money continues to live exclusively in <see cref="TangentLudoEmpire.Wallet.MoneyWallet"/>'s
    /// AES-256 encrypted store.
    /// </summary>
    public static class SettingsManager
    {
        private const string KeySoundVolume = "tle_sound_volume";
        private const string KeyMusicVolume = "tle_music_volume";
        private const string KeyNotificationsOn = "tle_notifications_on";

        public static float SoundVolume
        {
            get => PlayerPrefs.GetFloat(KeySoundVolume, 1f);
            set { PlayerPrefs.SetFloat(KeySoundVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); PushToSoundManager(); }
        }

        public static float MusicVolume
        {
            get => PlayerPrefs.GetFloat(KeyMusicVolume, 1f);
            set { PlayerPrefs.SetFloat(KeyMusicVolume, Mathf.Clamp01(value)); PlayerPrefs.Save(); PushToSoundManager(); }
        }

        public static bool NotificationsOn
        {
            get => PlayerPrefs.GetInt(KeyNotificationsOn, 1) != 0;
            set { PlayerPrefs.SetInt(KeyNotificationsOn, value ? 1 : 0); PlayerPrefs.Save(); }
        }

        /// <summary>Pushes the saved volumes into <see cref="TangentLudoEmpire.Core.SoundManager"/>. Call
        /// once at startup (GameServices.EnsureManagers, after SoundManager exists) and again whenever a
        /// Settings volume slider changes (the property setters above already do the latter).</summary>
        public static void PushToSoundManager()
        {
            var sm = TangentLudoEmpire.Core.SoundManager.Instance;
            sm?.ApplySavedVolumes(SoundVolume, MusicVolume);
        }
    }
}
