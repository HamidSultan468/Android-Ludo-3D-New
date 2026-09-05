using LudoGame.MiniGames.GulliDanda;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using static LudoGame.EditorTools.LudoEditorUtility;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// One-click setup for the Gulli Danda playing field: ground, a Kotha
    /// marker, the Gulli (with Rigidbody + Collider, ready to Flip/Strike),
    /// and the Bhaju fielder - wired to a new GulliDandaGameManager. Then
    /// builds the UI on top via GulliDandaUIBuilder. This is the piece that
    /// was missing before: only the UI existed, with no actual playable
    /// world objects to test against.
    ///
    /// How to use:
    /// - Window > Ludo Tools > Mini-Games > Gulli Danda Scene Builder (in an
    ///   open scene of your choice - ideally a fresh empty one), or
    /// - Window > Ludo Tools > Mini-Games > QA: Gulli Danda Build + Save Test Scene
    ///   for a one-click version that saves to
    ///   Assets/Scenes/GulliDandaMilestone_Test.unity, or from the command line:
    ///   Unity.exe -batchmode -nographics -quit -projectPath "&lt;path&gt;"
    ///     -executeMethod LudoGame.EditorTools.GulliDandaSceneBuilder.QaBuildAndSaveTestScene
    /// </summary>
    public static class GulliDandaSceneBuilder
    {
        private const string TestScenePath = "Assets/Scenes/GulliDandaMilestone_Test.unity";

        [MenuItem("Window/Ludo Tools/Mini-Games/Gulli Danda Scene Builder")]
        private static void BuildInCurrentScene() => Build();

        [MenuItem("Window/Ludo Tools/Mini-Games/QA: Gulli Danda Build + Save Test Scene")]
        public static void QaBuildAndSaveTestScene()
        {
            Debug.Log("[QA] Starting Gulli Danda automated setup...");

            EnsureFolder("Assets/Scenes");
            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            Debug.Log("[QA] Created a fresh scene for testing.");

            Build();
            Debug.Log("[QA] Build complete.");

            bool saved = EditorSceneManager.SaveScene(scene, TestScenePath);
            Debug.Log("[QA] Scene saved to " + TestScenePath + " - success: " + saved);
            Debug.Log("[QA] Gulli Danda automated setup COMPLETE. Open " + TestScenePath + " and press Play to test.");
        }

        private static void Build()
        {
            Transform ground = BuildGround();
            Transform kotha = BuildKotha(ground);
            GulliController gulli = BuildGulli(kotha);
            PowerMeter powerMeter = GetOrCreate<PowerMeter>("PowerMeter");
            BhajuAI bhaju = BuildBhaju(gulli, kotha);

            GameObject managerGO = new GameObject("GulliDandaGameManager");
            Undo.RegisterCreatedObjectUndo(managerGO, "Gulli Danda Scene Builder");
            GulliDandaGameManager manager = managerGO.AddComponent<GulliDandaGameManager>();
            SetField(manager, "gulli", gulli);
            SetField(manager, "powerMeter", powerMeter);
            SetField(manager, "bhaju", bhaju);

            GulliDandaUIBuilder.Build();

            EditorUtility.DisplayDialog("Gulli Danda Scene Builder",
                "Built the playing field: ground, Kotha, Gulli (with physics), Bhaju fielder, and the " +
                "full UI on top.\n\nSave the scene (Ctrl+S) and press Play: tap to flip the Gulli, " +
                "swipe to strike it, and watch Bhaju try to catch or throw it back.",
                "OK");
        }

        private static T GetOrCreate<T>(string name) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Gulli Danda Scene Builder");
            return go.AddComponent<T>();
        }

        private static Transform BuildGround()
        {
            GameObject existing = GameObject.Find("GulliDandaGround");
            if (existing != null) return existing.transform;

            GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Undo.RegisterCreatedObjectUndo(ground, "Gulli Danda Scene Builder");
            ground.name = "GulliDandaGround";
            ground.transform.localScale = Vector3.one * 3f;
            TintPrimitive(ground, new Color(0.7f, 0.55f, 0.35f)); // sandy/clay ground

            return ground.transform;
        }

        private static Transform BuildKotha(Transform ground)
        {
            GameObject existing = GameObject.Find("Kotha");
            if (existing != null) return existing.transform;

            GameObject kotha = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Undo.RegisterCreatedObjectUndo(kotha, "Gulli Danda Scene Builder");
            kotha.name = "Kotha";
            kotha.transform.position = new Vector3(0f, 0.02f, 0f);
            kotha.transform.localScale = new Vector3(1.5f, 0.04f, 0.4f);
            TintPrimitive(kotha, Color.white);

            Object.DestroyImmediate(kotha.GetComponent<Collider>()); // ground marker only, not solid
            return kotha.transform;
        }

        private static GulliController BuildGulli(Transform kotha)
        {
            GameObject existing = GameObject.Find("Gulli");
            if (existing != null) return existing.GetComponent<GulliController>();

            GameObject gulliGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(gulliGO, "Gulli Danda Scene Builder");
            gulliGO.name = "Gulli";
            gulliGO.transform.position = kotha.position + Vector3.up * 0.15f;
            gulliGO.transform.localScale = new Vector3(0.15f, 0.25f, 0.15f);
            TintPrimitive(gulliGO, new Color(0.6f, 0.35f, 0.15f)); // wood color

            GulliController gulli = gulliGO.AddComponent<GulliController>(); // auto-adds Rigidbody (RequireComponent)
            SetFloatField(gulli, "groundY", kotha.position.y);
            SetField(gulli, "spawnPoint", kotha);

            return gulli;
        }

        private static BhajuAI BuildBhaju(GulliController gulli, Transform kotha)
        {
            GameObject existing = GameObject.Find("Bhaju");
            if (existing != null) return existing.GetComponent<BhajuAI>();

            GameObject bhajuGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Undo.RegisterCreatedObjectUndo(bhajuGO, "Gulli Danda Scene Builder");
            bhajuGO.name = "Bhaju";
            bhajuGO.transform.position = kotha.position + new Vector3(0f, 0.9f, 8f);
            bhajuGO.transform.localScale = new Vector3(0.6f, 0.9f, 0.6f);
            TintPrimitive(bhajuGO, new Color(0.2f, 0.4f, 0.8f));

            BhajuAI bhaju = bhajuGO.AddComponent<BhajuAI>();
            SetField(bhaju, "gulli", gulli);
            SetField(bhaju, "kothaTarget", kotha);

            return bhaju;
        }

        private static void TintPrimitive(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = renderer.sharedMaterial != null ? new Material(renderer.sharedMaterial) : new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.color = color;
            renderer.sharedMaterial = mat;
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path)) return;

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace("\\", "/");
            string folderName = System.IO.Path.GetFileName(path);
            AssetDatabase.CreateFolder(parent, folderName);
        }
    }
}
