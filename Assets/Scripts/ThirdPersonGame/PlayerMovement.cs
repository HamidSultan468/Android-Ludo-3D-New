using UnityEngine;
using UnityEngine.InputSystem;

namespace LudoGame.ThirdPersonGame
{
    /// <summary>
    /// Moves the player forward/backward/left/right using keyboard (WASD or arrow keys),
    /// or a virtual joystick by calling SetJoystickInput(...) every frame from a UI script
    /// (see LudoGame.MiniGames.KillaBandar.VirtualJoystick for an existing joystick to reuse).
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this on your player character, which needs a Character Controller component
    ///    (Add Component > Character Controller) - it handles collision with walls/ground
    ///    for you, so the player never clips through geometry.
    /// 2. Tweak "Move Speed" / "Rotation Speed" / "Gravity" in the Inspector to taste.
    /// 3. For mobile: have your joystick script call SetJoystickInput(joystick.Direction)
    ///    every frame instead of (or alongside) the automatic keyboard input.
    /// 4. Other scripts (e.g. an Animator controller, a camera) can read IsMoving/MoveInput
    ///    to react to the player's current movement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMovement : MonoBehaviour
    {
        [Header("Movement Feel")]
        [SerializeField] private float moveSpeed = 5f;
        [SerializeField] private float rotationSpeed = 10f;
        [SerializeField] private float gravity = -9.81f;

        /// <summary>Current horizontal input: X = left(-1)/right(+1), Y = back(-1)/forward(+1).</summary>
        public Vector2 MoveInput { get; private set; }

        /// <summary>True while the player is actually moving - handy for driving Idle/Walking animations.</summary>
        public bool IsMoving => MoveInput.sqrMagnitude > 0.01f;

        private CharacterController controller;
        private Vector2 joystickInput;
        private float verticalVelocity;

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
        }

        private void Update()
        {
            // Joystick input (if any) takes priority over keyboard, so mobile and desktop
            // both work without needing separate scripts.
            MoveInput = joystickInput.sqrMagnitude > 0.01f ? joystickInput : ReadKeyboardInput();
            ApplyMovement();
        }

        private Vector2 ReadKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return Vector2.zero; // no keyboard connected (e.g. on a phone)

            Vector2 input = Vector2.zero;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;

            return Vector2.ClampMagnitude(input, 1f); // stops diagonal movement being faster
        }

        /// <summary>Call this every frame from a virtual joystick to drive movement instead of the keyboard. Pass Vector2.zero when not touched.</summary>
        public void SetJoystickInput(Vector2 input) => joystickInput = input;

        private void ApplyMovement()
        {
            Vector3 flatDirection = new Vector3(MoveInput.x, 0f, MoveInput.y);

            if (flatDirection.sqrMagnitude > 0.0001f)
            {
                flatDirection.Normalize();

                // Smoothly turn to face the movement direction instead of snapping instantly.
                Quaternion targetRotation = Quaternion.LookRotation(flatDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
            }

            // Simple gravity: keeps the character glued to slopes/ground instead of floating,
            // and lets it fall if it walks off a ledge.
            if (controller.isGrounded && verticalVelocity < 0f)
                verticalVelocity = -2f; // small downward push so isGrounded stays true on slopes
            else
                verticalVelocity += gravity * Time.deltaTime;

            Vector3 motion = flatDirection * moveSpeed + Vector3.up * verticalVelocity;
            controller.Move(motion * Time.deltaTime);
        }
    }
}
