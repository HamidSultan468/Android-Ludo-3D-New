using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet.UI; // WalletUiKit - internal, same assembly (no asmdef in this project)

namespace TangentLudoEmpire.Polish
{
    /// <summary>
    /// Task C.1. SPEC CORRECTION: "LoadingScreenManager.cs : Canvas" - built as a self-building
    /// MonoBehaviour via WalletUiKit, same correction already applied to every other "X : Canvas" ask in
    /// this project (TournamentUI, DepositPopup, etc.), not a literal Canvas subclass.
    ///
    /// Room/Tournament joins in this project's mock backend resolve synchronously (no real network round
    /// trip), so there is no actual load time to fill a progress bar with - <see cref="ShowForSeconds"/>
    /// gives a UI caller an honest, explicitly-timed way to show this screen anyway (e.g. while
    /// RoomManager/TournamentManager registration runs) without this session inventing a fake async delay
    /// inside those managers themselves. A real backend integration would instead call
    /// <see cref="Show"/>/<see cref="SetProgress"/>/<see cref="Hide"/> around its actual network call.
    /// </summary>
    [DisallowMultipleComponent]
    public class LoadingScreenManager : MonoBehaviour
    {
        public static LoadingScreenManager Instance { get; private set; }

        private static readonly string[] Tips =
        {
            "Tip: Roll a 6 to move a token out of your base.",
            "Tip: Capturing an opponent's token sends it straight back to their base.",
            "Tip: Star squares are safe - your token can't be captured while standing on one.",
            "Tip: You need the exact roll to bring a token all the way home.",
            "Tip: Three sixes in a row forfeits your turn - roll carefully!",
            "Tip: Two tokens of the same color on one square form a block - opponents can't pass.",
        };

        private GameObject _root;
        private Text _tipLabel;
        private RectTransform _fillRect;
        private float _trackWidth;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code would otherwise hit InvalidOperationException here and abort the rest of Awake()
            BuildUI();
        }

        /// <summary>Shows the screen with a random tip and progress reset to 0.</summary>
        public void Show()
        {
            if (_root == null) BuildUI();
            _tipLabel.text = Tips[UnityEngine.Random.Range(0, Tips.Length)];
            SetProgress(0f);
            _root.SetActive(true);
        }

        /// <summary>0..1 progress bar fill.</summary>
        public void SetProgress(float t01)
        {
            if (_fillRect == null) return;
            t01 = Mathf.Clamp01(t01);
            _fillRect.sizeDelta = new Vector2(_trackWidth * t01, _fillRect.sizeDelta.y);
        }

        public void Hide() { if (_root != null) _root.SetActive(false); }

        /// <summary>Convenience for callers with no real async load to time the screen against - see class
        /// remarks. Not a coroutine-blocking call; fires <paramref name="onComplete"/> when done.</summary>
        public void ShowForSeconds(float seconds, Action onComplete = null)
        {
            Show();
            StartCoroutine(TimedRoutine(Mathf.Max(0.05f, seconds), onComplete));
        }

        private IEnumerator TimedRoutine(float seconds, Action onComplete)
        {
            float t = 0f;
            while (t < seconds)
            {
                t += Time.unscaledDeltaTime;
                SetProgress(t / seconds);
                yield return null;
            }
            SetProgress(1f);
            Hide();
            onComplete?.Invoke();
        }

        private void BuildUI()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _root = WalletUiKit.Container(canvas.transform, "LoadingScreen", new Vector2(1080, 1920), Vector2.zero,
                                          new Color(0.05f, 0.05f, 0.08f, 0.98f));
            WalletUiKit.Label(_root.transform, "LOADING...", 44, new Vector2(900, 60), new Vector2(0, 220));
            _tipLabel = WalletUiKit.Label(_root.transform, "", 26, new Vector2(860, 140), new Vector2(0, 60));

            _trackWidth = 700f;
            var track = WalletUiKit.Container(_root.transform, "ProgressTrack", new Vector2(_trackWidth, 26),
                                              new Vector2(0, -80), new Color(1f, 1f, 1f, 0.15f));
            var fillGo = WalletUiKit.Container(track.transform, "ProgressFill", new Vector2(0, 26), Vector2.zero, WalletUiKit.Accent);
            _fillRect = fillGo.GetComponent<RectTransform>();
            _fillRect.pivot = new Vector2(0f, 0.5f);
            _fillRect.anchoredPosition = new Vector2(-_trackWidth / 2f, 0f); // left edge of the track - width then grows rightward

            _root.SetActive(false);
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build.</summary>
        public bool Editor_IsVisible => _root != null && _root.activeSelf;
        public float Editor_ProgressFraction => _trackWidth > 0f && _fillRect != null ? _fillRect.sizeDelta.x / _trackWidth : 0f;
#endif
    }
}
