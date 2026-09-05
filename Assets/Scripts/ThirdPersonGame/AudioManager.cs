using UnityEngine;

namespace LudoGame.ThirdPersonGame
{
    /// <summary>
    /// Central place for this game's sounds: looping background music, footsteps that
    /// play automatically while the player walks, and action-specific cues (praying,
    /// defeat). Other scripts (PlayerAnimatorController) call through
    /// AudioManager.Instance rather than holding their own AudioSources.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty GameObject named "AudioManager".
    /// 2. Add two AudioSource components to it: one for "Sfx Source" (Loop OFF) and one
    ///    for "Music Source" (Loop ON) - drag each into the matching field below.
    /// 3. Drag your clips into the fields below (footsteps accepts several clips and
    ///    picks one at random each step, so it doesn't sound too repetitive).
    /// 4. Drag your player's PlayerMovement into "Player Movement" so footsteps play
    ///    automatically while it's moving.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

        [Header("Background Music")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private bool playMusicOnStart = true;

        [Header("Footsteps")]
        [Tooltip("One is picked at random each step, so it doesn't sound too repetitive.")]
        [SerializeField] private AudioClip[] footstepClips;
        [Tooltip("Seconds between footstep sounds while the player is moving.")]
        [SerializeField] private float footstepInterval = 0.4f;

        [Header("Action Sounds")]
        [SerializeField] private AudioClip prayClip;
        [SerializeField] private AudioClip defeatedClip;
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Optional Auto-Hookup (leave empty to trigger sounds manually instead)")]
        [SerializeField] private PlayerMovement playerMovement;

        private float footstepTimer;

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

        private void Start()
        {
            if (playMusicOnStart && backgroundMusic != null)
                PlayMusic(backgroundMusic);
        }

        private void Update()
        {
            HandleFootsteps();
        }

        private void HandleFootsteps()
        {
            if (playerMovement == null || footstepClips == null || footstepClips.Length == 0) return;

            if (!playerMovement.IsMoving)
            {
                footstepTimer = 0f; // reset so the first step after standing still plays immediately
                return;
            }

            footstepTimer += Time.deltaTime;
            if (footstepTimer < footstepInterval) return;

            footstepTimer = 0f;
            PlaySfx(footstepClips[Random.Range(0, footstepClips.Length)]);
        }

        public void PlayPraySound() => PlaySfx(prayClip);
        public void PlayDefeatedSound() => PlaySfx(defeatedClip);

        /// <summary>Hook this to any UI Button's OnClick to give it a click sound.</summary>
        public void PlayButtonClick() => PlaySfx(buttonClickClip);

        /// <summary>Plays any one-shot sound effect through the shared SFX source.</summary>
        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        public void PlayMusic(AudioClip clip)
        {
            if (clip == null || musicSource == null) return;
            musicSource.clip = clip;
            musicSource.loop = true;
            musicSource.Play();
        }

        public void StopMusic()
        {
            if (musicSource != null) musicSource.Stop();
        }

        public void SetSfxVolume(float volume)
        {
            if (sfxSource != null) sfxSource.volume = Mathf.Clamp01(volume);
        }

        public void SetMusicVolume(float volume)
        {
            if (musicSource != null) musicSource.volume = Mathf.Clamp01(volume);
        }
    }
}
