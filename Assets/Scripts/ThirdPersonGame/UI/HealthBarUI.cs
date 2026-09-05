using LudoGame.ThirdPersonGame;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.ThirdPersonGame.UI
{
    /// <summary>
    /// A fillable health bar. Purely a display layer - it has no idea what "health" means
    /// or how it changes; something else (the player's health/damage script, built in the
    /// Gameplay Logic phase) calls SetHealth(current, max) whenever it changes.
    ///
    /// Setup (in the Unity Editor):
    /// 1. Build a health bar under your Canvas: an Image with "Image Type" set to
    ///    "Filled" (Horizontal fill), or a UI Slider - either works with the fields below.
    /// 2. Drag it into "Fill Image" (if using an Image) and/or "Fill Slider" (if using a
    ///    Slider) - you only need one, not both.
    /// 3. (Optional) Drag a UI Text into "Health Text" to also show "80 / 100".
    /// 4. (Optional) Drag the player's PlayerHealth into "Player Health" so this updates
    ///    itself automatically - leave empty to call SetHealth(...) manually instead.
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [Header("Auto-Hookup (optional - leave empty to call SetHealth() manually instead)")]
        [SerializeField] private PlayerHealth playerHealth;

        [Header("UI Elements (use either or both)")]
        [SerializeField] private Image fillImage;
        [SerializeField] private Slider fillSlider;
        [SerializeField] private Text healthText;

        [Header("Color Feedback (optional)")]
        [SerializeField] private bool tintByHealth = true;
        [SerializeField] private Color fullHealthColor = Color.green;
        [SerializeField] private Color lowHealthColor = Color.red;
        [Tooltip("Health fraction (0-1) at or below which the bar is fully tinted to Low Health Color.")]
        [SerializeField, Range(0f, 1f)] private float lowHealthThreshold = 0.3f;

        private void OnEnable()
        {
            if (playerHealth == null) return;
            playerHealth.OnHealthChanged += SetHealth;
        }

        private void OnDisable()
        {
            if (playerHealth == null) return;
            playerHealth.OnHealthChanged -= SetHealth;
        }

        /// <summary>Call this whenever the player's health changes (done automatically if "Player Health" is assigned).</summary>
        public void SetHealth(int current, int max)
        {
            max = Mathf.Max(max, 1); // avoid divide-by-zero if max health is ever 0
            float fraction = Mathf.Clamp01((float)current / max);

            if (fillImage != null) fillImage.fillAmount = fraction;
            if (fillSlider != null) fillSlider.value = fraction;
            if (healthText != null) healthText.text = current + " / " + max;

            if (tintByHealth && fillImage != null)
            {
                float colorT = lowHealthThreshold > 0f ? Mathf.Clamp01(fraction / lowHealthThreshold) : 1f;
                fillImage.color = Color.Lerp(lowHealthColor, fullHealthColor, colorT);
            }
        }
    }
}
