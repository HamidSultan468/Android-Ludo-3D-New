using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.MiniGames.GulliDanda
{
    /// <summary>
    /// Wires the Gulli Danda scoreboard, power meter, and "SWIPE TO STRIKE!"
    /// prompt to GulliDandaGameManager's events. Purely a display layer - all
    /// rules stay inside GulliDandaGameManager/PowerMeter/BhajuAI.
    ///
    /// Setup (in the Unity Editor): built automatically by
    /// Window > Ludo Tools > Mini-Games > Gulli Danda UI Builder. To wire by
    /// hand instead, put this on your Gulli Danda Canvas and fill in the
    /// fields below.
    /// </summary>
    public class GulliDandaUI : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private GulliDandaGameManager gameManager;
        [SerializeField] private PowerMeter powerMeter;

        [Header("Power Meter")]
        [SerializeField] private Image powerMeterFill;
        [SerializeField] private Color missColor = new Color(0.8f, 0.2f, 0.2f);
        [SerializeField] private Color goodColor = new Color(0.85f, 0.75f, 0.15f);
        [SerializeField] private Color perfectColor = new Color(0.2f, 0.75f, 0.3f);

        [Header("Scoreboard")]
        [SerializeField] private Text distanceText;
        [SerializeField] private Text chancesText;
        [SerializeField] private Text coinsText;

        [Header("Chances (optional visual pips instead of/alongside chancesText)")]
        [Tooltip("One Image per available chance, in order. Dims out any beyond the chances remaining.")]
        [SerializeField] private Image[] chancePips;
        [SerializeField] private Color chancePipActiveColor = new Color(0.85f, 0.55f, 0.2f);
        [SerializeField] private Color chancePipUsedColor = new Color(0.3f, 0.3f, 0.3f, 0.4f);

        [Header("Prompt")]
        [SerializeField] private GameObject swipePrompt;

        [Header("Game Over")]
        [SerializeField] private GameObject gameOverPanel;
        [SerializeField] private Text gameOverText;

        private void OnEnable()
        {
            if (gameManager == null) return;

            gameManager.OnStateChanged += HandleStateChanged;
            gameManager.OnTurnResolved += HandleTurnResolved;
            gameManager.OnChancesChanged += HandleChancesChanged;
            gameManager.OnGameOver += HandleGameOver;
        }

        private void OnDisable()
        {
            if (gameManager == null) return;

            gameManager.OnStateChanged -= HandleStateChanged;
            gameManager.OnTurnResolved -= HandleTurnResolved;
            gameManager.OnChancesChanged -= HandleChancesChanged;
            gameManager.OnGameOver -= HandleGameOver;
        }

        private void Update()
        {
            if (powerMeter == null || powerMeterFill == null || !powerMeter.IsCharging) return;

            powerMeterFill.fillAmount = powerMeter.CurrentValue;
            powerMeterFill.color = ColorForQuality(powerMeter.GetQualityForValue(powerMeter.CurrentValue));
        }

        private void HandleStateChanged(GulliDandaState state)
        {
            if (swipePrompt != null)
                swipePrompt.SetActive(state == GulliDandaState.Charging);

            if (powerMeterFill != null && state == GulliDandaState.Idle)
                powerMeterFill.fillAmount = 0f;
        }

        private void HandleTurnResolved(float distance, int coinsThisTurn)
        {
            if (distanceText != null) distanceText.text = "Distance Cover: " + distance.ToString("F1") + " M";
            if (coinsText != null) coinsText.text = gameManager.TotalCoins + " coins";
        }

        private void HandleChancesChanged(int chancesLeft)
        {
            if (chancesText != null) chancesText.text = "Chances Left: " + chancesLeft;

            if (chancePips == null) return;
            for (int i = 0; i < chancePips.Length; i++)
            {
                if (chancePips[i] == null) continue;
                chancePips[i].color = i < chancesLeft ? chancePipActiveColor : chancePipUsedColor;
            }
        }

        private void HandleGameOver()
        {
            if (gameOverPanel != null) gameOverPanel.SetActive(true);
            if (gameOverText != null) gameOverText.text = "Game Over! You won " + gameManager.TotalCoins + " coins";
        }

        private Color ColorForQuality(HitQuality quality)
        {
            switch (quality)
            {
                case HitQuality.Perfect: return perfectColor;
                case HitQuality.Good: return goodColor;
                default: return missColor;
            }
        }
    }
}
