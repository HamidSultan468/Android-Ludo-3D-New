using LudoGame.Audio;
using LudoGame.Save;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.UI
{
    /// <summary>
    /// Settings panel: sound/music volume sliders and mute toggles. Reads the
    /// saved values on open, and writes through to both SaveManager
    /// (persistence) and AudioManager (so the change is heard immediately).
    ///
    /// Setup (in the Unity Editor):
    /// 1. Build a Settings panel under your Canvas and put this script on it.
    /// 2. Drag your SaveManager and AudioManager into the reference fields.
    /// 3. Drag in whichever UI elements you have - all are optional, so you
    ///    can start with just a mute toggle and add sliders later:
    ///    - Sfx Volume Slider / Music Volume Slider (range 0-1)
    ///    - Sfx Mute Toggle / Music Mute Toggle
    /// </summary>
    public class SettingsUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private SaveManager saveManager;
        [SerializeField] private AudioManager audioManager;

        [Header("UI Elements (optional)")]
        [SerializeField] private Slider sfxVolumeSlider;
        [SerializeField] private Slider musicVolumeSlider;
        [SerializeField] private Toggle sfxMuteToggle;
        [SerializeField] private Toggle musicMuteToggle;

        private void Start()
        {
            LoadIntoUI();
        }

        private void OnEnable()
        {
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.AddListener(HandleSfxVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.AddListener(HandleMusicVolumeChanged);
            if (sfxMuteToggle != null) sfxMuteToggle.onValueChanged.AddListener(HandleSfxMuteChanged);
            if (musicMuteToggle != null) musicMuteToggle.onValueChanged.AddListener(HandleMusicMuteChanged);
        }

        private void OnDisable()
        {
            if (sfxVolumeSlider != null) sfxVolumeSlider.onValueChanged.RemoveListener(HandleSfxVolumeChanged);
            if (musicVolumeSlider != null) musicVolumeSlider.onValueChanged.RemoveListener(HandleMusicVolumeChanged);
            if (sfxMuteToggle != null) sfxMuteToggle.onValueChanged.RemoveListener(HandleSfxMuteChanged);
            if (musicMuteToggle != null) musicMuteToggle.onValueChanged.RemoveListener(HandleMusicMuteChanged);
        }

        private void LoadIntoUI()
        {
            if (saveManager == null) return;

            if (sfxVolumeSlider != null) sfxVolumeSlider.value = saveManager.GetSfxVolume();
            if (musicVolumeSlider != null) musicVolumeSlider.value = saveManager.GetMusicVolume();
            if (sfxMuteToggle != null) sfxMuteToggle.isOn = saveManager.IsSfxMuted();
            if (musicMuteToggle != null) musicMuteToggle.isOn = saveManager.IsMusicMuted();
        }

        private void HandleSfxVolumeChanged(float value)
        {
            if (saveManager != null) saveManager.SetSfxVolume(value);
            if (audioManager != null) audioManager.SetSfxVolume(value);
        }

        private void HandleMusicVolumeChanged(float value)
        {
            if (saveManager != null) saveManager.SetMusicVolume(value);
            if (audioManager != null) audioManager.SetMusicVolume(value);
        }

        private void HandleSfxMuteChanged(bool muted)
        {
            if (saveManager != null) saveManager.SetSfxMuted(muted);
            if (audioManager == null) return;

            float volume = muted ? 0f : (sfxVolumeSlider != null ? sfxVolumeSlider.value : 1f);
            audioManager.SetSfxVolume(volume);
        }

        private void HandleMusicMuteChanged(bool muted)
        {
            if (saveManager != null) saveManager.SetMusicMuted(muted);
            if (audioManager == null) return;

            float volume = muted ? 0f : (musicVolumeSlider != null ? musicVolumeSlider.value : 1f);
            audioManager.SetMusicVolume(volume);
        }
    }
}
