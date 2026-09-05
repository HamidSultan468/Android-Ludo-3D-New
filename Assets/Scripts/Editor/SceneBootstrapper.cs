using LudoGame.Audio;
using LudoGame.Dice;
using LudoGame.Game;
using LudoGame.Board;
using LudoGame.Player;
using LudoGame.Save;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// One-click setup for the game's core "manager" GameObjects: GridManager,
    /// DiceManager, GameManager, AudioManager, SaveManager, TokenSelector.
    /// Safe to run more than once - it reuses any managers that already exist
    /// instead of duplicating them, and simply re-links their references.
    ///
    /// How to use: Window > Ludo Tools > 1. Scene Bootstrapper. Run this
    /// FIRST, before the other Ludo Tools.
    /// </summary>
    public static class SceneBootstrapper
    {
        [MenuItem("Window/Ludo Tools/1. Scene Bootstrapper")]
        private static void Run()
        {
            GridManager gridManager = GetOrCreate<GridManager>("GridManager");
            DiceManager diceManager = GetOrCreate<DiceManager>("DiceManager");
            GameManager gameManager = GetOrCreate<GameManager>("GameManager");
            AudioManager audioManager = GetOrCreate<AudioManager>("AudioManager");
            SaveManager saveManager = GetOrCreate<SaveManager>("SaveManager");
            TokenSelector tokenSelector = GetOrCreate<TokenSelector>("TokenSelector");

            LudoEditorUtility.SetField(gameManager, "diceManager", diceManager);
            LudoEditorUtility.SetField(tokenSelector, "gameManager", gameManager);
            LudoEditorUtility.SetField(audioManager, "diceManager", diceManager);
            LudoEditorUtility.SetField(audioManager, "gameManager", gameManager);
            LudoEditorUtility.SetField(audioManager, "tokenSelector", tokenSelector);

            // saveManager and gridManager have no cross-manager references to wire yet.

            EditorUtility.DisplayDialog("Scene Bootstrapper",
                "Core managers are ready: GridManager, DiceManager, GameManager, AudioManager, SaveManager, TokenSelector.\n\n" +
                "Next: run '2. Board Waypoint Generator', then '3. Token Prefab Generator', " +
                "then '4. Basic UI Canvas Builder'.",
                "OK");
        }

        private static T GetOrCreate<T>(string name) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Scene Bootstrapper");
            T component = go.AddComponent<T>();
            EditorUtility.SetDirty(go);
            return component;
        }
    }
}
