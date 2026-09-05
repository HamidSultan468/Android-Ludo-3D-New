using LudoGame.Board;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>
    /// Wires the Killa Bandar HUD (shoe count, state, current Protector,
    /// Sprint button) to KillaBandarGameManager's events. Purely a display
    /// layer - all rules stay inside KillaBandarGameManager/ShoePile.
    /// </summary>
    public class KillaBandarUI : MonoBehaviour
    {
        [SerializeField] private KillaBandarGameManager gameManager;

        [Header("HUD")]
        [SerializeField] private Text shoesText;
        [SerializeField] private Text stateText;
        [SerializeField] private Text protectorText;

        [Header("Sprint Button")]
        [Tooltip("Shown only once shoes are critically low/empty (Attacker Strike state).")]
        [SerializeField] private Button sprintButton;

        private void OnEnable()
        {
            if (gameManager == null) return;

            gameManager.OnStateChanged += HandleStateChanged;
            gameManager.OnShoesChanged += HandleShoesChanged;
            gameManager.OnRoleSwapped += HandleRoleSwapped;
            gameManager.OnMilestoneSprintSucceeded += HandleMilestoneSprintSucceeded;

            if (sprintButton != null) sprintButton.onClick.AddListener(gameManager.BeginMilestoneSprint);

            Refresh();
        }

        private void OnDisable()
        {
            if (gameManager == null) return;

            gameManager.OnStateChanged -= HandleStateChanged;
            gameManager.OnShoesChanged -= HandleShoesChanged;
            gameManager.OnRoleSwapped -= HandleRoleSwapped;
            gameManager.OnMilestoneSprintSucceeded -= HandleMilestoneSprintSucceeded;

            if (sprintButton != null) sprintButton.onClick.RemoveListener(gameManager.BeginMilestoneSprint);
        }

        private void Refresh()
        {
            if (gameManager == null) return;

            if (stateText != null) stateText.text = gameManager.State.ToString();
            if (protectorText != null && gameManager.Protector != null)
                protectorText.text = gameManager.Protector.Color + " is Killa Bandar";

            if (sprintButton != null)
                sprintButton.gameObject.SetActive(gameManager.State == KillaBandarState.AttackerStrike);
        }

        private void HandleStateChanged(KillaBandarState state)
        {
            if (stateText != null) stateText.text = state.ToString();
            if (sprintButton != null) sprintButton.gameObject.SetActive(state == KillaBandarState.AttackerStrike);
        }

        private void HandleShoesChanged(int amount)
        {
            if (shoesText != null) shoesText.text = "Shoes: " + amount;
        }

        private void HandleRoleSwapped(GridManager.PlayerColor newProtector)
        {
            if (protectorText != null) protectorText.text = newProtector + " is Killa Bandar";
        }

        private void HandleMilestoneSprintSucceeded()
        {
            if (protectorText != null && gameManager.Protector != null)
                protectorText.text = gameManager.Protector.Color + " escaped! Round reset.";
        }
    }
}
