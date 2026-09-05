using LudoGame.Board;
using UnityEngine;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>Which side a participant is currently playing.</summary>
    public enum KillaBandarRole { Attacker, Protector }

    /// <summary>
    /// One participant in Killa Bandar - either the Protector ("Killa
    /// Bandar") or an Attacker. Simple Transform-based movement (no
    /// Rigidbody needed) driven by Move(), so a virtual joystick UI or an
    /// AI controller can feed it input the same way.
    ///
    /// Setup: put this on each player avatar. "Color" reuses the same
    /// GridManager.PlayerColor as the main Ludo game, so a Killa Bandar
    /// session can map 1:1 onto the current Ludo match's 4 players.
    /// </summary>
    public class KillaBandarPlayer : MonoBehaviour
    {
        [SerializeField] private GridManager.PlayerColor color;
        [SerializeField] private float moveSpeed = 5f;

        public GridManager.PlayerColor Color => color;
        public KillaBandarRole Role { get; private set; } = KillaBandarRole.Attacker;

        /// <summary>True once this attacker has been tagged this session (cleared on AssignRoles).</summary>
        public bool IsTagged { get; private set; }

        public void SetRole(KillaBandarRole role)
        {
            Role = role;
            IsTagged = false;
        }

        public void MarkTagged() => IsTagged = true;

        /// <summary>Moves this player on the XZ plane and faces the movement direction. "direction" need not be normalized.</summary>
        public void Move(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f) return;

            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.y).normalized;
            transform.position += flatDirection * (moveSpeed * Time.deltaTime);
            transform.forward = flatDirection;
        }
    }
}
