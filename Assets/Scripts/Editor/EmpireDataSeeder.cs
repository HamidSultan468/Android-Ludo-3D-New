using System.Collections.Generic;
using System.IO;
using LudoGame.Empire;
using UnityEditor;
using UnityEngine;

namespace LudoGame.EditorTools
{
    /// <summary>
    /// Creates a sensible set of starter ScriptableObject assets for the
    /// Ludo Empire Milestone 1 prototype: 7 materials, 3 tools, 2 biomes,
    /// and the free starter "Basic Foundry" factory - so you don't have to
    /// right-click-create 13 assets by hand. Safe to re-run; skips any
    /// asset that already exists at its path.
    ///
    /// How to use: Window > Ludo Tools > Empire > 1. Seed Starter Data.
    /// </summary>
    public static class EmpireDataSeeder
    {
        private const string DataFolder = "Assets/Empire/Data";

        [MenuItem("Window/Ludo Tools/Empire/1. Seed Starter Data")]
        internal static void Seed()
        {
            EnsureFolder("Assets/Empire");
            EnsureFolder(DataFolder);
            EnsureFolder(DataFolder + "/Materials");
            EnsureFolder(DataFolder + "/Tools");
            EnsureFolder(DataFolder + "/Biomes");
            EnsureFolder(DataFolder + "/Factories");

            MaterialDefinition wood = CreateMaterial("Wood", 2);
            CreateMaterial("Iron", 4);
            CreateMaterial("Copper", 4);
            CreateMaterial("Silver", 6);
            CreateMaterial("Rubber", 3);
            MaterialDefinition minerals = CreateMaterial("Minerals", 5);
            CreateMaterial("Brick", 3);

            CreateTool("Hammer", 20, 1.25f);
            CreateTool("Chisel", 35, 1.5f);
            CreateTool("Cutter", 50, 1.75f);

            CreateBiome("ForestBiome", "Forest Biome", wood);
            CreateBiome("SnowBiome", "Snow Biome", minerals);

            CreateBasicFoundry();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            EditorUtility.DisplayDialog("Empire Data Seeder",
                "Created (or found existing) starter assets under " + DataFolder + ":\n" +
                "7 Materials, 3 Tools, 2 Biomes, 1 Factory (Basic Foundry).\n\n" +
                "Next: Window > Ludo Tools > Empire > 2. Build Milestone 1 Scene.", "OK");
        }

        private static MaterialDefinition CreateMaterial(string name, int sellPrice)
        {
            string path = DataFolder + "/Materials/Material_" + name + ".asset";
            MaterialDefinition existing = AssetDatabase.LoadAssetAtPath<MaterialDefinition>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<MaterialDefinition>();
            asset.displayName = name;
            asset.baseSellPrice = sellPrice;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static ToolDefinition CreateTool(string name, int cost, float multiplier)
        {
            string path = DataFolder + "/Tools/Tool_" + name + ".asset";
            ToolDefinition existing = AssetDatabase.LoadAssetAtPath<ToolDefinition>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<ToolDefinition>();
            asset.displayName = name;
            asset.silverCoinCost = cost;
            asset.gatherYieldMultiplier = multiplier;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static BiomeDefinition CreateBiome(string fileSuffix, string displayName, MaterialDefinition primaryMaterial)
        {
            string path = DataFolder + "/Biomes/Biome_" + fileSuffix + ".asset";
            BiomeDefinition existing = AssetDatabase.LoadAssetAtPath<BiomeDefinition>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<BiomeDefinition>();
            asset.displayName = displayName;
            asset.primaryMaterial = primaryMaterial;
            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static FactoryDefinition CreateBasicFoundry()
        {
            string path = DataFolder + "/Factories/Factory_BasicFoundry.asset";
            FactoryDefinition existing = AssetDatabase.LoadAssetAtPath<FactoryDefinition>(path);
            if (existing != null) return existing;

            var asset = ScriptableObject.CreateInstance<FactoryDefinition>();
            asset.displayName = "Basic Foundry";
            asset.description = "Your first factory - free to build.";
            asset.silverCoinCost = 0; // "Basic Foundry is free" per the design doc
            asset.materialCosts = new List<MaterialCost>(); // no material cost either - truly free
            AssetDatabase.CreateAsset(asset, path);
            return asset;
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
