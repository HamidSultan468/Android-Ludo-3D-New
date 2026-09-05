using UnityEngine;
using UnityEngine.EventSystems;

namespace LudoGame.MiniGames.KillaBandar
{
    /// <summary>
    /// A draggable on-screen joystick: drag the handle within the
    /// background circle to get a direction (-1..1 on both axes). Uses
    /// Unity's UI event interfaces (drag/pointer), which work automatically
    /// with the new Input System through whichever EventSystem input module
    /// is active - no extra wiring needed beyond having an EventSystem in
    /// the scene (the Ludo Tools already ensure one exists).
    ///
    /// Setup: put this on the joystick's background Image (sized to the
    /// touch area you want). Assign a draggable "handle" Image as its
    /// child. Read Direction from whatever moves the player, e.g.
    /// KillaBandarInputController calls player.Move(joystick.Direction)
    /// every frame.
    /// </summary>
    public class VirtualJoystick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private RectTransform background;
        [SerializeField] private RectTransform handle;
        [Tooltip("How far the handle can move from center, in pixels.")]
        [SerializeField] private float handleRange = 60f;

        /// <summary>Current input direction, both axes in -1..1. Zero while not being touched.</summary>
        public Vector2 Direction { get; private set; }

        public void OnPointerDown(PointerEventData eventData) => OnDrag(eventData);

        public void OnDrag(PointerEventData eventData)
        {
            if (background == null) return;

            RectTransformUtility.ScreenPointToLocalPointInRectangle(
                background, eventData.position, eventData.pressEventCamera, out Vector2 localPoint);

            Vector2 clamped = Vector2.ClampMagnitude(localPoint, handleRange);
            Direction = handleRange > 0f ? clamped / handleRange : Vector2.zero;

            if (handle != null) handle.anchoredPosition = clamped;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            Direction = Vector2.zero;
            if (handle != null) handle.anchoredPosition = Vector2.zero;
        }
    }
}
