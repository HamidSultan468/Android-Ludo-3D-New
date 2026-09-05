using System;
using System.Collections;
using UnityEngine;

namespace LudoGame.Dice
{
    /// <summary>
    /// Rolls the dice (1-6), optionally spins a 3D dice model while rolling,
    /// and tells the rest of the game the result through events. This script
    /// has no UI or board knowledge - GameManager listens to its events and
    /// decides what happens next (e.g. calling PlayerToken.MoveByDice).
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on an empty GameObject named "DiceManager".
    /// 2. (Optional) Drag your dice's 3D model into "Dice Model" if you want
    ///    it to visually spin during the roll. Leave it empty to skip that.
    /// 3. Call Roll() from a UI button's OnClick, or from GameManager, whenever
    ///    it's time for the current player to roll.
    /// 4. Subscribe to OnDiceRolled(int value) elsewhere to react to the result.
    /// </summary>
    public class DiceManager : MonoBehaviour
    {
        [Header("Dice Settings")]
        [SerializeField] private int minValue = 1;
        [SerializeField] private int maxValue = 6;

        [Header("Roll Animation (optional)")]
        [Tooltip("Drag the dice's 3D model here if you want it to visually spin while rolling. Leave empty to skip animation.")]
        [SerializeField] private Transform diceModel;

        [Tooltip("How long the spin animation plays before the result is revealed.")]
        [SerializeField] private float rollDuration = 0.8f;

        [Tooltip("How fast the dice model spins while rolling (degrees per second).")]
        [SerializeField] private float spinSpeed = 720f;

        [Header("Three-Sixes Rule")]
        [Tooltip("Classic Ludo rule: rolling three 6s in a row cancels the turn. Set to 0 to turn this rule off.")]
        [SerializeField] private int maxConsecutiveSixes = 3;

        /// <summary>True while the roll animation is playing (use this to disable the Roll button).</summary>
        public bool IsRolling { get; private set; }

        /// <summary>The value shown by the last completed roll.</summary>
        public int LastValue { get; private set; }

        private int consecutiveSixes;

        /// <summary>Raised the moment a roll begins.</summary>
        public event Action OnRollStarted;

        /// <summary>Raised with the rolled value (1-6) once the roll animation finishes normally.</summary>
        public event Action<int> OnDiceRolled;

        /// <summary>Raised instead of OnDiceRolled when three 6s in a row cancel the turn (classic rule).</summary>
        public event Action OnTurnCancelledBySixes;

        /// <summary>Starts a dice roll. Does nothing if a roll is already in progress.</summary>
        public void Roll()
        {
            if (IsRolling) return;
            StartCoroutine(RollRoutine());
        }

        private IEnumerator RollRoutine()
        {
            IsRolling = true;
            OnRollStarted?.Invoke();

            if (diceModel != null)
            {
                float t = 0f;
                int safety = 0;
                // Safety net: if Time.deltaTime is ever stuck at 0 (e.g. Time.timeScale == 0,
                // such as a paused game), "t" would never advance and this would wait forever.
                // Bail out after an absurd number of frames instead of hanging the roll.
                while (t < rollDuration && safety < 100000)
                {
                    t += Time.deltaTime;
                    diceModel.Rotate(Vector3.one * (spinSpeed * Time.deltaTime), Space.Self);
                    safety++;
                    yield return null;
                }

                if (safety >= 100000)
                    Debug.LogWarning("DiceManager: roll-spin safety limit hit (Time.deltaTime may be stuck at 0, e.g. Time.timeScale == 0). Skipping straight to the result.", this);

                diceModel.rotation = Quaternion.identity; // settle so it doesn't stay mid-spin
            }

            int value = UnityEngine.Random.Range(minValue, maxValue + 1);
            LastValue = value;

            if (value == 6)
            {
                consecutiveSixes++;
                if (maxConsecutiveSixes > 0 && consecutiveSixes >= maxConsecutiveSixes)
                {
                    consecutiveSixes = 0;
                    IsRolling = false;
                    OnTurnCancelledBySixes?.Invoke();
                    yield break;
                }
            }
            else
            {
                consecutiveSixes = 0;
            }

            IsRolling = false;
            OnDiceRolled?.Invoke(value);
        }
    }
}
