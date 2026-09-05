using UnityEngine;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>
    /// Feeds a VirtualJoystick's direction into whichever player is
    /// currently the Protector - so control automatically follows the
    /// "Killa Bandar" role as it swaps between players, instead of being
    /// locked to one fixed character.
    ///
    /// This defaults to controlling the Protector because that's where the
    /// new mechanics (rope, tagging, Milestone Sprint) live. Attackers are
    /// not driven by anything yet - either add more joysticks for local
    /// multiplayer, or simple AI wandering/steal logic, as a follow-up.
    ///
    /// Setup: put this on an empty GameObject, drag in the
    /// KillaBandarGameManager and the on-screen VirtualJoystick.
    /// </summary>
    public class KillaBandarInputController : MonoBehaviour
    {
        [SerializeField] private KillaBandarGameManager gameManager;
        [SerializeField] private VirtualJoystick joystick;

        private void Update()
        {
            if (gameManager == null || joystick == null) return;
            gameManager.Protector?.Move(joystick.Direction);
        }
    }
}
