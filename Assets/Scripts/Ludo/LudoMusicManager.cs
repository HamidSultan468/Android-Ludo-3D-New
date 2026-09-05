using System;
using System.Collections;
using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Single persistent background-music player for the whole app - survives every scene load
    /// (Main Menu -&gt; board, Replay, etc.) via <see cref="DontDestroyOnLoad"/>, the same singleton
    /// pattern as <see cref="CurrencyManager"/>. Holds one looping <see cref="AudioClip"/> slot and a
    /// volume control, and starts playing automatically on Awake if a clip is assigned - left empty, it
    /// stays silently idle rather than throwing, exactly like this project's other optional SFX hooks.
    /// Volume and Play/Stop transitions fade smoothly instead of jumping abruptly.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    [RequireComponent(typeof(AudioSource))]
    public class LudoMusicManager : MonoBehaviour
    {
        public static LudoMusicManager Instance { get; private set; }

        [Header("Track")]
        [Tooltip("Looping background music clip suited to the Sci-Fi Neon board - a moody synth/ambient " +
                 "electronic loop reads best. Leave empty to stay silent; no error either way.")]
        [SerializeField] private AudioClip musicClip;

        [Header("Volume")]
        [Tooltip("The volume slider/control - drag it here in the Inspector, or drive it at runtime via SetVolume (e.g. from a settings UI).")]
        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.5f;

        [Header("Playback")]
        [SerializeField] private bool playOnAwake = true;
        [Tooltip("Seconds to fade in/out on Play/Stop/volume changes, so transitions are smooth rather than an abrupt jump.")]
        [SerializeField] private float fadeDuration = 1.5f;

        private AudioSource _audioSource;
        private Coroutine _fadeRoutine;

        /// <summary>Current target volume (0-1). Reflects what was last set via the Inspector or <see cref="SetVolume"/>.</summary>
        public float Volume => volume;
        public bool IsPlaying => _audioSource != null && _audioSource.isPlaying;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);

            _audioSource = GetComponent<AudioSource>();
            _audioSource.loop = true;
            _audioSource.playOnAwake = false;
            _audioSource.spatialBlend = 0f; // 2D - background music shouldn't attenuate with listener position
            _audioSource.volume = 0f; // starts silent; Play() below fades it up to avoid a jump-cut start
            _audioSource.clip = musicClip;

            if (playOnAwake && musicClip != null)
            {
                Play();
            }
        }

        /// <summary>Starts (or resumes) looping playback of the assigned clip, fading in smoothly. A
        /// no-op warning (not an error) if no clip has been assigned yet.</summary>
        public void Play()
        {
            if (_audioSource == null) return;

            if (musicClip == null)
            {
                Debug.LogWarning("[LudoMusicManager] Play() ignored: no music clip assigned.", this);
                return;
            }

            if (_audioSource.clip != musicClip) _audioSource.clip = musicClip;
            if (!_audioSource.isPlaying) _audioSource.Play();

            FadeTo(volume, null);
        }

        /// <summary>Fades out and stops playback.</summary>
        public void Stop()
        {
            if (_audioSource == null) return;
            FadeTo(0f, () => { if (_audioSource != null) _audioSource.Stop(); });
        }

        /// <summary>Swaps in a different track - e.g. a distinct menu vs. board mood - and starts playing it.</summary>
        public void PlayClip(AudioClip clip)
        {
            if (clip == null)
            {
                Debug.LogWarning("[LudoMusicManager] PlayClip ignored: clip is null.", this);
                return;
            }

            musicClip = clip;
            Play();
        }

        /// <summary>Sets the target music volume (0-1, clamped), fading smoothly toward it rather than
        /// snapping instantly. Safe to call whether or not music is currently playing.</summary>
        public void SetVolume(float newVolume)
        {
            volume = Mathf.Clamp01(newVolume);
            if (_audioSource != null && _audioSource.isPlaying)
            {
                FadeTo(volume, null);
            }
        }

        private void FadeTo(float targetVolume, Action onComplete)
        {
            if (_audioSource == null) return;

            if (_fadeRoutine != null) StopCoroutine(_fadeRoutine);
            _fadeRoutine = StartCoroutine(FadeRoutine(targetVolume, onComplete));
        }

        private IEnumerator FadeRoutine(float targetVolume, Action onComplete)
        {
            float startVolume = _audioSource.volume;
            float duration = Mathf.Max(0.01f, fadeDuration);
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.unscaledDeltaTime;
                _audioSource.volume = Mathf.Lerp(startVolume, targetVolume, elapsed / duration);
                yield return null;
            }

            _audioSource.volume = targetVolume;
            _fadeRoutine = null;
            onComplete?.Invoke();
        }
    }
}
