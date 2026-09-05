using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Runtime companion dropped in by the scene builder: drives the 4 theme-selector buttons -
    /// including their locked/cost display and a purchase-failure toast - and keeps their highlight
    /// synced to whichever theme is actually active. Binds to <see cref="LudoThemeManager.Instance"/>
    /// lazily so it doesn't matter whether "Build 3D Ludo Scene" or "Setup Theme Manager" was run first.
    /// </summary>
    public class LudoThemeSelectorUI : MonoBehaviour
    {
        [Serializable]
        public class ThemeButton
        {
            public LudoThemeId themeId;
            public Button button;
            public Image background;
            public Text label;
            [Tooltip("Padlock glyph shown while this theme is locked; hidden once it's unlocked.")]
            public GameObject lockIcon;
        }

        [SerializeField] private List<ThemeButton> buttons = new List<ThemeButton>();
        [SerializeField] private Color selectedColor = new Color(0.95f, 0.85f, 0.25f, 0.95f);
        [SerializeField] private Color unselectedColor = new Color(0.15f, 0.15f, 0.18f, 0.85f);
        [SerializeField] private Color lockedColor = new Color(0.08f, 0.08f, 0.1f, 0.85f);

        [Header("Purchase Toast")]
        [SerializeField] private GameObject toastRoot;
        [SerializeField] private Text toastText;
        [SerializeField] private float toastDuration = 1.6f;

        private LudoThemeManager _manager;
        private Coroutine _toastRoutine;

        public void Configure(List<ThemeButton> themeButtons, GameObject toast = null, Text toastLabel = null)
        {
            buttons = themeButtons ?? new List<ThemeButton>();
            toastRoot = toast;
            toastText = toastLabel;
            if (toastRoot != null) toastRoot.SetActive(false);
        }

        private void OnEnable() => TryBindManager();

        private void OnDisable()
        {
            if (_manager == null) return;
            _manager.OnThemeChanged -= HandleThemeChanged;
            _manager.OnThemeUnlocked -= HandleThemeUnlocked;
            _manager.OnThemePurchaseFailed -= HandlePurchaseFailed;
        }

        private bool TryBindManager()
        {
            if (_manager != null) return true;

            _manager = LudoThemeManager.Instance;
            if (_manager == null) return false;

            _manager.OnThemeChanged += HandleThemeChanged;
            _manager.OnThemeUnlocked += HandleThemeUnlocked;
            _manager.OnThemePurchaseFailed += HandlePurchaseFailed;

            RefreshAllButtons();
            if (_manager.CurrentTheme != null) HandleThemeChanged(_manager.CurrentTheme);
            return true;
        }

        public void SelectGlass() => Select(LudoThemeId.Glass);
        public void SelectStone() => Select(LudoThemeId.Stone);
        public void SelectJungle() => Select(LudoThemeId.Jungle);
        public void SelectCyberpunk() => Select(LudoThemeId.Cyberpunk);

        /// <summary>Wired to each theme button's onClick. Unlocked themes apply immediately; locked
        /// themes are purchased first (see <see cref="LudoThemeManager.SelectTheme"/>) - insufficient
        /// funds surfaces as a toast via <see cref="HandlePurchaseFailed"/> instead of switching.</summary>
        public void Select(LudoThemeId id)
        {
            if (!TryBindManager())
            {
                Debug.LogWarning("[LudoThemeSelectorUI] No LudoThemeManager found in the scene yet. " +
                                  "Run 'Ludo Tools/Setup Theme Manager' first.", this);
                return;
            }
            _manager.SelectTheme(id);
        }

        private void HandleThemeChanged(LudoThemeData theme)
        {
            if (theme == null || buttons == null) return;
            foreach (var entry in buttons)
            {
                if (entry?.background == null) continue;
                bool unlocked = _manager == null || _manager.IsThemeUnlocked(entry.themeId);
                entry.background.color = !unlocked ? lockedColor
                    : entry.themeId == theme.themeId ? selectedColor
                    : unselectedColor;
            }
        }

        private void HandleThemeUnlocked(LudoThemeId id)
        {
            ThemeButton entry = buttons?.Find(b => b != null && b.themeId == id);
            if (entry != null) RefreshButton(entry);
        }

        private void HandlePurchaseFailed(LudoThemeId id, string reason)
        {
            ShowToast(string.IsNullOrEmpty(reason) ? "Purchase failed." : reason);
        }

        private void RefreshAllButtons()
        {
            if (buttons == null) return;
            foreach (var entry in buttons) RefreshButton(entry);
        }

        private void RefreshButton(ThemeButton entry)
        {
            if (entry == null || _manager == null) return;

            LudoThemeData data = _manager.GetThemeData(entry.themeId);
            bool unlocked = _manager.IsThemeUnlocked(entry.themeId);
            string displayName = data != null && !string.IsNullOrEmpty(data.displayName) ? data.displayName : entry.themeId.ToString();

            if (entry.label != null)
            {
                entry.label.text = unlocked ? displayName : $"{displayName}\n({_manager.GetThemeCost(entry.themeId)} Coins)";
            }

            if (entry.lockIcon != null) entry.lockIcon.SetActive(!unlocked);

            if (entry.background != null && (_manager.CurrentTheme == null || entry.themeId != _manager.CurrentTheme.themeId))
            {
                entry.background.color = unlocked ? unselectedColor : lockedColor;
            }
        }

        private void ShowToast(string message)
        {
            if (toastRoot == null || toastText == null)
            {
                Debug.Log($"[LudoThemeSelectorUI] {message}");
                return;
            }

            if (_toastRoutine != null) StopCoroutine(_toastRoutine);
            _toastRoutine = StartCoroutine(ToastRoutine(message));
        }

        private System.Collections.IEnumerator ToastRoutine(string message)
        {
            toastText.text = message;
            toastRoot.SetActive(true);
            yield return new WaitForSeconds(toastDuration);
            toastRoot.SetActive(false);
            _toastRoutine = null;
        }
    }
}
