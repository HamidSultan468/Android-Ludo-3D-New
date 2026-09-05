using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Centralized SFX hook for the board: every clip is an optional placeholder - leave any of them
    /// unassigned and that event simply stays silent, so this component is safe to wire up before any
    /// real audio content exists. Reacts to <see cref="LudoBoardLogic"/>/<see cref="LudoDiceRoller"/>
    /// events (dice rolling/landing, a token stepping, a capture, reaching home, and turn changes) purely
    /// by subscription - no per-frame polling. Attach anywhere with an <see cref="AudioSource"/> -
    /// <see cref="LudoBoardSceneBuilder"/> wires this up automatically via <see cref="Configure"/>.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class LudoSfxController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private LudoDiceRoller diceRoller;

        [Header("Clips (optional placeholders - assign to enable; leave empty to stay silent)")]
        [SerializeField] private AudioClip diceRollClip;
        [SerializeField] private AudioClip diceLandClip;
        [SerializeField] private AudioClip tokenStepClip;
        [SerializeField] private AudioClip captureClip;
        [SerializeField] private AudioClip turnChangeClip;
        [Tooltip("A single token reaching its home stretch's final cell - not the whole game being won (see gameWinClip).")]
        [SerializeField] private AudioClip tokenHomeClip;
        [Tooltip("A player getting all 4 tokens home and winning the match.")]
        [SerializeField] private AudioClip gameWinClip;

        [Range(0f, 1f)]
        [SerializeField] private float volume = 0.8f;

        private AudioSource _audioSource;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.OnTurnChanged += HandleTurnChanged;
                board.OnTokenMoved += HandleTokenMoved;
                board.OnTokenCaptured += HandleTokenCaptured;
                board.OnTokenReachedHome += HandleTokenReachedHome;
                board.OnPlayerFinished += HandlePlayerFinished;
            }

            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted += HandleDiceRollStarted;
                diceRoller.OnDiceRollCompleted += HandleDiceRollCompleted;
            }
        }

        private void OnDisable()
        {
            if (board != null)
            {
                board.OnTurnChanged -= HandleTurnChanged;
                board.OnTokenMoved -= HandleTokenMoved;
                board.OnTokenCaptured -= HandleTokenCaptured;
                board.OnTokenReachedHome -= HandleTokenReachedHome;
                board.OnPlayerFinished -= HandlePlayerFinished;
            }

            if (diceRoller != null)
            {
                diceRoller.OnDiceRollStarted -= HandleDiceRollStarted;
                diceRoller.OnDiceRollCompleted -= HandleDiceRollCompleted;
            }
        }

        /// <summary>Wires the board/dice this controller reacts to. Safe to call before OnEnable has run
        /// (the scene builder calls this immediately after AddComponent, same as the other binders).</summary>
        public void Configure(LudoBoardLogic targetBoard, LudoDiceRoller targetDice)
        {
            board = targetBoard;
            diceRoller = targetDice;
        }

        private void HandleDiceRollStarted() => Play(diceRollClip);

        private void HandleDiceRollCompleted(int value) => Play(diceLandClip);

        private void HandleTurnChanged(PlayerColor color) => Play(turnChangeClip);

        private void HandleTokenMoved(PlayerColor color, int tokenId, int fromPos, int toPos) => Play(tokenStepClip);

        private void HandleTokenCaptured(PlayerColor victimColor, int victimTokenId, PlayerColor byColor, int byTokenId) => Play(captureClip);

        private void HandleTokenReachedHome(PlayerColor color, int tokenId) => Play(tokenHomeClip);

        private void HandlePlayerFinished(PlayerColor color) => Play(gameWinClip);

        /// <summary>Plays a one-shot clip if one has been assigned; a pure no-op otherwise, so every
        /// gameplay hook above is always safe to call even before real audio content is dropped in.</summary>
        private void Play(AudioClip clip)
        {
            if (clip == null || _audioSource == null) return;
            _audioSource.PlayOneShot(clip, volume);
        }
    }
}
