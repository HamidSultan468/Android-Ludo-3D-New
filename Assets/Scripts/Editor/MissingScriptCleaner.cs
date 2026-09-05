using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace LudoEmpire.Ludo
{
#if UNITY_EDITOR
    /// <summary>
    /// Editor-only utility that sweeps every GameObject in the active scene - root objects, all
    /// nested children, and inactive objects alike - and strips any "Missing Script" components
    /// (leftover MonoBehaviour slots whose source .cs asset was deleted, renamed, or moved, which
    /// Unity can no longer resolve and otherwise leaves behind as null references). Safe to run
    /// repeatedly; a clean scene simply reports zero removals.
    /// </summary>
    /// <remarks>
    /// Removal goes through <see cref="GameObjectUtility.RemoveMonoBehavioursWithMissingScript"/>,
    /// which - like Unity's own "Remove Missing Scripts" button in the Inspector - does not push an
    /// undo step (a missing script has no live component instance for the undo system to restore).
    /// Every affected GameObject is still marked dirty so the cleanup is captured on save.
    /// </remarks>
    public static class MissingScriptCleaner
    {
        [MenuItem("Ludo Tools/Clean Missing Scripts")]
        public static void CleanMissingScripts()
        {
            Scene scene = SceneManager.GetActiveScene();
            if (!scene.IsValid() || !scene.isLoaded)
            {
                Debug.LogWarning("[MissingScriptCleaner] No valid, loaded active scene to clean.");
                return;
            }

            GameObject[] rootObjects = scene.GetRootGameObjects();
            if (rootObjects == null || rootObjects.Length == 0)
            {
                Debug.Log($"[MissingScriptCleaner] Active scene '{scene.name}' has no GameObjects to scan.");
                return;
            }

            int scannedObjects = 0;
            int removedComponents = 0;
            int affectedObjects = 0;

            foreach (GameObject root in rootObjects)
            {
                if (root == null) continue;
                CleanRecursive(root.transform, ref scannedObjects, ref removedComponents, ref affectedObjects);
            }

            if (removedComponents > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
            }

            Debug.Log($"[MissingScriptCleaner] Scanned {scannedObjects} GameObject(s) in '{scene.name}': " +
                      $"removed {removedComponents} missing script component(s) across {affectedObjects} GameObject(s).");
        }

        /// <summary>Depth-first walk of the full hierarchy under <paramref name="current"/>, including inactive objects.</summary>
        private static void CleanRecursive(Transform current, ref int scannedObjects, ref int removedComponents, ref int affectedObjects)
        {
            if (current == null) return;

            GameObject go = current.gameObject;
            scannedObjects++;

            int missingCount = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);
            if (missingCount > 0)
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
                removedComponents += missingCount;
                affectedObjects++;
                EditorUtility.SetDirty(go);
            }

            for (int i = 0; i < current.childCount; i++)
            {
                CleanRecursive(current.GetChild(i), ref scannedObjects, ref removedComponents, ref affectedObjects);
            }
        }
    }
#endif
}
