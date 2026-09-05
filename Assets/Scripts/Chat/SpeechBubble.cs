using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace LudoGame.Chat
{
    /// <summary>
    /// A speech bubble that floats above a world-space Transform (a player's
    /// token) and fades out after a few seconds. It lives on a normal Screen
    /// Space Overlay canvas and repositions itself every frame with
    /// Camera.WorldToScreenPoint - no separate World Space canvas needed.
    ///
    /// Setup: built automatically by ChatUIBuilder as a prefab-like template
    /// object (kept inactive under the Canvas, cloned with Instantiate each
    /// time a message is sent). If wiring by hand: put this + a CanvasGroup
    /// on a small UI panel (background Image + Text child) under the Canvas.
    /// </summary>
    [RequireComponent(typeof(CanvasGroup))]
    public class SpeechBubble : MonoBehaviour
    {
        [SerializeField] private Text messageText;
        [SerializeField] private RectTransform rectTransform;
        [Tooltip("Pixel offset from the target's screen position - pushes the bubble up above the token instead of centered on it.")]
        [SerializeField] private Vector2 screenOffset = new Vector2(0f, 90f);
        [SerializeField] private float fadeDuration = 0.35f;

        private CanvasGroup canvasGroup;
        private Transform worldTarget;
        private Camera worldCamera;
        private Coroutine lifecycleCoroutine;

        private void Awake()
        {
            canvasGroup = GetComponent<CanvasGroup>();
            if (rectTransform == null) rectTransform = transform as RectTransform;
        }

        /// <summary>Shows this bubble above "target" with the given text, fading out after "duration" seconds.</summary>
        public void Show(string text, Transform target, Camera camera, float duration)
        {
            worldTarget = target;
            worldCamera = camera != null ? camera : Camera.main;
            if (messageText != null) messageText.text = text;

            gameObject.SetActive(true);
            if (lifecycleCoroutine != null) StopCoroutine(lifecycleCoroutine);
            lifecycleCoroutine = StartCoroutine(Lifecycle(duration));
        }

        private void LateUpdate()
        {
            if (worldTarget == null || worldCamera == null || rectTransform == null) return;

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldTarget.position);
            if (screenPoint.z < 0f)
            {
                canvasGroup.alpha = 0f; // target is behind the camera - hide instead of showing at a wrong spot
                return;
            }

            rectTransform.position = (Vector2)screenPoint + screenOffset;
        }

        private IEnumerator Lifecycle(float duration)
        {
            yield return Fade(0f, 1f, fadeDuration);

            float holdTime = Mathf.Max(0f, duration - fadeDuration * 2f);
            yield return new WaitForSeconds(holdTime);

            yield return Fade(1f, 0f, fadeDuration);

            gameObject.SetActive(false);
            Destroy(gameObject);
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            duration = Mathf.Max(duration, 0.01f);
            float t = 0f;

            while (t < duration)
            {
                t += Time.deltaTime;
                canvasGroup.alpha = Mathf.Lerp(from, to, t / duration);
                yield return null;
            }

            canvasGroup.alpha = to;
        }
    }
}
