using LudoGame.Audio;
using LudoGame.Dice;
using LudoGame.Game;
using LudoGame.Save;
using LudoGame.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.UI;
using static LudoGame.EditorTools.LudoEditorUtility;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Builds a basic but fully functional gameplay UI: a turn indicator, a
    /// Roll button + dice value text, a Game Over panel, and a Settings
    /// panel - then wires them straight into DiceUI/TurnIndicatorUI/
    /// GameOverUI/SettingsUI and your manager objects.
    ///
    /// The look is deliberately plain (default Unity colors, no sprites) -
    /// restyle colors/fonts/backgrounds freely afterwards in the Inspector,
    /// the script wiring keeps working as long as you don't delete these
    /// objects' components.
    ///
    /// How to use: run "1. Scene Bootstrapper" first (so DiceManager,
    /// GameManager, AudioManager, SaveManager exist), then Window > Ludo
    /// Tools > 4. Basic UI Canvas Builder.
    /// </summary>
    public static class UICanvasBuilder
    {
        [MenuItem("Window/Ludo Tools/4. Basic UI Canvas Builder")]
        private static void Build()
        {
            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            GameObject turnText = BuildTurnIndicator(canvas.transform);
            var (rollButton, diceValueText) = BuildDicePanel(canvas.transform);
            var (gameOverPanel, winnerText, restartButton, mainMenuButton) = BuildGameOverPanel(canvas.transform);
            var (settingsPanel, sfxSlider, musicSlider, sfxToggle, musicToggle) = BuildSettingsPanel(canvas.transform);
            BuildSettingsOpenButton(canvas.transform, settingsPanel);

            WireScripts(turnText, rollButton, diceValueText, gameOverPanel, winnerText, restartButton, mainMenuButton,
                settingsPanel, sfxSlider, musicSlider, sfxToggle, musicToggle);

            EditorUtility.DisplayDialog("UI Canvas Builder",
                "Basic UI created: turn indicator, Roll button + dice text, Game Over panel (Restart + Main Menu), Settings panel.\n\n" +
                "It's deliberately plain - restyle colors/fonts/sprites freely, the wiring will keep working.",
                "OK");
        }

        // ---------------- Panels ----------------

        private static GameObject BuildTurnIndicator(Transform canvasTransform)
        {
            Text text = CreateText(canvasTransform, "TurnIndicatorText", "Red's Turn", 42, TextAnchor.MiddleCenter);
            RectTransform rt = text.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 1f);
            rt.anchorMax = new Vector2(0.5f, 1f);
            rt.pivot = new Vector2(0.5f, 1f);
            rt.anchoredPosition = new Vector2(0f, -60f);
            rt.sizeDelta = new Vector2(600f, 80f);
            return text.gameObject;
        }

        private static (GameObject rollButton, GameObject diceValueText) BuildDicePanel(Transform canvasTransform)
        {
            GameObject panel = new GameObject("DicePanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panel, "UI Canvas Builder");
            panel.transform.SetParent(canvasTransform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0f);
            panelRect.anchorMax = new Vector2(0.5f, 0f);
            panelRect.pivot = new Vector2(0.5f, 0f);
            panelRect.anchoredPosition = new Vector2(0f, 80f);
            panelRect.sizeDelta = new Vector2(320f, 160f);

            Button rollButton = CreateButton(panel.transform, "RollButton", "Roll");
            RectTransform rollRect = rollButton.GetComponent<RectTransform>();
            rollRect.anchorMin = new Vector2(0f, 0f);
            rollRect.anchorMax = new Vector2(1f, 0.6f);
            rollRect.offsetMin = Vector2.zero;
            rollRect.offsetMax = Vector2.zero;

            Text valueText = CreateText(panel.transform, "DiceValueText", "-", 36, TextAnchor.MiddleCenter);
            RectTransform valueRect = valueText.rectTransform;
            valueRect.anchorMin = new Vector2(0f, 0.65f);
            valueRect.anchorMax = new Vector2(1f, 1f);
            valueRect.offsetMin = Vector2.zero;
            valueRect.offsetMax = Vector2.zero;

            return (rollButton.gameObject, valueText.gameObject);
        }

        private static (GameObject panel, GameObject winnerText, GameObject restartButton, GameObject mainMenuButton) BuildGameOverPanel(Transform canvasTransform)
        {
            GameObject panel = CreatePanel(canvasTransform, "GameOverPanel", new Color(0f, 0f, 0f, 0.75f));
            panel.SetActive(false);

            Text winnerText = CreateText(panel.transform, "WinnerText", "Wins!", 56, TextAnchor.MiddleCenter);
            PositionRow(winnerText.rectTransform, 0.6f, 700f, 100f);

            Button restartButton = CreateButton(panel.transform, "RestartButton", "Restart");
            PositionRow(restartButton.GetComponent<RectTransform>(), 0.45f, 240f, 70f);

            Button mainMenuButton = CreateButton(panel.transform, "MainMenuButton", "Main Menu");
            PositionRow(mainMenuButton.GetComponent<RectTransform>(), 0.32f, 240f, 70f);

            return (panel, winnerText.gameObject, restartButton.gameObject, mainMenuButton.gameObject);
        }

        private static (GameObject panel, Slider sfxSlider, Slider musicSlider, Toggle sfxToggle, Toggle musicToggle) BuildSettingsPanel(Transform canvasTransform)
        {
            GameObject panel = CreatePanel(canvasTransform, "SettingsPanel", new Color(0f, 0f, 0f, 0.85f));
            panel.SetActive(false);

            Text title = CreateText(panel.transform, "Title", "Settings", 40, TextAnchor.MiddleCenter);
            PositionRow(title.rectTransform, 0.8f, 500f, 80f);

            Text sfxLabel = CreateText(panel.transform, "SfxLabel", "SFX Volume", 26, TextAnchor.MiddleLeft);
            PositionRow(sfxLabel.rectTransform, 0.62f, 420f, 44f);
            Slider sfxSlider = CreateSlider(panel.transform, "SfxVolumeSlider");
            PositionRow(sfxSlider.GetComponent<RectTransform>(), 0.55f, 420f, 40f);

            Text musicLabel = CreateText(panel.transform, "MusicLabel", "Music Volume", 26, TextAnchor.MiddleLeft);
            PositionRow(musicLabel.rectTransform, 0.45f, 420f, 44f);
            Slider musicSlider = CreateSlider(panel.transform, "MusicVolumeSlider");
            PositionRow(musicSlider.GetComponent<RectTransform>(), 0.38f, 420f, 40f);

            Toggle sfxToggle = CreateToggle(panel.transform, "SfxMuteToggle", "Mute SFX");
            PositionRow(sfxToggle.GetComponent<RectTransform>(), 0.28f, 40f, 40f);

            Toggle musicToggle = CreateToggle(panel.transform, "MusicMuteToggle", "Mute Music");
            PositionRow(musicToggle.GetComponent<RectTransform>(), 0.2f, 40f, 40f);

            Button closeButton = CreateButton(panel.transform, "CloseButton", "Close");
            PositionRow(closeButton.GetComponent<RectTransform>(), 0.08f, 220f, 60f);
            // AddListener() only registers a runtime-only callback that is lost the moment
            // this scene is saved and reopened. AddBoolPersistentListener bakes the call
            // (and its "false" argument) into the serialized scene, same as wiring it by
            // hand in the Inspector, so the Close button keeps working after a restart.
            UnityEventTools.AddBoolPersistentListener(closeButton.onClick, panel.SetActive, false);

            return (panel, sfxSlider, musicSlider, sfxToggle, musicToggle);
        }

        private static Button BuildSettingsOpenButton(Transform canvasTransform, GameObject settingsPanel)
        {
            Button button = CreateButton(canvasTransform, "OpenSettingsButton", "Settings");
            RectTransform rt = button.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.anchoredPosition = new Vector2(-20f, -20f);
            rt.sizeDelta = new Vector2(160f, 60f);
            UnityEventTools.AddBoolPersistentListener(button.onClick, settingsPanel.SetActive, true);
            return button;
        }

        // ---------------- Wiring ----------------

        private static void WireScripts(GameObject turnTextGO, GameObject rollButtonGO, GameObject diceValueTextGO,
            GameObject gameOverPanel, GameObject winnerTextGO, GameObject restartButtonGO, GameObject mainMenuButtonGO,
            GameObject settingsPanel, Slider sfxSlider, Slider musicSlider, Toggle sfxToggle, Toggle musicToggle)
        {
            DiceManager diceManager = Object.FindAnyObjectByType<DiceManager>();
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            AudioManager audioManager = Object.FindAnyObjectByType<AudioManager>();
            SaveManager saveManager = Object.FindAnyObjectByType<SaveManager>();

            GameObject dicePanel = rollButtonGO.transform.parent.gameObject;
            DiceUI diceUI = dicePanel.AddComponent<DiceUI>();
            SetField(diceUI, "diceManager", diceManager);
            SetField(diceUI, "gameManager", gameManager);
            SetField(diceUI, "rollButton", rollButtonGO.GetComponent<Button>());
            SetField(diceUI, "valueText", diceValueTextGO.GetComponent<Text>());

            TurnIndicatorUI turnUI = turnTextGO.AddComponent<TurnIndicatorUI>();
            SetField(turnUI, "gameManager", gameManager);
            SetField(turnUI, "turnText", turnTextGO.GetComponent<Text>());

            GameOverUI gameOverUI = gameOverPanel.AddComponent<GameOverUI>();
            SetField(gameOverUI, "gameManager", gameManager);
            SetField(gameOverUI, "gameOverPanel", gameOverPanel);
            SetField(gameOverUI, "winnerText", winnerTextGO.GetComponent<Text>());
            SetField(gameOverUI, "restartButton", restartButtonGO.GetComponent<Button>());
            SetField(gameOverUI, "mainMenuButton", mainMenuButtonGO.GetComponent<Button>());

            SettingsUI settingsUI = settingsPanel.AddComponent<SettingsUI>();
            SetField(settingsUI, "saveManager", saveManager);
            SetField(settingsUI, "audioManager", audioManager);
            SetField(settingsUI, "sfxVolumeSlider", sfxSlider);
            SetField(settingsUI, "musicVolumeSlider", musicSlider);
            SetField(settingsUI, "sfxMuteToggle", sfxToggle);
            SetField(settingsUI, "musicMuteToggle", musicToggle);
        }
    }
}
