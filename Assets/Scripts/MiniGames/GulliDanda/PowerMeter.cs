using UnityEngine;

namespace LudoGame.MiniGames.GulliDanda
{
    public enum HitQuality { Miss, Good, Perfect }

    /// <summary>
    /// The timing/power bar: a value oscillates back and forth between 0 and 1
    /// while "charging"; the player locks it in (on their swipe) and this
    /// reports which zone (Miss/Good/Perfect) it landed in.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on an empty GameObject (or the power-bar UI object).
    /// 2. Drive a UI Image's fillAmount from CurrentValue every frame while
    ///    IsCharging is true, to visualize the semi-circular meter.
    /// 3. GulliDandaGameManager calls StartCharging() then Lock().
    /// </summary>
    public class PowerMeter : MonoBehaviour
    {
        [Header("Oscillation")]
        [Tooltip("How many full back-and-forth sweeps per second.")]
        [SerializeField] private float cyclesPerSecond = 1f;

        [Header("Zone Thresholds (0-1)")]
        [Tooltip("Value must be at or above this to count as Perfect (green zone).")]
        [SerializeField, Range(0f, 1f)] private float perfectZoneStart = 0.85f;
        [Tooltip("Value must be at or above this to count as Good (yellow zone); below it is a Miss (red zone).")]
        [SerializeField, Range(0f, 1f)] private float goodZoneStart = 0.55f;

        /// <summary>Current 0-1 position of the meter (ping-pongs continuously while charging).</summary>
        public float CurrentValue { get; private set; }

        public bool IsCharging { get; private set; }

        private float elapsed;

        public void StartCharging()
        {
            elapsed = 0f;
            IsCharging = true;
        }

        public void StopCharging()
        {
            IsCharging = false;
        }

        private void Update()
        {
            if (!IsCharging) return;

            elapsed += Time.deltaTime;
            float t = elapsed * cyclesPerSecond;
            CurrentValue = Mathf.PingPong(t, 1f);
        }

        /// <summary>Locks in the current value and reports the resulting power (0-1) and quality zone.</summary>
        public (float power, HitQuality quality) Lock()
        {
            StopCharging();
            return (CurrentValue, GetQualityForValue(CurrentValue));
        }

        /// <summary>Which zone (Miss/Good/Perfect) a given 0-1 value falls into. Useful for live UI color feedback.</summary>
        public HitQuality GetQualityForValue(float value)
        {
            return value >= perfectZoneStart ? HitQuality.Perfect
                 : value >= goodZoneStart ? HitQuality.Good
                 : HitQuality.Miss;
        }
    }
}
