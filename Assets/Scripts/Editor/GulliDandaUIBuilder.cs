using LudoGame.MiniGames.GulliDanda;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LudoGame.EditorTools.LudoEditorUtility;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Builds a "glassmorphic-styled" UI for the Gulli Danda mini-game: a
    /// translucent, rounded, neon-outlined top bar (avatar/level, title,
    /// coins/gems), a glass-backed semi-circular power meter, a glass "swipe
    /// to strike" prompt, a bottom glass bar (distance + chance pips), and a
    /// Game Over panel - then wires them into GulliDandaUI.
    ///
    /// This uses only plain Unity UI (Unity's built-in rounded sprite +
    /// translucent color + an Outline glow) - there's no real background
    /// blur, since that needs a custom shader, but it reads as "glass" at a
    /// glance. Restyle colors/outlines freely afterwards; the wiring keeps
    /// working.
    ///
    /// How to use: put GulliDandaGameManager (with GulliController, PowerMeter
    /// and BhajuAI wired in) in the scene first, then
    /// Window > Ludo Tools > Mini-Games > Gulli Danda UI Builder.
    /// </summary>
    public static class GulliDandaUIBuilder
    {
        // A cohesive "frosted glass at golden hour" palette.
        private static readonly Color GlassTint = new Color(1f, 1f, 1f, 0.14f);
        private static readonly Color NeonCyan = new Color(0.3f, 0.9f, 1f, 0.9f);
        private static readonly Color NeonPurple = new Color(0.7f, 0.4f, 1f, 0.9f);
        private static readonly Color EmeraldGreen = new Color(0.2f, 0.85f, 0.45f);
        private static readonly Color ElectricYellow = new Color(0.95f, 0.85f, 0.15f);
        private static readonly Color CoralRed = new Color(0.95f, 0.35f, 0.3f);

        [MenuItem("Window/Ludo Tools/Mini-Games/Gulli Danda UI Builder")]
        internal static void Build()
        {
            GulliDandaGameManager gameManager = Object.FindAnyObjectByType<GulliDandaGameManager>();
            if (gameManager == null)
            {
                EditorUtility.DisplayDialog("Gulli Danda UI Builder",
                    "No GulliDandaGameManager found in the scene. Add one (with GulliController, " +
                    "PowerMeter and BhajuAI wired in) first, then run this tool again.", "OK");
                return;
            }

            PowerMeter powerMeter = gameManager.GetComponent<PowerMeter>();
            if (powerMeter == null) powerMeter = Object.FindAnyObjectByType<PowerMeter>();

            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            Text coinsText = BuildTopBar(canvas.transform);
            Image powerMeterFill = BuildPowerMeter(canvas.transform);
            GameObject swipePrompt = BuildSwipePrompt(canvas.transform);
            var (distanceText, chancePips) = BuildBottomBar(canvas.transform);
            var (gameOverPanel, gameOverText) = BuildGameOverPanel(canvas.transform);

            GulliDandaUI ui = canvas.gameObject.AddComponent<GulliDandaUI>();
            SetField(ui, "gameManager", gameManager);
            SetField(ui, "powerMeter", powerMeter);
            SetField(ui, "powerMeterFill", powerMeterFill);
            SetField(ui, "distanceText", distanceText);
            SetField(ui, "coinsText", coinsText);
            SetObjectArray(ui, "chancePips", chancePips);
            SetField(ui, "swipePrompt", swipePrompt);
            SetField(ui, "gameOverPanel", gameOverPanel);
            SetField(ui, "gameOverText", gameOverText);

            EditorUtility.DisplayDialog("Gulli Danda UI Builder",
                "Built a glass-styled top bar, power meter, swipe prompt, bottom bar, and Game Over panel, " +
                "and wired them into GulliDandaUI.\n\nNo real blur (needs a custom shader) - restyle colors freely.",
                "OK");
        }

        // ---------------- Top Bar: Avatar/Level | Title | Coins/Gems ----------------

        private static Text BuildTopBar(Transform canvasTransform)
        {
            Image bar = CreateGlassPanel(canvasTransform, "TopBar", GlassTint, NeonCyan);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 1f);
            barRect.anchorMax = new Vector2(1f, 1f);
            barRect.pivot = new Vector2(0.5f, 1f);
            barRect.anchoredPosition = new Vector2(0f, -20f);
            barRect.sizeDelta = new Vector2(-40f, 110f); // -40 = 20px margin on each side

            // Avatar (left)
            Image avatar = CreateGlassPanel(bar.transform, "Avatar", new Color(1f, 1f, 1f, 0.25f), NeonPurple);
            RectTransform avatarRect = avatar.rectTransform;
            avatarRect.anchorMin = new Vector2(0f, 0.5f);
            avatarRect.anchorMax = new Vector2(0f, 0.5f);
            avatarRect.pivot = new Vector2(0f, 0.5f);
            avatarRect.anchoredPosition = new Vector2(20f, 0f);
            avatarRect.sizeDelta = new Vector2(80f, 80f);

            Text levelText = CreateText(bar.transform, "LevelText", "Lvl 1", 20, TextAnchor.MiddleCenter);
            RectTransform levelRect = levelText.rectTransform;
            levelRect.anchorMin = new Vector2(0f, 0f);
            levelRect.anchorMax = new Vector2(0f, 0f);
            levelRect.pivot = new Vector2(0f, 0f);
            levelRect.anchoredPosition = new Vector2(20f, 8f);
            levelRect.sizeDelta = new Vector2(80f, 26f);

            // Title (center)
            Text title = CreateText(bar.transform, "TitleText", "GULLI DANDA", 40, TextAnchor.MiddleCenter);
            title.fontStyle = FontStyle.Bold;
            RectTransform titleRect = title.rectTransform;
            titleRect.anchorMin = new Vector2(0.5f, 0.5f);
            titleRect.anchorMax = new Vector2(0.5f, 0.5f);
            titleRect.pivot = new Vector2(0.5f, 0.5f);
            titleRect.anchoredPosition = Vector2.zero;
            titleRect.sizeDelta = new Vector2(500f, 60f);

            // Wallet pills (right): Coins (live, driven by GulliDandaUI), Gems (decorative placeholder -
            // there's no Gems currency in the game logic yet, only coins from RewardCalculator).
            Text coinsText = BuildWalletPill(bar.transform, "CoinsPill", "0 Coins", new Vector2(-20f, 15f));
            BuildWalletPill(bar.transform, "GemsPill", "0 Gems", new Vector2(-20f, -30f));

            return coinsText;
        }

        private static Text BuildWalletPill(Transform parent, string name, string label, Vector2 anchoredPosition)
        {
            Image pill = CreateGlassPanel(parent, name, new Color(1f, 1f, 1f, 0.2f), NeonCyan);
            RectTransform rt = pill.rectTransform;
            rt.anchorMin = new Vector2(1f, 0.5f);
            rt.anchorMax = new Vector2(1f, 0.5f);
            rt.pivot = new Vector2(1f, 0.5f);
            rt.anchoredPosition = anchoredPosition;
            rt.sizeDelta = new Vector2(160f, 36f);

            Text text = CreateText(pill.transform, "Label", label, 20, TextAnchor.MiddleCenter);
            StretchToParent(text.rectTransform);
            return text;
        }

        // ---------------- Power Meter (center-right, glass-backed, 3 color zones) ----------------

        private static Image BuildPowerMeter(Transform canvasTransform)
        {
            Image backing = CreateGlassPanel(canvasTransform, "PowerMeterGlass", GlassTint, NeonPurple);
            RectTransform panelRect = backing.rectTransform;
            panelRect.anchorMin = new Vector2(0.72f, 0.42f);
            panelRect.anchorMax = new Vector2(0.72f, 0.42f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(280f, 150f);

            // Static background arc showing all 3 zones at once (Coral -> Yellow -> Emerald),
            // approximated with 3 stacked radial wedges since a single Image can't multi-color a fill.
            CreateZoneWedge(backing.transform, "ZoneMiss", CoralRed, 1f);
            CreateZoneWedge(backing.transform, "ZoneGood", ElectricYellow, 0.7f);
            CreateZoneWedge(backing.transform, "ZonePerfect", EmeraldGreen, 0.4f);

            // Live indicator - GulliDandaUI drives this Image's fillAmount/color each frame.
            Image fill = CreateRadialImage(backing.transform, "PowerFill", Color.white, Image.FillMethod.Radial180, (int)Image.Origin180.Bottom);
            fill.fillAmount = 0f;
            StretchToParent(fill.rectTransform, inset: 12f);

            return fill;
        }

        private static void CreateZoneWedge(Transform parent, string name, Color color, float fillAmount)
        {
            Image wedge = CreateRadialImage(parent, name, color, Image.FillMethod.Radial180, (int)Image.Origin180.Bottom);
            wedge.fillAmount = fillAmount;
            StretchToParent(wedge.rectTransform, inset: 12f);
        }

        // ---------------- Swipe Prompt (oval glass) ----------------

        private static GameObject BuildSwipePrompt(Transform canvasTransform)
        {
            Image oval = CreateGlassPanel(canvasTransform, "SwipePromptGlass", GlassTint, NeonCyan);
            PositionRow(oval.rectTransform, 0.32f, 420f, 70f);
            oval.gameObject.SetActive(false); // GulliDandaUI turns this on only while charging

            Text text = CreateText(oval.transform, "SwipePromptText", "SWIPE TO STRIKE!", 30, TextAnchor.MiddleCenter);
            StretchToParent(text.rectTransform);

            return oval.gameObject;
        }

        // ---------------- Bottom Bar: Distance + Chance Pips ----------------

        private static (Text distance, Image[] pips) BuildBottomBar(Transform canvasTransform)
        {
            Image bar = CreateGlassPanel(canvasTransform, "BottomBar", GlassTint, NeonPurple);
            RectTransform barRect = bar.rectTransform;
            barRect.anchorMin = new Vector2(0f, 0f);
            barRect.anchorMax = new Vector2(1f, 0f);
            barRect.pivot = new Vector2(0.5f, 0f);
            barRect.anchoredPosition = new Vector2(0f, 20f);
            barRect.sizeDelta = new Vector2(-40f, 90f);

            Text distanceText = CreateText(bar.transform, "DistanceText", "Distance Cover: 0.0 M", 28, TextAnchor.MiddleLeft);
            distanceText.fontStyle = FontStyle.Bold;
            RectTransform distanceRect = distanceText.rectTransform;
            distanceRect.anchorMin = new Vector2(0f, 0.5f);
            distanceRect.anchorMax = new Vector2(0f, 0.5f);
            distanceRect.pivot = new Vector2(0f, 0.5f);
            distanceRect.anchoredPosition = new Vector2(24f, 0f);
            distanceRect.sizeDelta = new Vector2(420f, 50f);

            const int pipCount = 3;
            Image[] pips = new Image[pipCount];
            float pipWidth = 14f, pipHeight = 46f, pipSpacing = 12f;

            for (int i = 0; i < pipCount; i++)
            {
                GameObject pipGO = new GameObject("ChancePip_" + i, typeof(RectTransform));
                Undo.RegisterCreatedObjectUndo(pipGO, "Gulli Danda UI Builder");
                pipGO.transform.SetParent(bar.transform, false);

                Image pip = pipGO.AddComponent<Image>();
                pip.sprite = RoundedSprite;
                pip.type = Image.Type.Sliced;
                pip.color = new Color(0.85f, 0.55f, 0.2f); // wooden-stick orange, GulliDandaUI updates this live

                RectTransform pipRect = pipGO.GetComponent<RectTransform>();
                pipRect.anchorMin = new Vector2(1f, 0.5f);
                pipRect.anchorMax = new Vector2(1f, 0.5f);
                pipRect.pivot = new Vector2(1f, 0.5f);
                float xOffset = -24f - i * (pipWidth + pipSpacing);
                pipRect.anchoredPosition = new Vector2(xOffset, 0f);
                pipRect.sizeDelta = new Vector2(pipWidth, pipHeight);

                pips[i] = pip;
            }

            return (distanceText, pips);
        }

        // ---------------- Game Over ----------------

        private static (GameObject panel, Text text) BuildGameOverPanel(Transform canvasTransform)
        {
            Image panel = CreateGlassPanel(canvasTransform, "GulliDandaGameOverPanel", new Color(0f, 0f, 0f, 0.6f), NeonCyan);
            StretchToParent(panel.rectTransform, inset: 0f, fullScreenMargin: true);
            panel.gameObject.SetActive(false);

            Text text = CreateText(panel.transform, "GameOverText", "Game Over!", 44, TextAnchor.MiddleCenter);
            PositionRow(text.rectTransform, 0.5f, 700f, 120f);

            return (panel.gameObject, text);
        }

        // ---------------- Layout helpers ----------------

        private static void StretchToParent(RectTransform rt, float inset = 0f, bool fullScreenMargin = false)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            float margin = fullScreenMargin ? 0f : inset;
            rt.offsetMin = new Vector2(margin, margin);
            rt.offsetMax = new Vector2(-margin, -margin);
        }
    }
}
