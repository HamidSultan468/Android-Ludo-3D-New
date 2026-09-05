using System.Collections.Generic;
using System.IO;
using LudoGame.Board;
using LudoGame.Game;
using LudoGame.Player;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Creates the 16 player tokens (4 colors x 4 each) as simple colored
    /// capsule placeholders with PlayerToken already configured, saves each
    /// as a reusable Prefab under Assets/Prefabs/Tokens, and plugs them
    /// straight into GameManager's Players list. Swap the placeholder capsule
    /// mesh for real art any time later - PlayerToken and GameManager don't
    /// care what the token looks like, only that PlayerToken is attached.
    ///
    /// How to use:
    /// 1. Run "1. Scene Bootstrapper" first (so GameManager exists), and
    ///    "2. Board Waypoint Generator" (so tokens can be placed at their
    ///    yard spot immediately - optional but nicer).
    /// 2. Window > Ludo Tools > 3. Token Prefab Generator.
    /// </summary>
    public static class TokenPrefabGenerator
    {
        private static readonly string[] ColorNames = { "Red", "Green", "Yellow", "Blue" };
        private static readonly Color[] ColorValues =
        {
            Color.red, Color.green, Color.yellow, new Color(0.2f, 0.4f, 1f),
        };

        [MenuItem("Window/Ludo Tools/3. Token Prefab Generator")]
        private static void Generate()
        {
            GameManager gameManager = Object.FindAnyObjectByType<GameManager>();
            GridManager gridManager = Object.FindAnyObjectByType<GridManager>();

            if (gameManager == null)
            {
                EditorUtility.DisplayDialog("Token Prefab Generator",
                    "No GameManager found in the scene. Run '1. Scene Bootstrapper' first.", "OK");
                return;
            }

            EnsureFolder("Assets/Prefabs");
            EnsureFolder("Assets/Prefabs/Tokens");

            Transform tokensRoot = FindOrCreateRoot("Tokens");
            var allColorTokens = new List<List<PlayerToken>>();

            for (int color = 0; color < 4; color++)
            {
                Transform colorRoot = FindOrCreateChild(tokensRoot, ColorNames[color]);
                var tokensForColor = new List<PlayerToken>();

                for (int slot = 0; slot < 4; slot++)
                    tokensForColor.Add(CreateToken(colorRoot, (GridManager.PlayerColor)color, slot, gridManager));

                allColorTokens.Add(tokensForColor);
            }

            ApplyToGameManager(gameManager, allColorTokens);

            EditorUtility.DisplayDialog("Token Prefab Generator",
                "Created 16 tokens (4 per color), saved as prefabs in Assets/Prefabs/Tokens, " +
                "and linked them into GameManager's Players list.", "OK");
        }

        private static PlayerToken CreateToken(Transform parent, GridManager.PlayerColor color, int slot, GridManager gridManager)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(go, "Token Prefab Generator");
            go.name = color + "_Token_" + slot;
            go.transform.SetParent(parent, false);
            go.transform.localScale = new Vector3(0.4f, 0.25f, 0.4f);

            SetMaterialColor(go.GetComponent<Renderer>(), ColorValues[(int)color]);

            PlayerToken token = go.AddComponent<PlayerToken>();
            var so = new SerializedObject(token);
            so.FindProperty("color").enumValueIndex = (int)color;
            so.FindProperty("yardSlot").intValue = slot;
            so.ApplyModifiedProperties();

            PositionAtYard(go.transform, color, slot, gridManager);

            string prefabPath = "Assets/Prefabs/Tokens/" + go.name + ".prefab";
            PrefabUtility.SaveAsPrefabAssetAndConnect(go, prefabPath, InteractionMode.AutomatedAction);

            return go.GetComponent<PlayerToken>();
        }

        private static void PositionAtYard(Transform tokenTransform, GridManager.PlayerColor color, int slot, GridManager gridManager)
        {
            if (gridManager == null) return;

            string fieldName = color + "Yard"; // e.g. "Red" + "Yard" -> field "redYard"
            fieldName = char.ToLowerInvariant(fieldName[0]) + fieldName.Substring(1);

            var so = new SerializedObject(gridManager);
            SerializedProperty yardProp = so.FindProperty(fieldName);
            if (yardProp == null || slot >= yardProp.arraySize) return;

            if (yardProp.GetArrayElementAtIndex(slot).objectReferenceValue is Transform yardTransform)
                tokenTransform.position = yardTransform.position;
        }

        private static void ApplyToGameManager(GameManager gameManager, List<List<PlayerToken>> tokensByColor)
        {
            var so = new SerializedObject(gameManager);
            SerializedProperty playersProp = so.FindProperty("players");
            playersProp.arraySize = 4;

            for (int color = 0; color < 4; color++)
            {
                SerializedProperty playerEntry = playersProp.GetArrayElementAtIndex(color);
                playerEntry.FindPropertyRelative("color").enumValueIndex = color;

                SerializedProperty tokensProp = playerEntry.FindPropertyRelative("tokens");
                tokensProp.arraySize = tokensByColor[color].Count;
                for (int i = 0; i < tokensByColor[color].Count; i++)
                    tokensProp.GetArrayElementAtIndex(i).objectReferenceValue = tokensByColor[color][i];
            }

            so.ApplyModifiedProperties();
        }

        private static void SetMaterialColor(Renderer renderer, Color color)
        {
            Material mat = renderer.sharedMaterial != null
                ? new Material(renderer.sharedMaterial)
                : new Material(Shader.Find("Standard"));

            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.color = color;

            renderer.sharedMaterial = mat;
        }

        private static Transform FindOrCreateRoot(string name)
        {
            GameObject existing = GameObject.Find(name);
            if (existing != null) return existing.transform;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Token Prefab Generator");
            return go.transform;
        }

        private static Transform FindOrCreateChild(Transform parent, string name)
        {
            Transform existing = parent.Find(name);
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Token Prefab Generator");
            go.transform.SetParent(parent, false);
            return go.transform;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
