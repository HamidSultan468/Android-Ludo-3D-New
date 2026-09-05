using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>Generates <c>Assets/_TangentLudoEmpire/Scenes/Boot.unity</c>: an otherwise-empty scene holding one
    /// <see cref="GameServices"/> object with <c>autoGoToDashboardOnBoot = true</c>, so the app boots
    /// straight into the Dashboard after a 1s hold. Rerunnable.</summary>
    public static class BootSceneBuilder
    {
        [MenuItem("Tangent Ludo Empire/Phase 1/Build Boot Scene")]
        public static void BuildBoot()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            Directory.CreateDirectory(DashboardSceneBuilder.SceneDir);
            string path = $"{DashboardSceneBuilder.SceneDir}/{AppConstants.SCENE_BOOT}.unity";
            if (File.Exists(path)) AssetDatabase.DeleteAsset(path);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            var gsGO = new GameObject("GameServices");
            var gs = gsGO.AddComponent<GameServices>();

            var so = new SerializedObject(gs);
            var prop = so.FindProperty("autoGoToDashboardOnBoot");
            if (prop != null) { prop.boolValue = true; so.ApplyModifiedPropertiesWithoutUndo(); }
            else Debug.LogWarning("[BootSceneBuilder] GameServices.autoGoToDashboardOnBoot not found - set it manually.");

            EditorSceneManager.SaveScene(scene, path);
            AssetDatabase.Refresh();
            Debug.Log($"[BootSceneBuilder] Built {path}");
        }
    }
}
