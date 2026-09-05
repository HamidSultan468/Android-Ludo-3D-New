using LudoGame.Board;
using LudoGame.Dice;
using LudoGame.Game;
using LudoGame.Player;
using UnityEngine;

namespace LudoGame.Audio
{
    /// <summary>
    /// Central place for all game sounds. If you wire up the optional
    /// DiceManager / GameManager / TokenSelector references, common sounds
    /// (dice roll, token tap, capture, win) play automatically. You can also
    /// call PlaySfx()/PlayButtonClick() manually from anywhere.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on an empty GameObject named "AudioManager".
    /// 2. Add two AudioSource components to that same object (or children):
    ///    one for "Sfx Source" (Loop OFF) and one for "Music Source" (Loop ON).
    /// 3. Drag your sound clips into the fields below.
    /// 4. (Optional) Drag DiceManager / GameManager / TokenSelector in under
    ///    "Optional Auto-Hookup" so matching sounds play automatically.
    /// 5. For any UI Button (e.g. Roll, Play), add an extra OnClick entry
    ///    pointing at AudioManager.Instance.PlayButtonClick() for a click sound.
    /// </summary>
    [DisallowMultipleComponent]
    public class AudioManager : MonoBehaviour
    {
        public static AudioManager Instance { get; private set; }

        [Header("Audio Sources")]
        [SerializeField] private AudioSource sfxSource;
        [SerializeField] private AudioSource musicSource;

        [Header("Sound Effect Clips")]
        [SerializeField] private AudioClip diceRollClip;
        [SerializeField] private AudioClip tokenMoveClip;
        [SerializeField] private AudioClip captureClip;
        [SerializeField] private AudioClip winClip;
        [SerializeField] private AudioClip buttonClickClip;

        [Header("Background Music")]
        [SerializeField] private AudioClip backgroundMusic;
        [SerializeField] private bool playMusicOnStart = true;

        [Header("Optional Auto-Hookup (leave empty to trigger sounds manually instead)")]
        [SerializeField] private DiceManager diceManager;
        [SerializeField] private GameManager gameManager;
        [SerializeField] private TokenSelector tokenSelector;

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

        private void OnEnable()
        {
            if (diceManager != null) diceManager.OnRollStarted += HandleDiceRollStarted;
            if (tokenSelector != null) tokenSelector.OnValidTokenTapped += HandleTokenTapped;

            if (gameManager != null)
            {
                gameManager.OnTokenCaptured += HandleTokenCaptured;
                gameManager.OnPlayerWon += HandlePlayerWon;
            }
        }

        private void OnDisable()
        {
            if (diceManager != null) diceManager.OnRollStarted -= HandleDiceRollStarted;
            if (tokenSelector != null) tokenSelector.OnValidTokenTapped -= HandleTokenTapped;

            if (gameManager != null)
            {
                gameManager.OnTokenCaptured -= HandleTokenCaptured;
                gameManager.OnPlayerWon -= HandlePlayerWon;
            }
        }

        private void HandleDiceRollStarted() => PlaySfx(diceRollClip);
        private void HandleTokenTapped(PlayerToken token) => PlaySfx(tokenMoveClip);
        private void HandleTokenCaptured(PlayerToken mover, PlayerToken captured) => PlaySfx(captureClip);
        private void HandlePlayerWon(GridManager.PlayerColor color) => PlaySfx(winClip);

        /// <summary>Hook this to any UI Button's OnClick to give it a click sound.</summary>
        public void PlayButtonClick() => PlaySfx(buttonClickClip);

        /// <summary>Plays any one-shot sound effect through the shared SFX source.</summary>
        public void PlaySfx(AudioClip clip)
        {
            if (clip == null || sfxSource == null) return;
            sfxSource.PlayOneShot(clip);
        }

        /// <summary>Starts looping background music (replacing whatever was playing).</summary>
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
