using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace LudoGame.MiniGames.GulliDanda
{
    public enum GulliDandaState { Idle, Flipping, Charging, Struck, Fielding, Resolved }

    /// <summary>
    /// Drives one full turn of the Gulli Danda mini-game: tap to flip, hold
    /// then swipe-release to strike with the power meter, then the fielder
    /// (BhajuAI) either catches it or throws it back at the Kotha. Tracks
    /// chances left and total coins won.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty "GulliDandaGameManager" GameObject.
    /// 2. Drag in the GulliController, PowerMeter, and BhajuAI from the scene.
    /// 3. Hook a UI script to the events below (OnStateChanged, OnStruck,
    ///    OnTurnResolved, OnChancesChanged, OnGameOver) for the scoreboard,
    ///    power-meter fill, and "SWIPE TO STRIKE!" prompt.
    /// </summary>
    public class GulliDandaGameManager : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GulliController gulli;
        [SerializeField] private PowerMeter powerMeter;
        [SerializeField] private BhajuAI bhaju;

        [Header("Rules")]
        [SerializeField] private int totalChances = 3;
        [Tooltip("Minimum swipe distance (screen pixels) to count as a strike attempt.")]
        [SerializeField] private float minSwipePixels = 40f;

        public GulliDandaState State { get; private set; } = GulliDandaState.Idle;
        public int ChancesLeft { get; private set; }
        public int TotalCoins { get; private set; }

        public event System.Action<GulliDandaState> OnStateChanged;
        public event System.Action<float, HitQuality> OnStruck;
        public event System.Action<float, int> OnTurnResolved; // (distance, coinsWonThisTurn)
        public event System.Action<int> OnChancesChanged;
        public event System.Action OnGameOver;

        private Vector2 swipeStart;
        private bool swipeActive;

        private void OnEnable()
        {
            if (bhaju == null) return;
            bhaju.OnCaughtGulli += HandleCaughtByFielder;
            bhaju.OnHitKotha += HandleKothaHit;
            bhaju.OnThrowMissed += HandleThrowMissed;
        }

        private void OnDisable()
        {
            if (bhaju == null) return;
            bhaju.OnCaughtGulli -= HandleCaughtByFielder;
            bhaju.OnHitKotha -= HandleKothaHit;
            bhaju.OnThrowMissed -= HandleThrowMissed;
        }

        private void Start()
        {
            ChancesLeft = totalChances;
            OnChancesChanged?.Invoke(ChancesLeft);
        }

        private void Update()
        {
            if (gulli == null || powerMeter == null || bhaju == null) return;

            HandleTapToFlip();
            HandleSwipeToStrike();
        }

        private void HandleTapToFlip()
        {
            if (State != GulliDandaState.Idle) return;

            Pointer pointer = Pointer.current;
            if (pointer != null && pointer.press.wasPressedThisFrame)
                FlipGulli();
        }

        private void FlipGulli()
        {
            gulli.Flip();
            SetState(GulliDandaState.Flipping);
            StartCoroutine(BeginChargingAfterFlip());
        }

        private IEnumerator BeginChargingAfterFlip()
        {
            yield return new WaitForSeconds(0.25f); // brief pause matching the flip animation
            if (State != GulliDandaState.Flipping) yield break;

            powerMeter.StartCharging();
            SetState(GulliDandaState.Charging);
        }

        private void HandleSwipeToStrike()
        {
            if (State != GulliDandaState.Charging) return;

            Pointer pointer = Pointer.current;
            if (pointer == null) return;

            if (pointer.press.wasPressedThisFrame)
            {
                swipeStart = pointer.position.ReadValue();
                swipeActive = true;
            }
            else if (swipeActive && pointer.press.wasReleasedThisFrame)
            {
                swipeActive = false;
                Vector2 swipeEnd = pointer.position.ReadValue();
                Vector2 swipeDelta = swipeEnd - swipeStart;

                if (swipeDelta.magnitude < minSwipePixels)
                {
                    Miss(); // released without a real swipe
                    return;
                }

                Vector3 direction = new Vector3(swipeDelta.x, 0f, swipeDelta.y).normalized;
                StrikeGulli(direction);
            }
        }

        private void StrikeGulli(Vector3 swipeDirection)
        {
            (float power, HitQuality quality) result = powerMeter.Lock();

            if (result.quality == HitQuality.Miss)
            {
                Miss();
                return;
            }

            gulli.Strike(swipeDirection, result.power);
            OnStruck?.Invoke(result.power, result.quality);
            SetState(GulliDandaState.Struck);

            bhaju.BeginFielding();
            SetState(GulliDandaState.Fielding);
        }

        private void Miss()
        {
            powerMeter.StopCharging();
            ResolveTurn(0f, caughtMidAir: true); // treated like an out with no score
        }

        private void HandleCaughtByFielder() => ResolveTurn(0f, caughtMidAir: true);
        private void HandleKothaHit() => ResolveTurn(DistanceFromStrikeOrigin(), caughtMidAir: false);
        private void HandleThrowMissed() => ResolveTurn(DistanceFromStrikeOrigin(), caughtMidAir: false);

        private float DistanceFromStrikeOrigin()
        {
            Vector3 flatStrike = new Vector3(gulli.StrikeOrigin.x, 0f, gulli.StrikeOrigin.z);
            Vector3 flatNow = new Vector3(gulli.transform.position.x, 0f, gulli.transform.position.z);
            return Vector3.Distance(flatStrike, flatNow);
        }

        private void ResolveTurn(float distance, bool caughtMidAir)
        {
            int coins = RewardCalculator.CalculateCoins(distance, caughtMidAir);
            TotalCoins += coins;

            OnTurnResolved?.Invoke(distance, coins);
            SetState(GulliDandaState.Resolved);

            ChancesLeft = Mathf.Max(0, ChancesLeft - 1);
            OnChancesChanged?.Invoke(ChancesLeft);

            if (ChancesLeft <= 0)
                OnGameOver?.Invoke();
            else
                SetState(GulliDandaState.Idle);
        }

        private void SetState(GulliDandaState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }
    }
}
