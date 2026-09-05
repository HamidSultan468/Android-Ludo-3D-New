using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Runtime companion dropped in by the scene builder: owns the Roll Dice button's interactable
    /// state so a player can't spam extra rolls in mid-throw or act out of turn. The button is
    /// disabled the instant it's clicked and only re-enabled once <see cref="LudoBoardLogic"/>
    /// reports it's the human player's turn to roll again (a normal turn change, or an extra turn
    /// granted by a six/capture/reaching home). Compiled into player builds - safe to ignore or
    /// remove if you drive the roll button yourself.
    /// </summary>
    public class LudoRollButtonController : MonoBehaviour
    {
        [SerializeField] private LudoBoardLogic board;
        [SerializeField] private LudoDiceRoller dice;
        [SerializeField] private Button rollButton;
        [SerializeField] private PlayerColor humanColor = PlayerColor.Red;
        private bool _allColorsHuman;

        public void Configure(LudoBoardLogic targetBoard, LudoDiceRoller targetDice, Button button, PlayerColor humanPlayerColor)
        {
            board = targetBoard;
            dice = targetDice;
            rollButton = button;
            humanColor = humanPlayerColor;
        }

        /// <summary>Switches from "only <see cref="humanColor"/> can roll" to "whoever's turn it is can
        /// roll" - used by <see cref="LudoGameModeController"/> when Pass &amp; Play is the selected mode
        /// (every color is a human passing the device, so the button should enable for all of them).</summary>
        public void SetAllHumanMode(bool allHuman)
        {
            _allColorsHuman = allHuman;
            if (board != null) SetInteractable(_allColorsHuman || board.CurrentPlayer == humanColor);
        }

        private void OnEnable()
        {
            if (board == null)
            {
                Debug.LogWarning("[LudoRollButtonController] No board assigned; the Roll button's enabled state won't track turns.", this);
                return;
            }

            board.OnTurnChanged += HandleTurnChanged;
            board.OnExtraTurnGranted += HandleExtraTurnGranted;
            HandleTurnChanged(board.CurrentPlayer);
        }

        private void OnDisable()
        {
            if (board == null) return;
            board.OnTurnChanged -= HandleTurnChanged;
            board.OnExtraTurnGranted -= HandleExtraTurnGranted;
        }

        /// <summary>Wired to the Roll button's onClick. Disables the button immediately, before the physical roll even starts.</summary>
        public void RollClicked()
        {
            SetInteractable(false);
            if (dice != null) dice.Roll();
        }

        private void HandleTurnChanged(PlayerColor color) => SetInteractable(_allColorsHuman || color == humanColor);

        private void HandleExtraTurnGranted(PlayerColor color, int diceValue) => SetInteractable(_allColorsHuman || color == humanColor);

        private void SetInteractable(bool value)
        {
            if (rollButton != null) rollButton.interactable = value;
        }
    }
}
