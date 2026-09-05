using UnityEngine;
using UnityEditor;

public class BoardGalleryViewer : EditorWindow
{
    private int selectedBoardIndex = 0;
    private readonly string[] boardNames = new string[]
    {
        "1. Classic Wooden Royal", "2. Frosted Acrylic Glass", "3. Gold & Obsidian VIP",
        "4. Cyberpunk Neon Circuit", "5. Jungle Tycoon Moss & Stone", "6. Emerald Gemstone",
        "7. Marble Imperial White", "8. Ruby Red Crystal", "9. Cosmic Galaxy Holographic",
        "10. Desert Kingdom Sandstone", "11. Midnight Dark Slate", "12. Pearl & Diamond Lux",
        "13. Neon Cyber Green", "14. Metallic Chrome Steel", "15. Rustic Ancient Viking",
        "16. Sunset Gradient Glass", "17. Deep Sea Aqua Crystal", "18. Volcanic Lava Core",
        "19. Pastel Clean Toy Style", "20. Brass & Antique Bronze", "21. Holographic Rainbow",
        "22. Frosted Mint Glass", "23. Royal Velvet Red", "24. Electric Purple Synthwave",
        "25. Jade Stone Dragon", "26. Matte Minimalist Flat", "27. Titanium Cyber Grid",
        "28. Autumn Oak Wood", "29. Diamond Prism Ice", "30. Ultimate Ludo Empire Gold"
    };

    [MenuItem("Ludo Tools/30 Board Gallery Viewer")]
    public static void ShowWindow()
    {
        GetWindow<BoardGalleryViewer>("30 Ludo Boards Viewer");
    }

    private void OnGUI()
    {
        GUILayout.Label("30 Premium 3D Ludo Boards Viewer", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        int previousIndex = selectedBoardIndex;
        selectedBoardIndex = EditorGUILayout.Popup("Select Board (1 to 30):", selectedBoardIndex, boardNames);

        EditorGUILayout.Space();
        if (GUILayout.Button("<< Previous Board", GUILayout.Height(30)) && selectedBoardIndex > 0)
        {
            selectedBoardIndex--;
        }
        if (GUILayout.Button("Next Board >>", GUILayout.Height(30)) && selectedBoardIndex < 29)
        {
            selectedBoardIndex++;
        }

        if (previousIndex != selectedBoardIndex || GUILayout.Button("Preview Selected Style", GUILayout.Height(40)))
        {
            ApplyBoardVisualStyle(selectedBoardIndex);
        }
    }

    private void ApplyBoardVisualStyle(int index)
    {
        GameObject boardRoot = GameObject.Find("LudoBoard_Root");
        if (boardRoot == null)
        {
            Debug.LogError("LudoBoard_Root Hierarchy me nahi mila! Pehle Scene build karein.");
            return;
        }

        MeshRenderer renderer = boardRoot.GetComponentInChildren<MeshRenderer>();
        if (renderer != null && renderer.sharedMaterial != null)
        {
            Material mat = new Material(renderer.sharedMaterial);
            
            // Generate 30 distinct procedural visual themes
            float hue = (index * 0.033f) % 1.0f;
            Color mainColor = Color.HSVToRGB(hue, 0.7f, 0.9f);
            
            mat.color = mainColor;
            mat.SetFloat("_Metallic", (index % 2 == 0) ? 0.85f : 0.1f);
            mat.SetFloat("_Glossiness", 0.75f + (index % 5) * 0.05f);

            renderer.sharedMaterial = mat;
            SceneView.RepaintAll();
            Debug.Log($"Displaying Visual Style {index + 1}: {boardNames[index]}");
        }
    }
}