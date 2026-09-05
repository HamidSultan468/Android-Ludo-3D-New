using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Creates a simple flat, textured Plane at the origin so the Game view
    /// isn't blank while there's no real 3D board model yet. Uses
    /// "ludo board 3d.png" from Assets/Art/Boards if found, otherwise any
    /// other texture in that folder, otherwise a plain green plane.
    ///
    /// This is only a stand-in - delete the "BoardPlaceholder" object once
    /// you have a real 3D board model, and re-run "2. Board Waypoint
    /// Generator" against it.
    ///
    /// How to use: Window > Ludo Tools > 6. Create Placeholder Board.
    /// </summary>
    public static class PlaceholderBoardGenerator
    {
        private const string ObjectName = "BoardPlaceholder";
        private const float BoardSize = 15f; // matches the 15x15 grid used by Board Waypoint Generator

        [MenuItem("Window/Ludo Tools/6. Create Placeholder Board")]
        private static void Create()
        {
            GameObject plane = GameObject.Find(ObjectName);
            bool alreadyExisted = plane != null;

            if (plane == null)
            {
                plane = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(plane, "Create Placeholder Board");
                plane.name = ObjectName;
            }
            else
            {
                Undo.RecordObject(plane.transform, "Reset Placeholder Board");
            }

            // Always reset position/rotation/scale to correct values - this also fixes it
            // if someone accidentally typed the wrong numbers in the Inspector (e.g. a
            // non-uniform scale, which stretches and distorts the board's texture).
            plane.transform.position = Vector3.zero;
            plane.transform.rotation = Quaternion.identity;
            plane.transform.localScale = Vector3.one * (BoardSize / 10f); // Unity's default Plane is 10x10 units

            ApplyBoardTexture(plane.GetComponent<Renderer>());

            Selection.activeGameObject = plane;
            EditorGUIUtility.PingObject(plane);

            EditorUtility.DisplayDialog("Placeholder Board",
                (alreadyExisted ? "Reset the existing" : "Created a new") + " " + BoardSize + "x" + BoardSize +
                " placeholder plane at the origin (position, rotation, and scale all corrected).\n\n" +
                "This is only a stand-in for testing - replace it with a real 3D board model whenever " +
                "you have one, then re-run '2. Board Waypoint Generator' and '5. Wire All References'.",
                "OK");
        }

        private static void ApplyBoardTexture(Renderer renderer)
        {
            Texture2D texture = FindBoardTexture();

            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard"));
            if (texture != null)
            {
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", texture);
                else if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", texture);
            }
            else
            {
                Color placeholderGreen = new Color(0.15f, 0.55f, 0.25f);
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", placeholderGreen);
                else mat.color = placeholderGreen;
            }

            renderer.sharedMaterial = mat;
        }

        private static Texture2D FindBoardTexture()
        {
            Texture2D preferred = AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/Art/Boards/ludo board 3d.png");
            if (preferred != null) return preferred;

            string[] guids = AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/Art/Boards" });
            if (guids.Length == 0) return null;

            string path = AssetDatabase.GUIDToAssetPath(guids[0]);
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }
    }
}
