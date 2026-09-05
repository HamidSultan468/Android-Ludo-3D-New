using System.Collections.Generic;
using UnityEngine;
using LudoEmpire.Ludo; // LudoMusicManager - called, never modified.

namespace TangentLudoEmpire.Core
{
    /// <summary>Task C.2 named SFX cues. String-keyed <see cref="SoundManager.PlaySFX(string)"/> still
    /// works underneath (this enum's <c>ToString()</c> IS the registry key) - it's just a typo-proof
    /// front door for the four cues the spec named.</summary>
    public enum SfxKey { DiceRoll, TokenMove, Win, Lose }

    /// <summary>Task C.2 named BGM tracks.</summary>
    public enum BgmKey { MainMenu, InGame }

    /// <summary>
    /// Front door for audio. Phase 1 shell:
    ///   * <see cref="SetMusicVolume"/> forwards to the existing <see cref="LudoMusicManager"/> singleton.
    ///   * <see cref="PlaySFX(string)"/> is a named-clip one-shot player (registry is empty for now - clips get
    ///     added in a later art pass). The existing <c>LudoSfxController</c> keeps handling in-match SFX
    ///     via board events untouched; this is for UI / meta screens.
    /// Everything logs so the flow is visible while wiring the rest of the ecosystem.
    ///
    /// Phase 5.3 (Task C.2) extension: <see cref="SfxKey"/>/<see cref="BgmKey"/> overloads for the four
    /// named SFX cues and two named BGM tracks, plus <see cref="ApplySavedVolumes"/> so
    /// <see cref="TangentLudoEmpire.Polish.SettingsManager"/>'s volume slider has something to push into.
    /// LudoMusicManager (the do-not-touch layer) only ever plays ONE music track at a time, so
    /// <see cref="PlayBgm"/> is a start/stop toggle for now, not real track-switching -
    /// TODO(art pass): once per-track BGM clips exist, register them like SFX clips and have PlayBgm
    /// actually swap LudoMusicManager's source instead of just Play()/Stop().
    /// </summary>
    [DisallowMultipleComponent]
    public class SoundManager : MonoBehaviour
    {
        public static SoundManager Instance { get; private set; }

        [Range(0f, 1f)] [SerializeField] private float musicVolume = 1f;
        [Range(0f, 1f)] [SerializeField] private float sfxVolume = 1f;

        private AudioSource _uiSource;
        private readonly Dictionary<string, AudioClip> _clips = new Dictionary<string, AudioClip>();

        public float MusicVolume => musicVolume;
        public float SfxVolume => sfxVolume;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()

            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;
        }

        // ---- public API ----

        public void SetMusicVolume(float v)
        {
            musicVolume = Mathf.Clamp01(v);
            Debug.Log($"[SoundManager] SetMusicVolume({musicVolume:0.00})");
            var mm = LudoMusicManager.Instance;
            if (mm != null) mm.SetVolume(musicVolume);
        }

        public void SetSfxVolume(float v)
        {
            sfxVolume = Mathf.Clamp01(v);
            Debug.Log($"[SoundManager] SetSfxVolume({sfxVolume:0.00})");
        }

        /// <summary>Registers a clip under a name so <see cref="PlaySFX"/> can trigger it (called by the
        /// audio-setup pass / a ScriptableObject bank later).</summary>
        public void RegisterClip(string key, AudioClip clip)
        {
            if (string.IsNullOrEmpty(key) || clip == null) return;
            _clips[key] = clip;
        }

        public void PlaySFX(string key)
        {
            Debug.Log($"[SoundManager] PlaySFX(\"{key}\")");
            if (_uiSource != null && _clips.TryGetValue(key, out var clip) && clip != null)
                _uiSource.PlayOneShot(clip, sfxVolume);
        }

        public void PlayMusic() => LudoMusicManager.Instance?.Play();
        public void StopMusic() => LudoMusicManager.Instance?.Stop();

        // ---- Phase 5.3 (Task C.2): named SFX/BGM + settings-driven volume ----

        /// <summary>Registers a clip for one of the four named SFX cues.</summary>
        public void RegisterClip(SfxKey key, AudioClip clip) => RegisterClip(key.ToString(), clip);

        /// <summary>Plays one of the four named SFX cues (DiceRoll/TokenMove/Win/Lose). A no-op (logged)
        /// if no clip was ever registered for it - never throws, an unassigned cue must not crash gameplay.</summary>
        public void PlaySFX(SfxKey key) => PlaySFX(key.ToString());

        /// <summary>Starts one of the two named BGM tracks. See class remarks: today this is a start/stop
        /// toggle on the single shared LudoMusicManager track, not real per-track switching.</summary>
        public void PlayBgm(BgmKey key)
        {
            Debug.Log($"[SoundManager] PlayBgm({key})");
            PlayMusic();
        }

        /// <summary>Pushes <see cref="TangentLudoEmpire.Polish.SettingsManager"/>'s saved SoundVolume/
        /// MusicVolume into this manager - called once at startup and whenever the Settings volume
        /// sliders change.</summary>
        public void ApplySavedVolumes(float sfxVol, float musicVol)
        {
            SetSfxVolume(sfxVol);
            SetMusicVolume(musicVol);
        }
    }
}
