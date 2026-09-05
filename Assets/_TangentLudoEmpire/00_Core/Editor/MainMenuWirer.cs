using System.IO;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>
    /// STEP 2: removes MainMenu-new's dependency on <c>SimpleMenuLoader</c>.
    ///  1. "Wire MainMenu" - adds <see cref="MenuMatchStarter"/> to the Canvas, repoints the two mode
    ///     buttons' onClick at it, and strips every <c>SimpleMenuLoader</c> component from the scene
    ///     (by type name, so this tool has no compile-time dependency on the class it removes).
    ///  2. "Delete SimpleMenuLoader.cs" - only after (1); refuses if any scene/prefab still references it
    ///     (so it can never re-introduce the missing-script / corrupt-level0 bug).
    /// </summary>
    public static class MainMenuWirer
    {
        private const string MenuScenePath = "Assets/Scenes/MainMenu-new.unity";
        private const string SmlPath = "Assets/Scripts/UI/SimpleMenuLoader.cs";

        [MenuItem("Tangent Ludo Empire/Phase 1/Wire MainMenu (replace SimpleMenuLoader)")]
        public static void WireMainMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            if (!File.Exists(MenuScenePath)) { Debug.LogError($"[MainMenuWirer] Missing {MenuScenePath}"); return; }

            var scene = EditorSceneManager.OpenScene(MenuScenePath, OpenSceneMode.Single);

            var canvas = GameObject.Find("Canvas");
            if (canvas == null) { Debug.LogError("[MainMenuWirer] 'Canvas' not found in MainMenu-new."); return; }

            var starter = canvas.GetComponent<MenuMatchStarter>();
            if (starter == null) starter = canvas.AddComponent<MenuMatchStarter>();

            int wired = 0;
            wired += WireButton("PlayVsAIButton",   starter, nameof(MenuMatchStarter.StartPlayVsAI));
            wired += WireButton("PassAndPlayButton", starter, nameof(MenuMatchStarter.StartPassAndPlay));

            int removed = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (mb != null && mb.GetType().Name == "SimpleMenuLoader")
                {
                    Object.DestroyImmediate(mb, true);
                    removed++;
                }
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[MainMenuWirer] Wired {wired} button(s) to MenuMatchStarter; removed {removed} SimpleMenuLoader component(s). " +
                      "Test in Play mode, then run 'Tangent/Phase 1/Delete SimpleMenuLoader.cs'.");
        }

        private static int WireButton(string goName, MenuMatchStarter target, string method)
        {
            var go = GameObject.Find(goName);
            if (go == null) { Debug.LogWarning($"[MainMenuWirer] {goName} not found."); return 0; }
            var btn = go.GetComponent<Button>();
            if (btn == null) { Debug.LogWarning($"[MainMenuWirer] {goName} has no Button."); return 0; }

            for (int i = btn.onClick.GetPersistentEventCount() - 1; i >= 0; i--)
                UnityEventTools.RemovePersistentListener(btn.onClick, i);

            var action = (UnityAction)System.Delegate.CreateDelegate(typeof(UnityAction), target, method);
            UnityEventTools.AddPersistentListener(btn.onClick, action);
            EditorUtility.SetDirty(btn);
            Debug.Log($"[MainMenuWirer] {goName}.onClick -> MenuMatchStarter.{method}()");
            return 1;
        }

        [MenuItem("Tangent Ludo Empire/Phase 1/Delete SimpleMenuLoader.cs")]
        public static void DeleteSimpleMenuLoader()
        {
            string guid = AssetDatabase.AssetPathToGUID(SmlPath);
            if (string.IsNullOrEmpty(guid)) { Debug.Log("[MainMenuWirer] SimpleMenuLoader.cs already removed."); return; }

            foreach (var g in AssetDatabase.FindAssets("t:SceneAsset"))
                if (StillReferences(AssetDatabase.GUIDToAssetPath(g), guid)) return;
            foreach (var g in AssetDatabase.FindAssets("t:Prefab"))
                if (StillReferences(AssetDatabase.GUIDToAssetPath(g), guid)) return;

            AssetDatabase.DeleteAsset(SmlPath);
            Debug.Log("[MainMenuWirer] Deleted Assets/Scripts/UI/SimpleMenuLoader.cs (no references remained).");
        }

        private static bool StillReferences(string assetPath, string guid)
        {
            if (string.IsNullOrEmpty(assetPath) || !File.Exists(assetPath)) return false;
            if (File.ReadAllText(assetPath).Contains(guid))
            {
                Debug.LogError($"[MainMenuWirer] {assetPath} still references SimpleMenuLoader - run 'Wire MainMenu' first. Delete aborted.");
                return true;
            }
            return false;
        }
    }
}
