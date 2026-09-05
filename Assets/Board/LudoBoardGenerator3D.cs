using UnityEngine;
using System.Collections.Generic;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace LudoEmpire.Board
{
    public enum TileType { Common, Safe, HomePath, HomeBase, Center }
    public enum PlayerColor { None, Red, Green, Yellow, Blue }

    /// <summary>
    /// Procedurally builds a 15x15 3D Ludo board in a "Neon Glow / Sci-Fi Glass" style: emissive
    /// glass-look track tiles and home-base sockets each with their own glowing rim, a sleek metallic
    /// frame/base, and a layered neon star hub at the center. All materials are generated on demand with
    /// the emission keyword properly baked in (so <see cref="Tile3D"/>'s MaterialPropertyBlock-based
    /// per-tile tinting actually glows), falling back to whatever you assign in the "Theme Materials"
    /// fields if you'd rather supply your own. Purely a styling/geometry pass - every grid position,
    /// scale, and coordinate computed below is unchanged from the original layout, so waypoint positions
    /// and anything reading <see cref="Tile3D.gridCoordinate"/>/<see cref="Tile3D.GetTokenSurfacePosition"/>
    /// keep working exactly as before.
    /// </summary>
    public class LudoBoardGenerator3D : MonoBehaviour
    {
        [Header("Grid Layout Config")]
        public float tileSize = 0.55f;
        public float tileHeight = 0.08f;
        public float tileSpacing = 0.04f;
        public float frameBorderPadding = 0.6f;

        [Header("Theme Materials (optional overrides)")]
        [Tooltip("Glass-look tile material. Leave empty to use a procedurally generated glowing glass default.")]
        public Material baseGlassMaterial;
        [Tooltip("Glow-rim material for tile/hub borders. Leave empty to use a procedurally generated default.")]
        public Material borderNeonMaterial;
        [Tooltip("Metallic frame/base material. Leave empty to use a procedurally generated default.")]
        public Material frameMetallicMaterial;
        [Tooltip("Center star hub material. Leave empty to use the same procedural glass as the tiles.")]
        public Material centerCrystalMaterial;

        [Header("Player Colors")]
        [Tooltip("Red slot's neon look - hot magenta, matching a cyan/magenta/yellow/green neon palette rather than literal red.")]
        public Color redNeon = new Color(1.0f, 0.08f, 0.85f, 1f);
        public Color greenNeon = new Color(0.15f, 1.0f, 0.4f, 1f);
        public Color yellowNeon = new Color(1.0f, 0.85f, 0.15f, 1f);
        [Tooltip("Blue slot's neon look - bright cyan, matching a cyan/magenta/yellow/green neon palette.")]
        public Color blueNeon = new Color(0.1f, 0.9f, 1.0f, 1f);
        public Color defaultTileColor = new Color(0.08f, 0.1f, 0.15f, 0.9f);

        [Header("Neon Glow Style")]
        [Tooltip("Neutral glow tint for common (non-owned) track tiles.")]
        public Color commonTileGlow = new Color(0.3f, 0.85f, 1.0f, 1f);
        [Tooltip("Emission strength for common track tiles.")]
        public float commonTileGlowIntensity = 1.1f;
        [Tooltip("Emission strength for each color's home-path lane tiles - punchier than common tiles.")]
        public float homePathGlowIntensity = 2.6f;
        [Tooltip("Emission strength for home-base yard sockets.")]
        public float homeBaseGlowIntensity = 1.8f;
        [Tooltip("Emission strength for the center star hub.")]
        public float centerGlowIntensity = 3.2f;

        [HideInInspector]
        public List<Tile3D> generatedTiles = new List<Tile3D>();

        // Regenerated fresh at the start of every GenerateBoardGeometry() run (see reset there) so
        // changing a "Theme Materials" override and re-running always picks up the latest choice, while
        // still only creating one Material instance per run rather than one per tile.
        private Material _glassTileMaterialCache;
        private Material _metalFrameMaterialCache;
        private Material _glowBorderMaterialCache;
        private static Mesh _sharedCubeMesh;

        [ContextMenu("Generate Full 3D Ludo Board")]
        public void GenerateBoardGeometry()
        {
            Transform existingHolder = transform.Find("LudoBoard_Geometry_Holder");
            if (existingHolder != null)
            {
                DestroyImmediate(existingHolder.gameObject);
            }

            _glassTileMaterialCache = null;
            _metalFrameMaterialCache = null;
            _glowBorderMaterialCache = null;

            GameObject boardHolder = new GameObject("LudoBoard_Geometry_Holder");
            boardHolder.transform.SetParent(this.transform);
            boardHolder.transform.localPosition = Vector3.zero;

            generatedTiles.Clear();

            float totalGridWidth = 15 * (tileSize + tileSpacing);
            float halfWidth = totalGridWidth / 2f - (tileSize / 2f);

            CreateBoardBaseAndFrame(boardHolder.transform, totalGridWidth);

            for (int x = 0; x < 15; x++)
            {
                for (int z = 0; z < 15; z++)
                {
                    Vector3 localPos = new Vector3((x * (tileSize + tileSpacing)) - halfWidth, tileHeight / 2f, (z * (tileSize + tileSpacing)) - halfWidth);
                    ProcessGridPosition(x, z, localPos, boardHolder.transform);
                }
            }

            CreateCenterPiece(boardHolder.transform);

            Debug.Log("<color=cyan><b>[LudoEmpire]</b> 3D Board Geometry successfully generated with Neon Glow / Sci-Fi Glass styling!</color>");
        }

        private void CreateBoardBaseAndFrame(Transform parent, float gridWidth)
        {
            GameObject basePlatform = GameObject.CreatePrimitive(PrimitiveType.Cube);
            basePlatform.name = "BoardBase_MainPlatform";
            basePlatform.transform.SetParent(parent);
            float baseWidth = gridWidth + frameBorderPadding;
            basePlatform.transform.localScale = new Vector3(baseWidth, 0.25f, baseWidth);
            basePlatform.transform.localPosition = new Vector3(0, -0.125f, 0);
            basePlatform.GetComponent<MeshRenderer>().sharedMaterial = GetMetalFrameMaterial();

            GameObject glassTop = GameObject.CreatePrimitive(PrimitiveType.Plane);
            glassTop.name = "BoardBase_GlassTopSurface";
            glassTop.transform.SetParent(parent);
            glassTop.transform.localScale = new Vector3(baseWidth * 0.1f, 1f, baseWidth * 0.1f);
            glassTop.transform.localPosition = new Vector3(0, 0.001f, 0);
            glassTop.GetComponent<MeshRenderer>().sharedMaterial = GetGlassTileMaterial();
        }

        private void ProcessGridPosition(int x, int z, Vector3 pos, Transform parent)
        {
            if (x >= 6 && x <= 8 && z >= 6 && z <= 8) return;

            if (IsHomeBase(x, z, out PlayerColor homeOwner))
            {
                CreateHomeBaseSocket(x, z, pos, parent, homeOwner);
                return;
            }

            if (IsTrackTile(x, z))
            {
                CreatePathTile(x, z, pos, parent);
            }
        }

        private bool IsHomeBase(int x, int z, out PlayerColor color)
        {
            color = PlayerColor.None;
            if (x < 6 && z > 8) { color = PlayerColor.Red; return true; }
            if (x > 8 && z > 8) { color = PlayerColor.Green; return true; }
            if (x > 8 && z < 6) { color = PlayerColor.Yellow; return true; }
            if (x < 6 && z < 6) { color = PlayerColor.Blue; return true; }
            return false;
        }

        private bool IsTrackTile(int x, int z)
        {
            return (x >= 6 && x <= 8) || (z >= 6 && z <= 8);
        }

        private void CreateHomeBaseSocket(int x, int z, Vector3 pos, Transform parent, PlayerColor color)
        {
            GameObject socketTile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            socketTile.name = $"HomeBaseSocket_{color}_{x}_{z}";
            socketTile.transform.SetParent(parent);
            socketTile.transform.localPosition = pos;
            Vector3 socketScale = new Vector3(tileSize * 0.85f, tileHeight * 0.5f, tileSize * 0.85f);
            socketTile.transform.localScale = socketScale;
            socketTile.GetComponent<MeshRenderer>().sharedMaterial = GetGlassTileMaterial();

            Tile3D tileComp = socketTile.AddComponent<Tile3D>();
            tileComp.tileType = TileType.HomeBase;
            tileComp.colorOwner = color;
            tileComp.gridCoordinate = new Vector2Int(x, z);

            MeshRenderer glowRenderer = AttachGlowBorder(socketTile.transform, socketScale);
            tileComp.SetGlowBorder(glowRenderer);

            Color themeColor = GetColorByEnum(color);
            tileComp.SetTileColor(defaultTileColor, themeColor, homeBaseGlowIntensity);

            generatedTiles.Add(tileComp);
        }

        private void CreatePathTile(int x, int z, Vector3 pos, Transform parent)
        {
            GameObject tileObj = new GameObject($"Tile_Track_{x}_{z}");
            tileObj.transform.SetParent(parent);
            tileObj.transform.localPosition = pos;

            Vector3 tileScale = new Vector3(tileSize, tileHeight, tileSize);

            MeshFilter filter = tileObj.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateBevelledPanelMesh(tileScale);

            MeshRenderer renderer = tileObj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetGlassTileMaterial();

            // CreatePrimitive(Cube) used to give this an implicit BoxCollider - now that the tile is a
            // custom bevelled mesh, add one explicitly so nothing relying on it (raycasts, physics) loses it.
            BoxCollider collider = tileObj.AddComponent<BoxCollider>();
            collider.size = tileScale;

            Tile3D tileComp = tileObj.AddComponent<Tile3D>();
            tileComp.gridCoordinate = new Vector2Int(x, z);

            MeshRenderer glowRenderer = AttachGlowBorder(tileObj.transform, tileScale);
            tileComp.SetGlowBorder(glowRenderer);

            if (z == 7 && x >= 1 && x <= 5) { tileComp.tileType = TileType.HomePath; tileComp.colorOwner = PlayerColor.Red; tileComp.SetTileColor(defaultTileColor, redNeon, homePathGlowIntensity); }
            else if (x == 7 && z >= 9 && z <= 13) { tileComp.tileType = TileType.HomePath; tileComp.colorOwner = PlayerColor.Green; tileComp.SetTileColor(defaultTileColor, greenNeon, homePathGlowIntensity); }
            else if (z == 7 && x >= 9 && x <= 13) { tileComp.tileType = TileType.HomePath; tileComp.colorOwner = PlayerColor.Yellow; tileComp.SetTileColor(defaultTileColor, yellowNeon, homePathGlowIntensity); }
            else if (x == 7 && z >= 1 && z <= 5) { tileComp.tileType = TileType.HomePath; tileComp.colorOwner = PlayerColor.Blue; tileComp.SetTileColor(defaultTileColor, blueNeon, homePathGlowIntensity); }
            else
            {
                tileComp.tileType = TileType.Common;
                tileComp.SetTileColor(defaultTileColor, commonTileGlow, commonTileGlowIntensity);
            }

            generatedTiles.Add(tileComp);
        }

        private void CreateCenterPiece(Transform parent)
        {
            GameObject centerObj = new GameObject("BoardCenter_NeonStarHub");
            centerObj.transform.SetParent(parent);
            centerObj.transform.localPosition = new Vector3(0, tileHeight * 0.8f, 0);
            float centerDiameter = 3 * (tileSize + tileSpacing);

            MeshFilter coreFilter = centerObj.AddComponent<MeshFilter>();
            coreFilter.sharedMesh = CreateStarBurstMesh(centerDiameter * 0.75f, tileHeight * 1.6f);
            MeshRenderer coreRenderer = centerObj.AddComponent<MeshRenderer>();
            coreRenderer.sharedMaterial = centerCrystalMaterial != null ? centerCrystalMaterial : GetGlassTileMaterial();

            Tile3D centerTile = centerObj.AddComponent<Tile3D>();
            centerTile.tileType = TileType.Center;

            // A wider, dimmer star halo just beneath the bright core reads as a soft glow pool around the hub.
            GameObject haloObj = new GameObject("CenterHalo");
            haloObj.transform.SetParent(centerObj.transform, false);
            haloObj.transform.localPosition = new Vector3(0f, -tileHeight * 0.5f, 0f);
            MeshFilter haloFilter = haloObj.AddComponent<MeshFilter>();
            haloFilter.sharedMesh = CreateStarBurstMesh(centerDiameter * 1.05f, tileHeight * 0.3f);
            MeshRenderer haloRenderer = haloObj.AddComponent<MeshRenderer>();
            haloRenderer.sharedMaterial = GetGlowBorderMaterial();
            centerTile.SetGlowBorder(haloRenderer);

            Color coreColor = Color.Lerp(blueNeon, redNeon, 0.5f); // cyan/magenta blend - a bright "energy" hue
            centerTile.SetTileColor(Color.white, coreColor, centerGlowIntensity);

            GameObject pointLightObj = new GameObject("Center_GlowPointLight");
            pointLightObj.transform.SetParent(centerObj.transform);
            pointLightObj.transform.localPosition = new Vector3(0, 1.2f, 0);
            Light pLight = pointLightObj.AddComponent<Light>();
            pLight.type = LightType.Point;
            pLight.range = 8.0f;
            pLight.intensity = 3.5f;
            pLight.color = Color.cyan;
        }

        private Color GetColorByEnum(PlayerColor c)
        {
            switch (c)
            {
                case PlayerColor.Red: return redNeon;
                case PlayerColor.Green: return greenNeon;
                case PlayerColor.Yellow: return yellowNeon;
                case PlayerColor.Blue: return blueNeon;
                default: return Color.white;
            }
        }

        // --- Neon material/mesh generation -----------------------------------------------------------

        private Material GetGlassTileMaterial()
        {
            if (baseGlassMaterial != null) return baseGlassMaterial;
            if (_glassTileMaterialCache == null)
            {
                _glassTileMaterialCache = CreateNeonMaterial("NeonGlassTile_Mat", new Color(0.05f, 0.08f, 0.14f, 0.55f),
                    metallic: 0.15f, smoothness: 0.92f, transparent: true);
            }
            return _glassTileMaterialCache;
        }

        private Material GetMetalFrameMaterial()
        {
            if (frameMetallicMaterial != null) return frameMetallicMaterial;
            if (_metalFrameMaterialCache == null)
            {
                _metalFrameMaterialCache = CreateNeonMaterial("NeonMetalFrame_Mat", new Color(0.08f, 0.09f, 0.11f, 1f),
                    metallic: 0.9f, smoothness: 0.8f, transparent: false);
            }
            return _metalFrameMaterialCache;
        }

        private Material GetGlowBorderMaterial()
        {
            if (borderNeonMaterial != null) return borderNeonMaterial;
            if (_glowBorderMaterialCache == null)
            {
                _glowBorderMaterialCache = CreateNeonMaterial("NeonGlowBorder_Mat", Color.black,
                    metallic: 0f, smoothness: 0.5f, transparent: false);
            }
            return _glowBorderMaterialCache;
        }

        /// <summary>Builds a URP Lit (falling back to Standard/Sprites-Default) material with the emission
        /// keyword baked in at the material level. <see cref="Tile3D.SetTileColor"/> varies _EmissionColor
        /// per-instance afterward via MaterialPropertyBlock - property blocks can change values but can't
        /// toggle shader keywords themselves, so without this every tile would silently render with no
        /// glow at all regardless of what color it's given.</summary>
        private static Material CreateNeonMaterial(string name, Color baseColor, float metallic, float smoothness, bool transparent)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader) { name = name };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", baseColor);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", baseColor);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));

            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", Color.black);

            if (transparent) MakeTransparent(mat);
            return mat;
        }

        /// <summary>URP Lit / built-in Standard transparent-surface setup, so the glass tiles actually
        /// alpha-blend instead of rendering as solid color with an ignored alpha channel.</summary>
        private static void MakeTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f);
                mat.SetFloat("_Blend", 0f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
            else if (mat.HasProperty("_Mode"))
            {
                mat.SetFloat("_Mode", 3f);
                mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                mat.SetInt("_ZWrite", 0);
                mat.EnableKeyword("_ALPHABLEND_ON");
                mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            }
        }

        private static Mesh GetSharedCubeMesh()
        {
            if (_sharedCubeMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _sharedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                DestroyImmediate(temp);
            }
            return _sharedCubeMesh;
        }

        /// <summary>Builds a "two-tier plateau" mesh - a full-footprint base slab with a slightly inset,
        /// thinner cap stacked on top - out of two transformed copies of Unity's own Cube mesh combined
        /// via CombineMeshes, so the raised/bevelled edge always renders correctly with zero hand-rolled
        /// triangle-winding risk. Reads as a sleek, slightly raised panel rather than a flat block.</summary>
        private static Mesh CreateBevelledPanelMesh(Vector3 size, float bevelRatio = 0.16f, float capHeightRatio = 0.4f)
        {
            float capHeight = size.y * capHeightRatio;
            float baseHeight = size.y - capHeight;
            float inset = Mathf.Min(size.x, size.z) * Mathf.Clamp01(bevelRatio);

            float capX = Mathf.Max(0.01f, size.x - inset * 2f);
            float capZ = Mathf.Max(0.01f, size.z - inset * 2f);

            var combine = new CombineInstance[2];
            combine[0].mesh = GetSharedCubeMesh();
            combine[0].transform = Matrix4x4.TRS(
                new Vector3(0f, -size.y * 0.5f + baseHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(size.x, Mathf.Max(0.001f, baseHeight), size.z));

            combine[1].mesh = GetSharedCubeMesh();
            combine[1].transform = Matrix4x4.TRS(
                new Vector3(0f, size.y * 0.5f - capHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(capX, Mathf.Max(0.001f, capHeight), capZ));

            Mesh mesh = new Mesh { name = "NeonTile_BevelMesh" };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Classic "8-point star from 2 overlapping squares" - one square plus a second rotated
        /// 45 degrees, both built from Unity's own Cube mesh (guaranteed-correct winding/normals) via
        /// CombineMeshes - same safe technique as the bevel above, rather than hand-rolled star-polygon
        /// vertex math that risks inside-out normals with no way to visually verify here.</summary>
        private static Mesh CreateStarBurstMesh(float size, float thickness)
        {
            var combine = new CombineInstance[2];
            combine[0].mesh = GetSharedCubeMesh();
            combine[0].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.identity, new Vector3(size, thickness, size));

            combine[1].mesh = GetSharedCubeMesh();
            combine[1].transform = Matrix4x4.TRS(Vector3.zero, Quaternion.Euler(0f, 45f, 0f), new Vector3(size, thickness, size));

            Mesh mesh = new Mesh { name = "NeonStarBurst_Mesh" };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Adds a slightly larger, thinner "GlowBorder" slab just beneath a tile so its edges peek
        /// out as a bright neon rim - returns the renderer so callers can wire it into
        /// <see cref="Tile3D.SetGlowBorder"/>.</summary>
        private MeshRenderer AttachGlowBorder(Transform tileTransform, Vector3 tileSize)
        {
            GameObject glowGO = new GameObject("GlowBorder");
            glowGO.transform.SetParent(tileTransform, false);
            glowGO.transform.localPosition = new Vector3(0f, -tileSize.y * 0.6f, 0f);
            glowGO.transform.localScale = new Vector3(tileSize.x * 1.14f, Mathf.Max(0.005f, tileSize.y * 0.3f), tileSize.z * 1.14f);

            MeshFilter filter = glowGO.AddComponent<MeshFilter>();
            filter.sharedMesh = GetSharedCubeMesh();

            MeshRenderer renderer = glowGO.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetGlowBorderMaterial();

            return renderer;
        }
    }
}
