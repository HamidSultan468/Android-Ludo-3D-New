using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Runtime companion dropped in by the scene builder: keeps the generated turn/dice/coins labels in
    /// sync with <see cref="LudoBoardLogic"/>, <see cref="LudoDiceRoller"/>, and <see cref="CurrencyManager"/>
    /// events - the turn label re-colors itself to the active player's color and tags whether it's the
    /// human's turn or an AI's, so the active player/character is unambiguous at a glance. Compiled into
    /// player builds - safe to ignore or remove if you drive your own HUD.
    /// </summary>
    public class LudoHudBinder : MonoBehaviour
    {
        // Mirrors LudoBoardSceneBuilder.PlayerColors' Neon Glow palette (Red -> magenta, Blue -> cyan) -
        // kept independent since HUD tinting is a display concern, not board-geometry, but intentionally
        // the same hues so the turn text always matches the actual token/tile color on the board.
        private static readonly Dictionary<PlayerColor, Color> TurnTextColors = new Dictionary<PlayerColor, Color>
        {
            { PlayerColor.Red, new Color(1f, 0.35f, 0.85f) },
            { PlayerColor.Green, new Color(0.45f, 0.95f, 0.55f) },
            { PlayerColor.Yellow, new Color(1f, 0.9f, 0.4f) },
            { PlayerColor.Blue, new Color(0.4f, 0.85f, 1f) },
        };

        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private LudoDiceRoller dice;
        [SerializeField] private Text turnText;
        [SerializeField] private Text coinsText;
        [SerializeField] private Text diceValueText;
        [SerializeField] private PlayerColor humanColor = PlayerColor.Red;
        private bool _allColorsHuman;

        public void Configure(LudoBoardLogic targetBoard, LudoDiceRoller targetDice, Text turnLabel, Text coinsLabel, Text diceLabel, PlayerColor humanPlayerColor)
        {
            board = targetBoard;
            dice = targetDice;
            turnText = turnLabel;
            coinsText = coinsLabel;
            diceValueText = diceLabel;
            humanColor = humanPlayerColor;
        }

        /// <summary>Switches between "Player vs AI" tagging (only <see cref="humanColor"/> shows "You",
        /// everyone else "AI") and Pass &amp; Play, where every color is a human passing the device -
        /// used by <see cref="LudoGameModeController"/> when Pass &amp; Play is the selected mode.</summary>
        public void SetAllHumanMode(bool allHuman)
        {
            _allColorsHuman = allHuman;
            if (board != null) HandleTurnChanged(board.CurrentPlayer);
        }

        private void OnEnable()
        {
            if (board != null)
            {
                board.OnTurnChanged += HandleTurnChanged;
                HandleTurnChanged(board.CurrentPlayer);
            }

            if (dice != null)
            {
                dice.OnDiceRollStarted += HandleDiceRollStarted;
                dice.OnDiceRollCompleted += HandleDiceRollCompleted;
            }

            if (CurrencyManager.Instance != null)
            {
                CurrencyManager.Instance.OnCoinsChanged += HandleCoinsChanged;
                HandleCoinsChanged(CurrencyManager.Instance.Coins);
            }
        }

        private void OnDisable()
        {
            if (board != null) board.OnTurnChanged -= HandleTurnChanged;

            if (dice != null)
            {
                dice.OnDiceRollStarted -= HandleDiceRollStarted;
                dice.OnDiceRollCompleted -= HandleDiceRollCompleted;
            }

            if (CurrencyManager.Instance != null) CurrencyManager.Instance.OnCoinsChanged -= HandleCoinsChanged;
        }

        private void HandleTurnChanged(PlayerColor color)
        {
            if (turnText == null) return;

            string who = (_allColorsHuman || color == humanColor) ? "You" : "AI";
            turnText.text = $"{color}'s Turn ({who})";
            turnText.color = TurnTextColors.TryGetValue(color, out Color tint) ? tint : Color.white;

            if (diceValueText != null) diceValueText.text = string.Empty; // clear the last player's roll
        }

        private void HandleDiceRollStarted()
        {
            if (diceValueText != null) diceValueText.text = "Rolling...";
        }

        private void HandleDiceRollCompleted(int value)
        {
            if (diceValueText != null) diceValueText.text = $"Dice: {value}";
        }

        private void HandleCoinsChanged(long coins)
        {
            if (coinsText != null) coinsText.text = $"Coins: {coins:N0}";
        }
    }
}
