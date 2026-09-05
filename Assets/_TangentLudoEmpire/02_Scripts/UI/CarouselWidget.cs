using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace TangentLudoEmpire.Core
{
    /// <summary>Dead-simple auto-advancing text carousel for the Dashboard's "Top Players" strip.
    /// Phase 1 placeholder - swap the string list for real leaderboard data later.</summary>
    [RequireComponent(typeof(Text))]
    public class CarouselWidget : MonoBehaviour
    {
        [SerializeField] private string[] slides = { "Top Player 1", "Top Player 2", "Top Player 3" };
        [SerializeField] private float secondsPerSlide = 2.5f;
        [SerializeField] private float fadeSeconds = 0.25f;

        private Text _text;
        private CanvasGroup _group;
        private int _index;

        private void Awake()
        {
            _text = GetComponent<Text>();
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
        }

        private void OnEnable() => StartCoroutine(Run());

        public void SetSlides(string[] items)
        {
            if (items != null && items.Length > 0) slides = items;
            _index = 0;
        }

        private IEnumerator Run()
        {
            if (slides == null || slides.Length == 0) yield break;
            _text.text = slides[0];
            _group.alpha = 1f;

            var hold = new WaitForSecondsRealtime(secondsPerSlide);
            while (true)
            {
                yield return hold;
                yield return Fade(1f, 0f);
                _index = (_index + 1) % slides.Length;
                _text.text = slides[_index];
                yield return Fade(0f, 1f);
            }
        }

        private IEnumerator Fade(float a, float b)
        {
            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.unscaledDeltaTime;
                _group.alpha = Mathf.Lerp(a, b, t / fadeSeconds);
                yield return null;
            }
            _group.alpha = b;
        }
    }
}
