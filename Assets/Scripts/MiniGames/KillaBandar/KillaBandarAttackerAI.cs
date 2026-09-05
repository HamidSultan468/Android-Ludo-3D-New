using UnityEngine;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>
    /// Simple attacker AI: while this player is an Attacker, wanders toward
    /// the Killa stake to attempt steals, and backs away if the Protector
    /// gets close (to avoid being tagged). Does nothing while this player
    /// is the Protector - that role is joystick/human-controlled instead
    /// (see KillaBandarInputController).
    ///
    /// Setup: put this on each player alongside KillaBandarPlayer. Assign
    /// the KillaBandarGameManager (to find the current Protector) and the
    /// Killa stake Transform.
    /// </summary>
    public class KillaBandarAttackerAI : MonoBehaviour
    {
        [SerializeField] private KillaBandarPlayer player;
        [SerializeField] private KillaBandarGameManager gameManager;
        [SerializeField] private Transform killaStake;

        [Tooltip("If the Protector is closer than this, retreat instead of advancing.")]
        [SerializeField] private float dangerRadius = 2f;

        private void Update()
        {
            if (player == null || gameManager == null || killaStake == null) return;
            if (player.Role != KillaBandarRole.Attacker || player.IsTagged) return;
            if (gameManager.Protector == null) return;

            Vector3 toProtector = gameManager.Protector.transform.position - transform.position;
            Vector3 toStake = killaStake.position - transform.position;

            Vector2 direction = toProtector.magnitude < dangerRadius
                ? new Vector2(-toProtector.x, -toProtector.z)  // too close to the Protector - retreat
                : new Vector2(toStake.x, toStake.z);            // safe distance - head for the shoes

            player.Move(direction.normalized);
        }
    }
}
