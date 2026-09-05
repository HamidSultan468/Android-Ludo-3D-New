using LudoGame.Empire;
using LudoGame.Empire.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using static LudoGame.EditorTools.LudoEditorUtility;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// One-click setup for the Ludo Empire "Milestone 1" playable loop:
    /// EmpireManager, a ground patch with 10 placeholder trees to chop,
    /// tap-to-chop input, and a full UI (biome select, HUD, market, tool
    /// shop, build-foundry panel) wired to the seeded ScriptableObject data.
    ///
    /// This is a local, single-player prototype scene - no multiplayer or
    /// cloud save (those need Photon/FishNet + a backend, set up separately
    /// later). Best run in its own empty scene rather than the Ludo board
    /// scene, so its camera/UI don't compete with the Ludo game's.
    ///
    /// How to use:
    /// 1. Window > Ludo Tools > Empire > 1. Seed Starter Data (once).
    /// 2. Window > Ludo Tools > Empire > 2. Build Milestone 1 Scene.
    /// 3. Save the scene (Ctrl+S) and press Play.
    /// </summary>
    public static class EmpireSceneBuilder
    {
        private const string DataFolder = "Assets/Empire/Data";

        [MenuItem("Window/Ludo Tools/Empire/2. Build Milestone 1 Scene")]
        internal static void Build()
        {
            MaterialDefinition wood = AssetDatabase.LoadAssetAtPath<MaterialDefinition>(DataFolder + "/Materials/Material_Wood.asset");
            BiomeDefinition forest = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(DataFolder + "/Biomes/Biome_ForestBiome.asset");
            BiomeDefinition snow = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(DataFolder + "/Biomes/Biome_SnowBiome.asset");
            FactoryDefinition foundry = AssetDatabase.LoadAssetAtPath<FactoryDefinition>(DataFolder + "/Factories/Factory_BasicFoundry.asset");
            ToolDefinition[] tools = LoadAllTools();

            if (wood == null || forest == null || snow == null || foundry == null || tools.Length == 0)
            {
                EditorUtility.DisplayDialog("Empire Scene Builder",
                    "Starter data not found. Run '1. Seed Starter Data' first, then try again.", "OK");
                return;
            }

            EmpireManager manager = GetOrCreate<EmpireManager>("EmpireManager");
            BuildGroundAndTrees(wood);
            TreeChopInput chopInput = GetOrCreate<TreeChopInput>("TreeChopInput");

            Canvas canvas = GetOrCreateCanvas();
            EnsureEventSystem();

            BuildBiomeSelectPanel(canvas.transform, manager, forest, snow);
            BuildHud(canvas.transform, manager, chopInput, wood);
            BuildMarketPanel(canvas.transform, manager, wood);
            BuildToolShopPanel(canvas.transform, manager, tools);
            BuildFactoryPanel(canvas.transform, manager, foundry);

            EditorUtility.DisplayDialog("Empire Scene Builder",
                "Built the Milestone 1 loop: biome select screen, ground + 10 placeholder trees, HUD, " +
                "market, tool shop, and build-foundry panel.\n\n" +
                "Save the scene (Ctrl+S) and press Play to test: pick a biome, tap the trees to chop " +
                "them, sell Wood, buy a tool, then build the Basic Foundry.", "OK");
        }

        private static ToolDefinition[] LoadAllTools()
        {
            string[] guids = AssetDatabase.FindAssets("t:ToolDefinition", new[] { DataFolder + "/Tools" });
            var tools = new ToolDefinition[guids.Length];
            for (int i = 0; i < guids.Length; i++)
                tools[i] = AssetDatabase.LoadAssetAtPath<ToolDefinition>(AssetDatabase.GUIDToAssetPath(guids[i]));
            return tools;
        }

        private static T GetOrCreate<T>(string name) where T : Component
        {
            T existing = Object.FindAnyObjectByType<T>();
            if (existing != null) return existing;

            GameObject go = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(go, "Empire Scene Builder");
            return go.AddComponent<T>();
        }

        // ---------------- World: ground + trees ----------------

        private static void BuildGroundAndTrees(MaterialDefinition wood)
        {
            if (GameObject.Find("EmpireGround") == null)
            {
                GameObject ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
                Undo.RegisterCreatedObjectUndo(ground, "Empire Scene Builder");
                ground.name = "EmpireGround";
                ground.transform.position = Vector3.zero;
                ground.transform.localScale = Vector3.one * 3f;
                SetPrimitiveColor(ground, new Color(0.35f, 0.55f, 0.25f));
            }

            if (GameObject.Find("Trees") != null) return; // already built once

            Transform treesRoot = new GameObject("Trees").transform;
            Undo.RegisterCreatedObjectUndo(treesRoot.gameObject, "Empire Scene Builder");

            const int treeCount = 10;
            for (int i = 0; i < treeCount; i++)
            {
                float angle = i / (float)treeCount * Mathf.PI * 2f;
                float radius = 6f + (i % 3);
                Vector3 pos = new Vector3(Mathf.Cos(angle) * radius, 0.9f, Mathf.Sin(angle) * radius);

                GameObject treeGO = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                Undo.RegisterCreatedObjectUndo(treeGO, "Empire Scene Builder");
                treeGO.name = "Tree_" + i;
                treeGO.transform.SetParent(treesRoot, false);
                treeGO.transform.position = pos;
                treeGO.transform.localScale = new Vector3(0.6f, 1.8f, 0.6f);
                SetPrimitiveColor(treeGO, new Color(0.25f, 0.45f, 0.15f));

                ResourceTree tree = treeGO.AddComponent<ResourceTree>();
                SetField(tree, "material", wood);
            }
        }

        private static void SetPrimitiveColor(GameObject go, Color color)
        {
            Renderer renderer = go.GetComponent<Renderer>();
            if (renderer == null) return;

            Material mat = renderer.sharedMaterial != null ? new Material(renderer.sharedMaterial) : new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            else if (mat.HasProperty("_Color")) mat.color = color;
            renderer.sharedMaterial = mat;
        }

        // ---------------- UI ----------------

        private static void BuildBiomeSelectPanel(Transform canvasTransform, EmpireManager manager, BiomeDefinition forest, BiomeDefinition snow)
        {
            GameObject panel = CreatePanel(canvasTransform, "BiomeSelectPanel", new Color(0f, 0f, 0f, 0.85f));

            Text title = CreateText(panel.transform, "Title", "Choose Your Biome", 44, TextAnchor.MiddleCenter);
            PositionRow(title.rectTransform, 0.68f, 700f, 80f);

            Button forestButton = CreateButton(panel.transform, "ForestButton", "Forest Biome");
            RectTransform forestRect = forestButton.GetComponent<RectTransform>();
            PositionRow(forestRect, 0.5f, 260f, 90f);
            forestRect.anchoredPosition = new Vector2(-150f, 0f);

            Button snowButton = CreateButton(panel.transform, "SnowButton", "Snow Biome");
            RectTransform snowRect = snowButton.GetComponent<RectTransform>();
            PositionRow(snowRect, 0.5f, 260f, 90f);
            snowRect.anchoredPosition = new Vector2(150f, 0f);

            BiomeSelectUI ui = panel.AddComponent<BiomeSelectUI>();
            SetField(ui, "empireManager", manager);
            SetField(ui, "panel", panel);
            SetField(ui, "forestButton", forestButton);
            SetField(ui, "forestBiome", forest);
            SetField(ui, "snowButton", snowButton);
            SetField(ui, "snowBiome", snow);
        }

        private static void BuildHud(Transform canvasTransform, EmpireManager manager, TreeChopInput chopInput, MaterialDefinition wood)
        {
            Text materialText = CreateText(canvasTransform, "MaterialText", "Wood: 0", 28, TextAnchor.UpperLeft);
            AnchorTopLeft(materialText.rectTransform, 0f);

            Text silverText = CreateText(canvasTransform, "SilverText", "0 Silver", 28, TextAnchor.UpperLeft);
            AnchorTopLeft(silverText.rectTransform, 40f);

            Text treesText = CreateText(canvasTransform, "TreesFelledText", "Trees Felled: 0 / 10", 28, TextAnchor.UpperLeft);
            AnchorTopLeft(treesText.rectTransform, 80f);

            GameObject hudGO = new GameObject("EmpireHud");
            Undo.RegisterCreatedObjectUndo(hudGO, "Empire Scene Builder");
            hudGO.transform.SetParent(canvasTransform, false);

            EmpireHudUI hud = hudGO.AddComponent<EmpireHudUI>();
            SetField(hud, "empireManager", manager);
            SetField(hud, "treeChopInput", chopInput);
            SetField(hud, "trackedMaterial", wood);
            SetField(hud, "materialText", materialText);
            SetField(hud, "silverCoinsText", silverText);
            SetField(hud, "treesFelledText", treesText);
        }

        private static void BuildMarketPanel(Transform canvasTransform, EmpireManager manager, MaterialDefinition wood)
        {
            Button sellButton = CreateButton(canvasTransform, "SellWoodButton", "Sell Wood");
            RectTransform rt = sellButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(0f, 0f);
            rt.pivot = new Vector2(0f, 0f);
            rt.anchoredPosition = new Vector2(30f, 30f);
            rt.sizeDelta = new Vector2(320f, 70f);

            MarketUI market = sellButton.gameObject.AddComponent<MarketUI>();
            SetField(market, "empireManager", manager);
            SetField(market, "material", wood);
            SetField(market, "sellButton", sellButton);
            SetField(market, "sellButtonLabel", sellButton.GetComponentInChildren<Text>());
        }

        private static void BuildToolShopPanel(Transform canvasTransform, EmpireManager manager, ToolDefinition[] tools)
        {
            GameObject panel = new GameObject("ToolShopPanel", typeof(RectTransform));
            Undo.RegisterCreatedObjectUndo(panel, "Empire Scene Builder");
            panel.transform.SetParent(canvasTransform, false);

            RectTransform panelRect = panel.GetComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(1f, 0f);
            panelRect.anchorMax = new Vector2(1f, 0f);
            panelRect.pivot = new Vector2(1f, 0f);
            panelRect.anchoredPosition = new Vector2(-30f, 30f);
            panelRect.sizeDelta = new Vector2(320f, 60f + tools.Length * 60f);

            for (int i = 0; i < tools.Length; i++)
            {
                Button buyButton = CreateButton(panel.transform, "Buy_" + tools[i].displayName, tools[i].displayName);
                RectTransform buttonRect = buyButton.GetComponent<RectTransform>();
                buttonRect.anchorMin = new Vector2(0f, 1f);
                buttonRect.anchorMax = new Vector2(1f, 1f);
                buttonRect.pivot = new Vector2(0.5f, 1f);
                buttonRect.anchoredPosition = new Vector2(0f, -i * 60f);
                buttonRect.sizeDelta = new Vector2(0f, 55f);

                ToolShopEntryUI entry = buyButton.gameObject.AddComponent<ToolShopEntryUI>();
                SetField(entry, "empireManager", manager);
                SetField(entry, "tool", tools[i]);
                SetField(entry, "buyButton", buyButton);
                SetField(entry, "label", buyButton.GetComponentInChildren<Text>());
            }
        }

        private static void BuildFactoryPanel(Transform canvasTransform, EmpireManager manager, FactoryDefinition foundry)
        {
            Button buildButton = CreateButton(canvasTransform, "BuildFoundryButton", "Build Basic Foundry - Free");
            RectTransform rt = buildButton.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 30f);
            rt.sizeDelta = new Vector2(360f, 70f);

            FactoryBuildUI factoryUI = buildButton.gameObject.AddComponent<FactoryBuildUI>();
            SetField(factoryUI, "empireManager", manager);
            SetField(factoryUI, "factory", foundry);
            SetField(factoryUI, "buildButton", buildButton);
            SetField(factoryUI, "label", buildButton.GetComponentInChildren<Text>());
        }

        private static void AnchorTopLeft(RectTransform rt, float yOffset)
        {
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.anchoredPosition = new Vector2(30f, -30f - yOffset);
            rt.sizeDelta = new Vector2(400f, 36f);
        }
    }
}
