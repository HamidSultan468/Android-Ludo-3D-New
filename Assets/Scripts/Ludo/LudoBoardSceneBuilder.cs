using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
#endif

namespace LudoEmpire.Ludo
{

#if UNITY_EDITOR
    /// <summary>
    /// Editor-only tool that procedurally builds a complete, playable 3D Ludo board into the active
    /// scene as a physical "Neon Glow / Sci-Fi Glass" table: a dark translucent glass BoardBase inside a
    /// bevelled, dark-chrome border frame, 52 bevelled TrackTiles, 4 home-stretch lanes, and 4 Player
    /// Home Bases all glowing in a cyan/magenta/yellow/green neon palette, a bevelled CenterHome hub with
    /// a bright cyan-magenta energy-core glow, 16 Tokens, 1 Physics Dice, a Dynamic Light Rig (directional
    /// sun/fill plus embedded cool-neon point lights), board logic + AI bots, a
    /// <see cref="LudoCameraController"/> that auto-frames whoever's turn it is, and a basic UI
    /// (including a theme-selector overlay). Every re-skinnable piece is tagged with a
    /// <see cref="LudoThemedElement"/> so 'Ludo Tools/Setup Theme Manager' can wire up runtime theme
    /// switching afterwards - re-theming only ever changes materials/prefabs, never this base geometry
    /// (this Neon Glow look is simply the new pre-theme default, same as "Acrylic Glass &amp; Gold" was
    /// before it - the separate purchasable Cyberpunk theme is unaffected either way).
    /// Sized for Android/mobile with full 3D character avatars standing on tiles (see <see cref="LayoutScale"/>).
    /// </summary>
    public static class LudoBoardSceneBuilder
    {
        private const string RootName = "LudoBoard_Root";

        // Mobile layout scale: every absolute-unit prop below (border, dice, lights, the placeholder
        // token size) is sized up from the original CellSize=1 baseline so a full 3D character avatar
        // can stand on a tile comfortably without overlapping its neighbours. The grid's cell COUNT is
        // unchanged (still a 15x15 grid / 52-cell shared path) - only each cell's physical size grows,
        // so none of LudoBoardLogic's path math (which only ever deals in cell indices) is affected.
        private const float LayoutScale = 1.6f;

        private const float CellSize = 1f * LayoutScale;
        private const float BoardThickness = 0.2f * LayoutScale;
        private const float TileHeight = 0.06f * LayoutScale;
        private const int CenterCell = 7;

        // The 52 shared-path cells, in relative-path order starting at Red's start square (index 0).
        // Grid is 15x15, (col, row), 0-indexed. Verified to match LudoBoardLogic's start offsets:
        // Red=0 -> (1,6), Green=13 -> (8,1), Yellow=26 -> (13,8), Blue=39 -> (6,13).
        private static readonly Vector2Int[] PathCells =
        {
            new Vector2Int(1,6), new Vector2Int(2,6), new Vector2Int(3,6), new Vector2Int(4,6), new Vector2Int(5,6),
            new Vector2Int(6,5), new Vector2Int(6,4), new Vector2Int(6,3), new Vector2Int(6,2), new Vector2Int(6,1), new Vector2Int(6,0),
            new Vector2Int(7,0),
            new Vector2Int(8,0),
            new Vector2Int(8,1), new Vector2Int(8,2), new Vector2Int(8,3), new Vector2Int(8,4), new Vector2Int(8,5),
            new Vector2Int(9,6), new Vector2Int(10,6), new Vector2Int(11,6), new Vector2Int(12,6), new Vector2Int(13,6), new Vector2Int(14,6),
            new Vector2Int(14,7),
            new Vector2Int(14,8),
            new Vector2Int(13,8), new Vector2Int(12,8), new Vector2Int(11,8), new Vector2Int(10,8), new Vector2Int(9,8),
            new Vector2Int(8,9), new Vector2Int(8,10), new Vector2Int(8,11), new Vector2Int(8,12), new Vector2Int(8,13), new Vector2Int(8,14),
            new Vector2Int(7,14),
            new Vector2Int(6,14),
            new Vector2Int(6,13), new Vector2Int(6,12), new Vector2Int(6,11), new Vector2Int(6,10), new Vector2Int(6,9),
            new Vector2Int(5,8), new Vector2Int(4,8), new Vector2Int(3,8), new Vector2Int(2,8), new Vector2Int(1,8), new Vector2Int(0,8),
            new Vector2Int(0,7),
            new Vector2Int(0,6),
        };

        private static readonly Dictionary<PlayerColor, Vector2Int[]> HomeStretchCells = new Dictionary<PlayerColor, Vector2Int[]>
        {
            { PlayerColor.Red, new[] { new Vector2Int(1,7), new Vector2Int(2,7), new Vector2Int(3,7), new Vector2Int(4,7), new Vector2Int(5,7), new Vector2Int(6,7) } },
            { PlayerColor.Green, new[] { new Vector2Int(7,1), new Vector2Int(7,2), new Vector2Int(7,3), new Vector2Int(7,4), new Vector2Int(7,5), new Vector2Int(7,6) } },
            { PlayerColor.Yellow, new[] { new Vector2Int(13,7), new Vector2Int(12,7), new Vector2Int(11,7), new Vector2Int(10,7), new Vector2Int(9,7), new Vector2Int(8,7) } },
            { PlayerColor.Blue, new[] { new Vector2Int(7,13), new Vector2Int(7,12), new Vector2Int(7,11), new Vector2Int(7,10), new Vector2Int(7,9), new Vector2Int(7,8) } },
        };

        // Top-left origin (col,row) of each color's 6x6 home-base quadrant.
        private static readonly Dictionary<PlayerColor, Vector2Int> YardOrigins = new Dictionary<PlayerColor, Vector2Int>
        {
            { PlayerColor.Red, new Vector2Int(0,0) },
            { PlayerColor.Green, new Vector2Int(9,0) },
            { PlayerColor.Yellow, new Vector2Int(9,9) },
            { PlayerColor.Blue, new Vector2Int(0,9) },
        };

        private static readonly Vector2Int[] YardSlotLocalOffsets =
        {
            new Vector2Int(1,1), new Vector2Int(4,1), new Vector2Int(1,4), new Vector2Int(4,4)
        };

        // Neon Glow palette: Red's slot renders as hot magenta and Blue's as bright cyan (rather than
        // literal red/blue) to hit the requested cyan/magenta/yellow/green neon set. Purely a display
        // color - LudoBoardLogic's PlayerColor enum values (and all game logic) are untouched by this.
        private static readonly Dictionary<PlayerColor, Color> PlayerColors = new Dictionary<PlayerColor, Color>
        {
            { PlayerColor.Red, new Color(1.0f, 0.08f, 0.75f) },
            { PlayerColor.Green, new Color(0.15f, 1.0f, 0.45f) },
            { PlayerColor.Yellow, new Color(1.0f, 0.85f, 0.15f) },
            { PlayerColor.Blue, new Color(0.1f, 0.85f, 1.0f) },
        };

        private static readonly Color NeutralTileColor = new Color(0.06f, 0.09f, 0.14f);
        private static readonly Color StarTileColor = new Color(1.0f, 0.95f, 0.55f);
        // Common tiles' dark base color makes a poor emission color (multiplying it by intensity stays
        // near-black); this soft cyan is what actually glows through the dark glass instead.
        private static readonly Color CommonTileGlowColor = new Color(0.3f, 0.75f, 0.95f);

        // Neon Glow emission strengths, tuned so start/home-stretch tiles (a color's own territory) read
        // brightest, the center hub brightest of all, and common tiles/home-base floors carry a softer
        // ambient glow rather than competing for attention.
        private const float StartTileGlowIntensity = 2.2f;
        private const float StarTileGlowIntensity = 1.6f;
        private const float CommonTileGlowIntensity = 0.9f;
        private const float HomeStretchGlowIntensity = 2.4f;
        private const float HomeBaseGlowIntensity = 1.1f;
        private const float CenterGlowIntensity = 2.8f;
        private const float TokenGlowIntensity = 1.8f;

        // Human-controlled by convention; AI bots are built for the other 3 colors (see BuildScene).
        private const PlayerColor HumanPlayerColor = PlayerColor.Red;

        // Must match LudoMainMenuSceneBuilder.ScenePath's scene name and be present in Build Settings for
        // LudoVictoryScreenController's Main Menu button to actually load it at runtime.
        private const string MainMenuSceneName = "MainMenu-new";

        [MenuItem("Ludo Tools/Build 3D Ludo Scene")]
        public static void BuildScene()
        {
            GameObject existingRoot = GameObject.Find(RootName);
            if (existingRoot != null)
            {
                bool rebuild = EditorUtility.DisplayDialog(
                    "Rebuild Ludo Board?",
                    $"A '{RootName}' already exists in this scene. Delete it and generate a fresh board?",
                    "Rebuild", "Cancel");

                if (!rebuild) return;
                Undo.DestroyObjectImmediate(existingRoot);
            }

            Undo.SetCurrentGroupName("Build 3D Ludo Scene");
            int undoGroup = Undo.GetCurrentGroup();

            GameObject root = new GameObject(RootName);
            Undo.RegisterCreatedObjectUndo(root, "Build 3D Ludo Scene");

            BuildBoardBase(root.transform);
            BuildBoardBorder(root.transform);
            Transform[] trackTiles = BuildTrackTiles(root.transform);
            var homeStretchTransforms = BuildHomeStretches(root.transform);
            var yardSlotTransforms = BuildHomeBases(root.transform);
            var tokenVisuals = BuildTokens(root.transform, yardSlotTransforms);
            BuildCenterHome(root.transform);

            LudoDiceRoller dice = BuildDice(root.transform);
            BuildLightRig(root.transform);
            EnsureCamera();

            GameObject logicGO = new GameObject("GameLogic");
            logicGO.transform.SetParent(root.transform, false);
            LudoBoardLogic board = logicGO.AddComponent<LudoBoardLogic>();
            ConfigureBoardLogic(board, trackTiles, homeStretchTransforms, yardSlotTransforms, dice);
            SetupDynamicCamera(board, dice, root.transform);

            // Human-controlled Red by convention; AI for the rest.
            BuildAIBot(logicGO.transform, board, dice, PlayerColor.Green, AIDifficulty.Medium);
            BuildAIBot(logicGO.transform, board, dice, PlayerColor.Yellow, AIDifficulty.Medium);
            BuildAIBot(logicGO.transform, board, dice, PlayerColor.Blue, AIDifficulty.Medium);

            var bindings = new List<LudoTokenVisualBinder.Binding>();
            foreach (var kvp in tokenVisuals)
            {
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    bindings.Add(new LudoTokenVisualBinder.Binding { color = kvp.Key, tokenId = i, visual = kvp.Value[i] });
                }
            }
            LudoTokenVisualBinder binder = logicGO.AddComponent<LudoTokenVisualBinder>();
            binder.Configure(board, bindings);

            EnsureCurrencyManager();
            EnsureMusicManager();
            BuildUI(root.transform, board, dice);

            Selection.activeGameObject = root;
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());

            Undo.CollapseUndoOperations(undoGroup);
            Debug.Log($"[LudoBoardSceneBuilder] Neon Glow / Sci-Fi Glass 3D Ludo scene generated at {LayoutScale}x mobile " +
                      "layout scale: bevelled dark-glass BoardBase inside a dark-chrome BoardBorder, 52 bevelled " +
                      "glowing TrackTiles, 4 home-stretch lanes, 4 Player Home Bases (cyan/magenta/yellow/green neon " +
                      "emission throughout), a bevelled CenterHome energy-core hub, 16 Tokens, 1 Physics Dice " +
                      "shared by the human and all 3 AI bots, a Dynamic Light Rig with embedded cool-neon corner/center " +
                      "point lights, a LudoCameraController that auto-frames whoever's turn it is, a color-coded turn/dice " +
                      "HUD, and a LudoSfxController with placeholder SFX hooks (dice, steps, captures, turns, " +
                      "victory) ready for audio clips. Run 'Ludo Tools/Setup Theme Manager' next to enable " +
                      "runtime Glass/Stone/Jungle/Cyberpunk switching.");
        }

        private static Transform BuildBoardBase(Transform parent)
        {
            GameObject root = new GameObject("BoardBase");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, -BoardThickness * 0.5f, 0f);

            Vector3 size = new Vector3(15f * CellSize, BoardThickness, 15f * CellSize);

            // A stable collider lives on the anchor itself so the physics dice always has something
            // solid to land on, independent of whatever visual the active theme swaps in underneath it.
            BoxCollider collider = root.AddComponent<BoxCollider>();
            collider.size = size;

            // Dark, glossy, semi-transparent sci-fi glass look for the default (pre-theme) board plate -
            // a dark backdrop is what lets the tiles' neon emission actually read as glowing.
            // Kept as a plain flat slab rather than the bevelled mesh below - at 15x15 units the
            // bevel's inset would sit well inside the tile ring and read as a mismatched step, since
            // tile/token positions are computed independently of the board mesh's own surface shape.
            Transform visual = CreateDefaultVisual(root.transform, PrimitiveType.Cube, size,
                new Color(0.04f, 0.06f, 0.1f, 0.55f), "BoardBase_Mat", metallic: 0.1f, smoothness: 0.9f, transparent: true);

            LudoThemedElement themed = root.AddComponent<LudoThemedElement>();
            themed.Configure(LudoThemeElementKind.BoardBase, visual, tintEnabled: false);

            return root.transform;
        }

        /// <summary>Builds a raised, bevelled, high-metallic dark-chrome picture-frame around the board's
        /// outer edge. Fixed (not a <see cref="LudoThemedElement"/>) - the frame is this board's physical
        /// identity and stays put across all 4 re-skinnable themes, matching how a real gaming table's
        /// metal frame wouldn't change just because the felt/tile insert does. Deliberately kept dark and
        /// non-emissive so the glowing tiles inside it read as the bright element, not the frame.</summary>
        private static void BuildBoardBorder(Transform parent)
        {
            GameObject group = new GameObject("BoardBorder");
            group.transform.SetParent(parent, false);

            Color frameColor = new Color(0.1f, 0.11f, 0.14f);
            const float barThickness = 0.6f * LayoutScale;
            const float barHeight = 0.18f * LayoutScale;
            float span = 15f * CellSize + barThickness; // extends half a bar-thickness beyond the board on each side
            float offset = 15f * CellSize * 0.5f + barThickness * 0.5f;
            float y = barHeight * 0.5f - 0.02f * LayoutScale; // sits flush with the board's top surface, trimmed slightly proud of it

            BuildBorderBar(group.transform, "Border_North", new Vector3(0f, y, offset), new Vector3(span, barHeight, barThickness), frameColor);
            BuildBorderBar(group.transform, "Border_South", new Vector3(0f, y, -offset), new Vector3(span, barHeight, barThickness), frameColor);
            BuildBorderBar(group.transform, "Border_East", new Vector3(offset, y, 0f), new Vector3(barThickness, barHeight, span), frameColor);
            BuildBorderBar(group.transform, "Border_West", new Vector3(-offset, y, 0f), new Vector3(barThickness, barHeight, span), frameColor);
        }

        private static void BuildBorderBar(Transform parent, string name, Vector3 localPosition, Vector3 size, Color color)
        {
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;

            CreateBevelledVisual(root.transform, size, color, $"{name}_Mat", metallic: 0.92f, smoothness: 0.85f, bevelRatio: 0.1f);
        }

        private static Transform[] BuildTrackTiles(Transform parent)
        {
            GameObject group = new GameObject("TrackTiles");
            group.transform.SetParent(parent, false);

            var startIndices = new HashSet<int>();
            var starIndices = new HashSet<int>();
            var startColorByIndex = new Dictionary<int, PlayerColor>();
            foreach (PlayerColor color in Enum.GetValues(typeof(PlayerColor)))
            {
                int start = color switch
                {
                    PlayerColor.Red => 0,
                    PlayerColor.Green => 13,
                    PlayerColor.Yellow => 26,
                    PlayerColor.Blue => 39,
                    _ => 0
                };
                startIndices.Add(start);
                startColorByIndex[start] = color;
                starIndices.Add((start + 8) % LudoBoardLogic.CommonPathLength);
            }

            Vector3 tileScale = new Vector3(CellSize * 0.92f, TileHeight, CellSize * 0.92f);
            Transform[] tiles = new Transform[PathCells.Length];

            for (int i = 0; i < PathCells.Length; i++)
            {
                Vector3 pos = CellToWorld(PathCells[i]);

                GameObject tileRoot = new GameObject($"TrackTile_{i:00}");
                tileRoot.transform.SetParent(group.transform, false);
                tileRoot.transform.localPosition = new Vector3(pos.x, TileHeight * 0.5f, pos.z);

                PlayerColor? owner = null;
                Color tint = NeutralTileColor;
                Color glowColor = CommonTileGlowColor;
                float glowIntensity = CommonTileGlowIntensity;
                if (startIndices.Contains(i))
                {
                    owner = startColorByIndex[i];
                    tint = PlayerColors[owner.Value];
                    glowColor = tint;
                    glowIntensity = StartTileGlowIntensity;
                }
                else if (starIndices.Contains(i))
                {
                    tint = StarTileColor;
                    glowColor = tint;
                    glowIntensity = StarTileGlowIntensity;
                }

                Transform visual = CreateBevelledVisual(tileRoot.transform, tileScale, tint, $"TrackTile_{i:00}_Mat", metallic: 0.15f, smoothness: 0.75f,
                    emissionColor: glowColor, emissionIntensity: glowIntensity);

                LudoThemedElement themed = tileRoot.AddComponent<LudoThemedElement>();
                themed.Configure(LudoThemeElementKind.TrackTile, visual, owner, tint);

                tiles[i] = tileRoot.transform;
            }

            return tiles;
        }

        private static Dictionary<PlayerColor, Transform[]> BuildHomeStretches(Transform parent)
        {
            GameObject group = new GameObject("HomeStretches");
            group.transform.SetParent(parent, false);

            Vector3 tileScale = new Vector3(CellSize * 0.92f, TileHeight, CellSize * 0.92f);
            var result = new Dictionary<PlayerColor, Transform[]>();

            foreach (var kvp in HomeStretchCells)
            {
                PlayerColor color = kvp.Key;
                GameObject colorGroup = new GameObject($"HomeStretch_{color}");
                colorGroup.transform.SetParent(group.transform, false);

                Transform[] tiles = new Transform[kvp.Value.Length];
                for (int i = 0; i < kvp.Value.Length; i++)
                {
                    Vector3 pos = CellToWorld(kvp.Value[i]);

                    GameObject tileRoot = new GameObject($"{color}Home_{i}");
                    tileRoot.transform.SetParent(colorGroup.transform, false);
                    tileRoot.transform.localPosition = new Vector3(pos.x, TileHeight * 0.5f, pos.z);

                    Transform visual = CreateBevelledVisual(tileRoot.transform, tileScale, PlayerColors[color], $"{color}Home_{i}_Mat", metallic: 0.15f, smoothness: 0.75f,
                        emissionColor: PlayerColors[color], emissionIntensity: HomeStretchGlowIntensity);

                    LudoThemedElement themed = tileRoot.AddComponent<LudoThemedElement>();
                    themed.Configure(LudoThemeElementKind.TrackTile, visual, color, PlayerColors[color]);

                    tiles[i] = tileRoot.transform;
                }
                result[color] = tiles;
            }
            return result;
        }

        /// <summary>Builds the 4 Player Home Bases (floor + 4 slot locators each). Slot locators are plain,
        /// non-themed markers - the actual visible pieces live in the separate "Tokens" group built by <see cref="BuildTokens"/>.</summary>
        private static Dictionary<PlayerColor, Transform[]> BuildHomeBases(Transform parent)
        {
            GameObject group = new GameObject("HomeBases");
            group.transform.SetParent(parent, false);

            var slotTransforms = new Dictionary<PlayerColor, Transform[]>();

            foreach (var kvp in YardOrigins)
            {
                PlayerColor color = kvp.Key;
                Vector2Int origin = kvp.Value;

                GameObject baseGO = new GameObject($"HomeBase_{color}");
                baseGO.transform.SetParent(group.transform, false);

                Vector2Int floorCell = origin + new Vector2Int(2, 2);
                Vector3 floorPos = CellToWorld(floorCell);

                GameObject floorRoot = new GameObject("Floor");
                floorRoot.transform.SetParent(baseGO.transform, false);
                floorRoot.transform.localPosition = new Vector3(floorPos.x, TileHeight * 0.3f, floorPos.z);

                Vector3 floorScale = new Vector3(5.5f * CellSize, TileHeight * 0.6f, 5.5f * CellSize);
                // Dark glass floor subtly tinted toward the player's neon hue, with the full-saturation
                // color glowing through via emission - matches the "dark quadrant, bright accent" look
                // rather than a pale pastel wash.
                Color floorTint = Color.Lerp(NeutralTileColor, PlayerColors[color], 0.25f);
                Transform floorVisual = CreateBevelledVisual(floorRoot.transform, floorScale, floorTint, $"HomeBase_{color}_Mat", metallic: 0.1f, smoothness: 0.6f,
                    emissionColor: PlayerColors[color], emissionIntensity: HomeBaseGlowIntensity);

                LudoThemedElement floorThemed = floorRoot.AddComponent<LudoThemedElement>();
                floorThemed.Configure(LudoThemeElementKind.HomeBaseFloor, floorVisual, color, floorTint);

                GameObject slotsGroup = new GameObject("Slots");
                slotsGroup.transform.SetParent(baseGO.transform, false);

                Transform[] slots = new Transform[YardSlotLocalOffsets.Length];
                for (int i = 0; i < YardSlotLocalOffsets.Length; i++)
                {
                    Vector2Int cell = origin + YardSlotLocalOffsets[i];
                    Vector3 worldPos = CellToWorld(cell);

                    GameObject slotGO = new GameObject($"Slot_{i}");
                    slotGO.transform.SetParent(slotsGroup.transform, false);
                    slotGO.transform.localPosition = new Vector3(worldPos.x, TileHeight, worldPos.z);
                    slots[i] = slotGO.transform;
                }

                slotTransforms[color] = slots;
            }

            return slotTransforms;
        }

        /// <summary>Builds the 16 Tokens (gotis) as their own top-level group, each initially placed at its
        /// home-base slot's world position. Each token's root Transform is the stable anchor <see cref="LudoBoardLogic"/>
        /// moves via <see cref="LudoToken.Visual"/> - only its "Visual" child is ever swapped by the theme system.</summary>
        private static Dictionary<PlayerColor, Transform[]> BuildTokens(Transform parent, Dictionary<PlayerColor, Transform[]> yardSlots)
        {
            GameObject group = new GameObject("Tokens");
            group.transform.SetParent(parent, false);

            Vector3 tokenScale = new Vector3(0.35f, 0.25f, 0.35f) * LayoutScale;
            var tokenVisuals = new Dictionary<PlayerColor, Transform[]>();

            foreach (var kvp in yardSlots)
            {
                PlayerColor color = kvp.Key;
                Transform[] slots = kvp.Value;
                Transform[] tokens = new Transform[slots.Length];

                for (int i = 0; i < slots.Length; i++)
                {
                    GameObject tokenRoot = new GameObject($"Token_{color}_{i}");
                    tokenRoot.transform.SetParent(group.transform, false);
                    tokenRoot.transform.position = slots[i] != null ? slots[i].position + new Vector3(0f, 0.25f * LayoutScale, 0f) : Vector3.up * (0.25f * LayoutScale);

                    GameObject characterPrefab = (int)color < CharacterModelPrefabsByColor.Length ? CharacterModelPrefabsByColor[(int)color] : null;
                    Transform visual = characterPrefab != null
                        ? SpawnCharacterModel(characterPrefab, tokenRoot.transform)
                        : CreateDefaultVisual(tokenRoot.transform, PrimitiveType.Cylinder, tokenScale, PlayerColors[color], $"Token_{color}_{i}_Mat",
                            emissionColor: PlayerColors[color], emissionIntensity: TokenGlowIntensity);

                    LudoThemedElement themed = tokenRoot.AddComponent<LudoThemedElement>();
                    themed.Configure(LudoThemeElementKind.Token, visual, color, PlayerColors[color]);

                    // Tap target: CreateDefaultVisual/SpawnCharacterModel strip their own colliders, so the
                    // token has none otherwise - without this LudoHumanTurnAutoResolver's raycast can't
                    // pick this token and the human's turn falls back to the timeout auto-pick every time.
                    BoxCollider tapCollider = tokenRoot.AddComponent<BoxCollider>();
                    Renderer tokenRenderer = visual != null ? visual.GetComponentInChildren<Renderer>() : null;
                    if (tokenRenderer != null)
                    {
                        tapCollider.center = tokenRoot.transform.InverseTransformPoint(tokenRenderer.bounds.center);
                        Vector3 worldSize = tokenRoot.transform.InverseTransformVector(tokenRenderer.bounds.size);
                        tapCollider.size = new Vector3(Mathf.Abs(worldSize.x), Mathf.Abs(worldSize.y), Mathf.Abs(worldSize.z)) * 1.25f;
                    }
                    else
                    {
                        tapCollider.size = tokenScale * 1.5f;
                    }

                    tokens[i] = tokenRoot.transform;
                }

                tokenVisuals[color] = tokens;
            }

            return tokenVisuals;
        }

        /// <summary>Builds the decorative CenterHome piece at the board's center cell. Purely visual -
        /// finished tokens simply stop animating at the last home-stretch waypoint, right next to it.</summary>
        private static Transform BuildCenterHome(Transform parent)
        {
            GameObject root = new GameObject("CenterHome");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = new Vector3(0f, TileHeight * 0.5f, 0f); // board center, cell (7,7)

            Vector3 scale = new Vector3(CellSize * 2.6f, TileHeight * 2.5f, CellSize * 2.6f);
            // Cyan/magenta blend - a bright "energy core" hue distinct from any single player's color,
            // since every player's home stretch converges here.
            Color centerGlow = Color.Lerp(PlayerColors[PlayerColor.Blue], PlayerColors[PlayerColor.Red], 0.5f);
            Transform visual = CreateBevelledVisual(root.transform, scale, Color.white, "CenterHome_Mat", metallic: 0.35f, smoothness: 0.9f,
                emissionColor: centerGlow, emissionIntensity: CenterGlowIntensity);

            LudoThemedElement themed = root.AddComponent<LudoThemedElement>();
            themed.Configure(LudoThemeElementKind.CenterHome, visual, tintEnabled: false);

            return root.transform;
        }

        private static LudoDiceRoller BuildDice(Transform parent)
        {
            GameObject diceGO = GameObject.CreatePrimitive(PrimitiveType.Cube);
            diceGO.name = "Dice";
            diceGO.transform.SetParent(parent, false);

            Vector3 spawnLocalPos = new Vector3(0f, 2.5f * LayoutScale, 0f);
            diceGO.transform.localPosition = spawnLocalPos;
            diceGO.transform.localScale = Vector3.one * (0.6f * LayoutScale);

            Renderer renderer = diceGO.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = CreateColorMaterial(Color.white, "Dice_Mat");

            Rigidbody rb = diceGO.AddComponent<Rigidbody>();
            // Mass scales with the dice's physical size so its throw behaves the same relative to its
            // own scale as before (LudoDiceRoller's default launch force/torque are tuned to match).
            rb.mass = 0.2f * LayoutScale;
            rb.linearDamping = 0.3f;
            rb.angularDamping = 0.3f;

            // The dice re-skins its own MeshFilter/Renderer in place (no visual-slot child) so its
            // Rigidbody/Collider/LudoDiceRoller never get recreated by a theme switch.
            LudoThemedElement themed = diceGO.AddComponent<LudoThemedElement>();
            themed.Configure(LudoThemeElementKind.Dice, null, tintEnabled: false);

            // A dedicated, never-swapped spawn marker so re-theming the dice's mesh/material can
            // never disturb where LudoDiceRoller resets it before each throw.
            GameObject spawnGO = new GameObject("DiceSpawnPoint");
            spawnGO.transform.SetParent(parent, false);
            spawnGO.transform.localPosition = spawnLocalPos;

            LudoDiceRoller dice = diceGO.AddComponent<LudoDiceRoller>();

            var so = new SerializedObject(dice);
            SerializedProperty spawnProp = so.FindProperty("spawnPoint");
            if (spawnProp != null)
            {
                spawnProp.objectReferenceValue = spawnGO.transform;
            }
            else
            {
                Debug.LogWarning("[LudoBoardSceneBuilder] LudoDiceRoller's 'spawnPoint' field name changed; " +
                                  "update BuildDice() to match. The dice will still roll, just without an assigned reset point.");
            }

            // Scale the default launch force/torque by the same factor as the dice's mass above, so a
            // bigger/heavier mobile-scale dice throws with the same relative punch as the original tuning.
            SerializedProperty minForceProp = so.FindProperty("minForce");
            SerializedProperty maxForceProp = so.FindProperty("maxForce");
            SerializedProperty minTorqueProp = so.FindProperty("minTorque");
            SerializedProperty maxTorqueProp = so.FindProperty("maxTorque");
            if (minForceProp != null) minForceProp.floatValue *= LayoutScale;
            if (maxForceProp != null) maxForceProp.floatValue *= LayoutScale;
            if (minTorqueProp != null) minTorqueProp.floatValue *= LayoutScale;
            if (maxTorqueProp != null) maxTorqueProp.floatValue *= LayoutScale;

            so.ApplyModifiedPropertiesWithoutUndo();

            return dice;
        }

        /// <summary>Builds the Dynamic Light Rig (sun + fill light, plus embedded point lights) parented
        /// under the board root, so <see cref="LudoThemeManager"/> always has stable lights to control
        /// regardless of what else is in the scene.</summary>
        private static void BuildLightRig(Transform parent)
        {
            GameObject rig = new GameObject("LightRig");
            rig.transform.SetParent(parent, false);

            GameObject sunGO = new GameObject("SunLight");
            sunGO.transform.SetParent(rig.transform, false);
            sunGO.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            Light sun = sunGO.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = Color.white;
            sun.intensity = 1.2f;
            sun.shadows = LightShadows.Soft;

            GameObject fillGO = new GameObject("FillLight");
            fillGO.transform.SetParent(rig.transform, false);
            fillGO.transform.localPosition = new Vector3(0f, 6f * LayoutScale, 0f);
            fillGO.transform.rotation = Quaternion.Euler(70f, 150f, 0f);
            Light fill = fillGO.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.color = new Color(0.85f, 0.9f, 1f);
            fill.intensity = 0.35f;
            fill.shadows = LightShadows.None;

            BuildEmbeddedPointLights(rig.transform);
        }

        /// <summary>Small cool-neon point lights embedded around the board - 4 corner accents plus a
        /// brighter magenta-white center glow above <see cref="CenterHome"/> - so the dark sci-fi glass
        /// board catches specular highlights beyond what the directional sun/fill alone would give it,
        /// and the neon tiles' own emission gets a matching ambient bounce to sit in.</summary>
        private static void BuildEmbeddedPointLights(Transform rigParent)
        {
            Color neonAccent = new Color(0.5f, 0.85f, 1f);
            float corner = 15f * CellSize * 0.5f - 1f * LayoutScale;
            float cornerHeight = 1.4f * LayoutScale;
            Vector3[] cornerPositions =
            {
                new Vector3(corner, cornerHeight, corner),
                new Vector3(-corner, cornerHeight, corner),
                new Vector3(corner, cornerHeight, -corner),
                new Vector3(-corner, cornerHeight, -corner),
            };

            for (int i = 0; i < cornerPositions.Length; i++)
            {
                GameObject lightGO = new GameObject($"CornerLight_{i}");
                lightGO.transform.SetParent(rigParent, false);
                lightGO.transform.localPosition = cornerPositions[i];

                Light light = lightGO.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = neonAccent;
                light.intensity = 1.4f * LayoutScale; // brighter to carry across the larger range below
                light.range = 5f * LayoutScale;
                light.shadows = LightShadows.None;
            }

            GameObject centerGO = new GameObject("CenterGlow");
            centerGO.transform.SetParent(rigParent, false);
            centerGO.transform.localPosition = new Vector3(0f, 1.6f * LayoutScale, 0f);

            Light centerLight = centerGO.AddComponent<Light>();
            centerLight.type = LightType.Point;
            centerLight.color = new Color(1f, 0.6f, 0.95f);
            centerLight.intensity = 2.2f * LayoutScale;
            centerLight.range = 6f * LayoutScale;
            centerLight.shadows = LightShadows.None;
        }

        private static void EnsureCamera()
        {
            if (Camera.main == null)
            {
                GameObject camGO = new GameObject("Main Camera");
                camGO.tag = "MainCamera";
                Camera cam = camGO.AddComponent<Camera>();
                camGO.transform.position = new Vector3(0f, 16f * LayoutScale, -10f * LayoutScale);
                camGO.transform.rotation = Quaternion.Euler(58f, 0f, 0f);
                cam.nearClipPlane = 0.3f;
                Undo.RegisterCreatedObjectUndo(camGO, "Build 3D Ludo Scene");
            }
        }

        /// <summary>Attaches (or reuses) a <see cref="LudoCameraController"/> on the main camera and wires
        /// it to this board/dice, so the camera automatically zooms in on whoever's turn it is and pulls
        /// back to an overview when idle - no manual scene setup required after 'Build 3D Ludo Scene'.</summary>
        private static void SetupDynamicCamera(LudoBoardLogic board, LudoDiceRoller dice, Transform boardRoot)
        {
            Camera cam = Camera.main;
            if (cam == null)
            {
                Debug.LogWarning("[LudoBoardSceneBuilder] No main camera found; skipping LudoCameraController setup.");
                return;
            }

            LudoCameraController controller = cam.GetComponent<LudoCameraController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<LudoCameraController>(cam.gameObject);
            }

            // Pass the board root so the controller frames the whole board on Start without a name lookup.
            controller.Configure(board, dice, boardRoot);
        }

        /// <summary>Builds a dedicated "SfxController" object carrying an <see cref="AudioSource"/> and
        /// <see cref="LudoSfxController"/>, wired to the board/dice so dice, turn, step, capture, and
        /// victory events all have a ready SFX hook - clips start unassigned (silent) as placeholders;
        /// drop AudioClips onto the component in the Inspector to enable each sound, no code changes needed.</summary>
        private static void SetupSfx(Transform parent, LudoBoardLogic board, LudoDiceRoller dice)
        {
            GameObject sfxGO = new GameObject("SfxController");
            sfxGO.transform.SetParent(parent, false);

            AudioSource source = sfxGO.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.spatialBlend = 0f; // 2D - a mobile board game's SFX shouldn't attenuate with camera distance

            LudoSfxController sfx = sfxGO.AddComponent<LudoSfxController>();
            sfx.Configure(board, dice);
        }

        /// <summary>Creates the CurrencyManager singleton as its own scene-root object (deliberately NOT
        /// parented under the board root) - CurrencyManager.Awake() calls DontDestroyOnLoad on itself,
        /// which only works for a root GameObject; nesting it under LudoBoard_Root would silently fail
        /// that call (logging a warning) every time this scene builds.</summary>
        private static void EnsureCurrencyManager()
        {
            if (UnityEngine.Object.FindAnyObjectByType<CurrencyManager>() != null) return;

            GameObject currencyGO = new GameObject("CurrencyManager");
            currencyGO.AddComponent<CurrencyManager>();
            Undo.RegisterCreatedObjectUndo(currencyGO, "Build 3D Ludo Scene");
        }

        /// <summary>Creates the LudoMusicManager singleton as its own scene-root object, for the same
        /// DontDestroyOnLoad reason as <see cref="EnsureCurrencyManager"/>. Skipped if this scene is
        /// currently open together with one that already has a LudoMusicManager (e.g. testing both
        /// additively loaded at once in the Editor); at actual runtime, if the Main Menu scene's own copy
        /// already survived a scene switch via DontDestroyOnLoad, LudoMusicManager.Awake()'s own singleton
        /// guard destroys this scene's redundant instance instead - either way, exactly one persists.</summary>
        private static void EnsureMusicManager()
        {
            if (UnityEngine.Object.FindAnyObjectByType<LudoMusicManager>() != null) return;

            GameObject musicGO = new GameObject("MusicManager");
            musicGO.AddComponent<AudioSource>();
            musicGO.AddComponent<LudoMusicManager>();
            Undo.RegisterCreatedObjectUndo(musicGO, "Build 3D Ludo Scene");
        }

        /// <summary>Builds an AI opponent and wires it to the board plus the same shared physical dice the
        /// human rolls, so every turn - human or AI - actually rolls the one visible die (consistent dice
        /// animation, camera roll-reactive zoom, and dice SFX hooks all fire for AI turns too, instead of
        /// AI silently picking a random number no one sees).</summary>
        private static void BuildAIBot(Transform parent, LudoBoardLogic board, LudoDiceRoller dice, PlayerColor color, AIDifficulty difficulty)
        {
            GameObject botGO = new GameObject($"AIBot_{color}");
            botGO.transform.SetParent(parent, false);
            LudoAIBot bot = botGO.AddComponent<LudoAIBot>();

            var so = new SerializedObject(bot);
            SerializedProperty boardProp = so.FindProperty("board");
            SerializedProperty colorProp = so.FindProperty("aiColor");
            SerializedProperty difficultyProp = so.FindProperty("difficulty");
            SerializedProperty diceProp = so.FindProperty("diceRoller");

            if (boardProp == null || colorProp == null || difficultyProp == null)
            {
                Debug.LogError("[LudoBoardSceneBuilder] LudoAIBot's serialized field names have changed; " +
                                "update BuildAIBot() to match (expected 'board', 'aiColor', 'difficulty').");
                return;
            }

            boardProp.objectReferenceValue = board;
            colorProp.enumValueIndex = (int)color;
            difficultyProp.enumValueIndex = (int)difficulty;

            if (diceProp != null)
            {
                diceProp.objectReferenceValue = dice;
            }
            else
            {
                Debug.LogWarning("[LudoBoardSceneBuilder] LudoAIBot's 'diceRoller' field name changed; " +
                                  "update BuildAIBot() to match. This bot will fall back to picking a random " +
                                  "value with no visible dice roll.");
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(bot);
        }

        private static void ConfigureBoardLogic(
            LudoBoardLogic board,
            Transform[] trackTiles,
            Dictionary<PlayerColor, Transform[]> homeStretchTransforms,
            Dictionary<PlayerColor, Transform[]> yardSlotTransforms,
            LudoDiceRoller dice)
        {
            var so = new SerializedObject(board);

            SerializedProperty commonProp = so.FindProperty("commonPathWaypoints");
            SerializedProperty homePropCheck = so.FindProperty("homeStretches");
            SerializedProperty yardPropCheck = so.FindProperty("yardSlots");

            if (commonProp == null || homePropCheck == null || yardPropCheck == null)
            {
                Debug.LogError("[LudoBoardSceneBuilder] LudoBoardLogic's serialized field names have changed; " +
                                "update ConfigureBoardLogic() to match (expected 'commonPathWaypoints', 'homeStretches', 'yardSlots').");
                return;
            }

            commonProp.arraySize = trackTiles.Length;
            for (int i = 0; i < trackTiles.Length; i++)
            {
                commonProp.GetArrayElementAtIndex(i).objectReferenceValue = trackTiles[i];
            }

            SerializedProperty homeProp = so.FindProperty("homeStretches");
            homeProp.arraySize = 4;
            for (int c = 0; c < 4; c++)
            {
                PlayerColor color = (PlayerColor)c;
                SerializedProperty waypointsProp = homeProp.GetArrayElementAtIndex(c).FindPropertyRelative("waypoints");
                Transform[] stretch = homeStretchTransforms[color];
                waypointsProp.arraySize = stretch.Length;
                for (int i = 0; i < stretch.Length; i++)
                {
                    waypointsProp.GetArrayElementAtIndex(i).objectReferenceValue = stretch[i];
                }
            }

            SerializedProperty yardProp = so.FindProperty("yardSlots");
            yardProp.arraySize = 4;
            for (int c = 0; c < 4; c++)
            {
                PlayerColor color = (PlayerColor)c;
                SerializedProperty waypointsProp = yardProp.GetArrayElementAtIndex(c).FindPropertyRelative("waypoints");
                Transform[] slots = yardSlotTransforms[color];
                waypointsProp.arraySize = slots.Length;
                for (int i = 0; i < slots.Length; i++)
                {
                    waypointsProp.GetArrayElementAtIndex(i).objectReferenceValue = slots[i];
                }
            }

            SerializedProperty diceProp = so.FindProperty("diceRoller");
            if (diceProp != null) diceProp.objectReferenceValue = dice;

            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(board);
        }

        private static void BuildUI(Transform parent, LudoBoardLogic board, LudoDiceRoller dice)
        {
            GameObject canvasGO = new GameObject("Canvas");
            canvasGO.transform.SetParent(parent, false);
            Canvas canvas = canvasGO.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            CanvasScaler scaler = canvasGO.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);

            canvasGO.AddComponent<GraphicRaycaster>();

            if (UnityEngine.Object.FindAnyObjectByType<EventSystem>() == null)
            {
                GameObject eventSystemGO = new GameObject("EventSystem");
                eventSystemGO.transform.SetParent(parent, false);
                eventSystemGO.AddComponent<EventSystem>();
                try
                {
                    eventSystemGO.AddComponent<StandaloneInputModule>();
                }
                catch (Exception e)
                {
                    Debug.LogWarning($"[LudoBoardSceneBuilder] Could not add StandaloneInputModule automatically: {e.Message}. " +
                                      "Add an input module manually (legacy or Input System) if UI clicks don't register.");
                }
            }

            Text turnText = CreateLabel(canvasGO.transform, "TurnIndicatorText", new Vector2(0f, -80f), new Vector2(600f, 100f),
                new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), "Red's Turn", 42);

            Text coinsText = CreateLabel(canvasGO.transform, "CoinsText", new Vector2(-40f, -40f), new Vector2(320f, 80f),
                new Vector2(1f, 1f), new Vector2(1f, 1f), "Coins: 0", 34);

            Button rollButton = CreateButton(canvasGO.transform, "RollDiceButton", new Vector2(0f, 140f), new Vector2(320f, 140f), "Roll Dice");

            Text diceValueText = CreateLabel(canvasGO.transform, "DiceValueText", new Vector2(0f, 300f), new Vector2(300f, 60f),
                new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), string.Empty, 32);

            LudoRollButtonController rollController = canvasGO.AddComponent<LudoRollButtonController>();
            rollController.Configure(board, dice, rollButton, HumanPlayerColor);
            UnityEventTools.AddPersistentListener(rollButton.onClick, rollController.RollClicked);

            LudoHudBinder hud = canvasGO.AddComponent<LudoHudBinder>();
            hud.Configure(board, dice, turnText, coinsText, diceValueText, HumanPlayerColor);

            LudoHumanTurnAutoResolver autoResolver = canvasGO.AddComponent<LudoHumanTurnAutoResolver>();
            autoResolver.Configure(board, HumanPlayerColor);

            SetupSfx(parent, board, dice);
            BuildVictoryScreen(canvasGO.transform, board);

            LudoGameModeController modeController = canvasGO.AddComponent<LudoGameModeController>();
            modeController.Configure(rollController, hud, autoResolver);

            BuildThemeSelectorPanel(canvasGO.transform);
            BuildZoomControls(canvasGO.transform);
        }

        /// <summary>Bottom-right +/- and "Fit" buttons wired to the <see cref="LudoCameraController"/> on
        /// the main camera, so the player can override the auto follow-cam. (Pinch-to-zoom also works with
        /// no UI at all.)</summary>
        private static void BuildZoomControls(Transform canvasParent)
        {
            LudoCameraController camController = Camera.main != null ? Camera.main.GetComponent<LudoCameraController>() : null;
            if (camController == null)
            {
                Debug.LogWarning("[LudoBoardSceneBuilder] No LudoCameraController on Camera.main; skipping zoom buttons (pinch-to-zoom still works).");
                return;
            }

            Button zoomIn = CreateButton(canvasParent, "ZoomInButton", Vector2.zero, new Vector2(120f, 120f), "+");
            Button zoomOut = CreateButton(canvasParent, "ZoomOutButton", Vector2.zero, new Vector2(120f, 120f), "−");
            Button zoomFit = CreateButton(canvasParent, "ZoomFitButton", Vector2.zero, new Vector2(120f, 76f), "Fit");

            AnchorBottomRight(zoomIn.GetComponent<RectTransform>(), new Vector2(-32f, 320f));
            AnchorBottomRight(zoomOut.GetComponent<RectTransform>(), new Vector2(-32f, 188f));
            AnchorBottomRight(zoomFit.GetComponent<RectTransform>(), new Vector2(-32f, 100f));

            UnityEventTools.AddPersistentListener(zoomIn.onClick, camController.ZoomIn);
            UnityEventTools.AddPersistentListener(zoomOut.onClick, camController.ZoomOut);
            UnityEventTools.AddPersistentListener(zoomFit.onClick, camController.ResetZoom);
        }

        private static void AnchorBottomRight(RectTransform rt, Vector2 anchoredPos)
        {
            rt.anchorMin = new Vector2(1f, 0f);
            rt.anchorMax = new Vector2(1f, 0f);
            rt.pivot = new Vector2(1f, 0f);
            rt.anchoredPosition = anchoredPos;
        }

        /// <summary>Builds the full-screen Victory panel - hidden by default, shown automatically by
        /// <see cref="LudoVictoryScreenController"/> the instant a player gets all 4 tokens home. Its
        /// background is a raycast-blocking overlay, so once it appears the board/Roll button underneath
        /// can no longer be interacted with.</summary>
        private static void BuildVictoryScreen(Transform canvasParent, LudoBoardLogic board)
        {
            GameObject panel = new GameObject("VictoryPanel", typeof(RectTransform));
            panel.transform.SetParent(canvasParent, false);

            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = Vector2.zero;
            panelRt.anchorMax = Vector2.one;
            panelRt.offsetMin = Vector2.zero;
            panelRt.offsetMax = Vector2.zero;

            Image background = panel.AddComponent<Image>();
            background.color = new Color(0.02f, 0.02f, 0.05f, 0.92f);
            background.raycastTarget = true; // blocks clicks on the Roll button/board underneath once shown

            Text winnerText = CreateLabel(panel.transform, "WinnerText", new Vector2(0f, 200f), new Vector2(900f, 160f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), "Red Wins!", 64);

            Button replayButton = CreateButton(panel.transform, "ReplayButton", new Vector2(0f, 820f), new Vector2(380f, 130f), "Replay");
            Button mainMenuButton = CreateButton(panel.transform, "MainMenuButton", new Vector2(0f, 650f), new Vector2(380f, 130f), "Main Menu");

            LudoVictoryScreenController victoryController = panel.AddComponent<LudoVictoryScreenController>();
            victoryController.Configure(board, panel, winnerText, MainMenuSceneName);

            UnityEventTools.AddPersistentListener(replayButton.onClick, victoryController.OnReplayClicked);
            UnityEventTools.AddPersistentListener(mainMenuButton.onClick, victoryController.OnMainMenuClicked);
        }

        /// <summary>Builds the theme-selector overlay: a small top-left panel with 4 buttons that call
        /// into <see cref="LudoThemeSelectorUI"/>, which looks up <see cref="LudoThemeManager.Instance"/>
        /// lazily so it works whether or not "Setup Theme Manager" has been run yet. Locked buttons show
        /// a padlock icon and "(Cost Coins)" label; a center-screen toast surfaces failed purchases.</summary>
        private static void BuildThemeSelectorPanel(Transform canvasParent)
        {
            GameObject panel = new GameObject("ThemePanel", typeof(RectTransform));
            panel.transform.SetParent(canvasParent, false);

            RectTransform panelRt = panel.GetComponent<RectTransform>();
            panelRt.anchorMin = new Vector2(0f, 1f);
            panelRt.anchorMax = new Vector2(0f, 1f);
            panelRt.pivot = new Vector2(0f, 1f);
            panelRt.anchoredPosition = new Vector2(24f, -24f);
            panelRt.sizeDelta = new Vector2(220f, 360f);

            Text header = CreateLabel(panel.transform, "Header", Vector2.zero, new Vector2(220f, 50f),
                new Vector2(0f, 1f), new Vector2(0f, 1f), "Theme", 30);
            header.alignment = TextAnchor.MiddleLeft;
            RectTransform headerRt = header.GetComponent<RectTransform>();
            headerRt.pivot = new Vector2(0f, 1f);

            var entries = new (LudoThemeId id, string label)[]
            {
                (LudoThemeId.Glass, "Glass"),
                (LudoThemeId.Stone, "Stone"),
                (LudoThemeId.Jungle, "Jungle"),
                (LudoThemeId.Cyberpunk, "Cyberpunk"),
            };

            var buttons = new List<LudoThemeSelectorUI.ThemeButton>(entries.Length);
            float y = -60f;
            foreach (var (id, label) in entries)
            {
                Button button = CreateThemeButton(panel.transform, $"ThemeButton_{id}", new Vector2(0f, y), new Vector2(200f, 64f), label,
                    out Image background, out Text labelText, out GameObject lockIcon);
                buttons.Add(new LudoThemeSelectorUI.ThemeButton { themeId = id, button = button, background = background, label = labelText, lockIcon = lockIcon });
                y -= 72f;
            }

            BuildToast(canvasParent, out GameObject toastRoot, out Text toastText);

            LudoThemeSelectorUI selector = panel.AddComponent<LudoThemeSelectorUI>();
            selector.Configure(buttons, toastRoot, toastText);

            UnityEventTools.AddPersistentListener(buttons[0].button.onClick, selector.SelectGlass);
            UnityEventTools.AddPersistentListener(buttons[1].button.onClick, selector.SelectStone);
            UnityEventTools.AddPersistentListener(buttons[2].button.onClick, selector.SelectJungle);
            UnityEventTools.AddPersistentListener(buttons[3].button.onClick, selector.SelectCyberpunk);
        }

        private static Text CreateLabel(Transform canvasParent, string name, Vector2 anchoredPos, Vector2 size, Vector2 anchorMin, Vector2 anchorMax, string text, int fontSize)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.pivot = anchorMax;
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Text label = go.AddComponent<Text>();
            label.text = text;
            label.fontSize = fontSize;
            label.alignment = TextAnchor.MiddleCenter;
            label.color = Color.white;
            label.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            return label;
        }

        private static Button CreateButton(Transform canvasParent, string name, Vector2 anchoredPos, Vector2 size, string label)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(canvasParent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            Image image = go.AddComponent<Image>();
            image.color = new Color(0.2f, 0.55f, 0.9f);

            Button button = go.AddComponent<Button>();

            Text buttonText = CreateLabel(go.transform, "Label", Vector2.zero, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), label, 40);
            RectTransform textRt = buttonText.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            return button;
        }

        /// <summary>Compact, top-left-anchored button variant used by the theme selector panel (<see cref="CreateButton"/> is bottom-center-anchored).
        /// Also carries a corner padlock icon (hidden once <see cref="LudoThemeSelectorUI"/> confirms the theme is unlocked) so a two-line
        /// "Name / (Cost Coins)" label and lock glyph both fit comfortably.</summary>
        private static Button CreateThemeButton(Transform parent, string name, Vector2 anchoredPos, Vector2 size, string label, out Image background, out Text labelText, out GameObject lockIcon)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0f, 1f);
            rt.anchorMax = new Vector2(0f, 1f);
            rt.pivot = new Vector2(0f, 1f);
            rt.sizeDelta = size;
            rt.anchoredPosition = anchoredPos;

            background = go.AddComponent<Image>();
            background.color = new Color(0.15f, 0.15f, 0.18f, 0.85f);

            Button button = go.AddComponent<Button>();

            labelText = CreateLabel(go.transform, "Label", Vector2.zero, size, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), label, 24);
            RectTransform textRt = labelText.GetComponent<RectTransform>();
            textRt.anchorMin = Vector2.zero;
            textRt.anchorMax = Vector2.one;
            textRt.sizeDelta = Vector2.zero;
            textRt.anchoredPosition = Vector2.zero;

            lockIcon = CreateLockIcon(go.transform);

            return button;
        }

        /// <summary>Small amber padlock in a button's top-right corner, built from a procedurally
        /// generated icon (no external art asset needed) so a locked theme is unmistakable at a glance.</summary>
        private static GameObject CreateLockIcon(Transform buttonParent)
        {
            GameObject iconGO = new GameObject("LockIcon", typeof(RectTransform));
            iconGO.transform.SetParent(buttonParent, false);

            RectTransform rt = iconGO.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(1f, 1f);
            rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(1f, 1f);
            rt.sizeDelta = new Vector2(20f, 22f);
            rt.anchoredPosition = new Vector2(-6f, -6f);

            Image image = iconGO.AddComponent<Image>();
            image.sprite = GetLockIconSprite();
            image.color = new Color(1f, 0.82f, 0.3f);
            image.preserveAspect = true;
            image.raycastTarget = false; // purely decorative; must not steal the button's click

            return iconGO;
        }

        // 16x18 padlock silhouette (arch/shackle on top, solid body with a keyhole notch below),
        // baked into a small crisp Texture2D at build time - '#' = filled, '.' = transparent.
        private static readonly string[] LockIconBitmap =
        {
            "....########....",
            "....########....",
            "....########....",
            "....###..###....",
            "....###..###....",
            "....###..###....",
            "....###..###....",
            "....###..###....",
            ".##############.",
            ".##############.",
            ".##############.",
            ".######..######.",
            ".######..######.",
            ".##############.",
            ".##############.",
            ".##############.",
            ".##############.",
            ".##############.",
        };

        private static Sprite _cachedLockIconSprite;

        private static Sprite GetLockIconSprite()
        {
            if (_cachedLockIconSprite == null) _cachedLockIconSprite = CreateLockIconSprite();
            return _cachedLockIconSprite;
        }

        private static Sprite CreateLockIconSprite()
        {
            int height = LockIconBitmap.Length;
            int width = LockIconBitmap[0].Length;

            Texture2D tex = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "LudoTheme_LockIcon",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp
            };

            Color clear = new Color(1f, 1f, 1f, 0f);
            Color[] pixels = new Color[width * height];

            for (int row = 0; row < height; row++)
            {
                string line = LockIconBitmap[row];
                int textureY = height - 1 - row; // bitmap row 0 is the top; texture row 0 is the bottom.
                for (int col = 0; col < width; col++)
                {
                    bool filled = col < line.Length && line[col] == '#';
                    pixels[textureY * width + col] = filled ? Color.white : clear;
                }
            }

            tex.SetPixels(pixels);
            tex.Apply();

            return Sprite.Create(tex, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), width);
        }

        /// <summary>Builds the brief center-screen "Not Enough Coins!" style popup driven by <see cref="LudoThemeSelectorUI"/>. Starts hidden.</summary>
        private static void BuildToast(Transform canvasParent, out GameObject toastRoot, out Text toastText)
        {
            GameObject root = new GameObject("ThemeToast", typeof(RectTransform));
            root.transform.SetParent(canvasParent, false);

            RectTransform rt = root.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(520f, 100f);
            rt.anchoredPosition = Vector2.zero;

            Image background = root.AddComponent<Image>();
            background.color = new Color(0.08f, 0.08f, 0.08f, 0.92f);
            background.raycastTarget = false;

            Text label = CreateLabel(root.transform, "Label", Vector2.zero, new Vector2(500f, 90f),
                new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), string.Empty, 36);
            label.color = new Color(1f, 0.82f, 0.3f);
            RectTransform labelRt = label.GetComponent<RectTransform>();
            labelRt.anchorMin = Vector2.zero;
            labelRt.anchorMax = Vector2.one;
            labelRt.sizeDelta = Vector2.zero;
            labelRt.anchoredPosition = Vector2.zero;

            root.SetActive(false);

            toastRoot = root;
            toastText = label;
        }

        /// <summary>
        /// Creates the default primitive visual for a themable anchor, as a child named "Visual".
        /// This is what makes the board look correct even before any theme is ever applied - the theme
        /// manager only ever re-skins or replaces this child, never the anchor that owns it.
        /// </summary>
        private static Transform CreateDefaultVisual(Transform parent, PrimitiveType primitive, Vector3 localScale, Color color, string materialName,
            float metallic = 0f, float smoothness = 0.4f, bool transparent = false, Color? emissionColor = null, float emissionIntensity = 0f)
        {
            GameObject visual = GameObject.CreatePrimitive(primitive);
            visual.name = "Visual";
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;
            visual.transform.localScale = localScale;

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null) UnityEngine.Object.DestroyImmediate(collider);

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = CreateColorMaterial(color, materialName, metallic, smoothness, transparent, emissionColor, emissionIntensity);

            return visual.transform;
        }

        private static Vector3 CellToWorld(Vector2Int cell)
        {
            float x = (cell.x - CenterCell) * CellSize;
            float z = (CenterCell - cell.y) * CellSize;
            return new Vector3(x, 0f, z);
        }

        /// <summary>
        /// Builds a URP Lit (falling back to Standard/Sprites-Default) material. Passing an
        /// <paramref name="emissionColor"/> with a positive <paramref name="emissionIntensity"/> bakes the
        /// _EMISSION keyword into the material itself, not just the color value - this matters because
        /// nothing downstream can toggle a shader keyword after the fact (a MaterialPropertyBlock, e.g.,
        /// can vary _EmissionColor per-instance but can't turn emission on for a material that was never
        /// built with the keyword enabled), so skipping this step would make a tile silently never glow
        /// regardless of what color it's given.
        /// </summary>
        private static Material CreateColorMaterial(Color color, string materialName, float metallic = 0f, float smoothness = 0.4f, bool transparent = false,
            Color? emissionColor = null, float emissionIntensity = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Sprites/Default");

            Material mat = new Material(shader) { name = materialName };
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_Metallic")) mat.SetFloat("_Metallic", Mathf.Clamp01(metallic));
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", Mathf.Clamp01(smoothness));
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", Mathf.Clamp01(smoothness));

            if (transparent) MakeTransparent(mat);

            if (emissionColor.HasValue && emissionIntensity > 0f)
            {
                mat.EnableKeyword("_EMISSION");
                mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", emissionColor.Value * emissionIntensity);
            }

            return mat;
        }

        /// <summary>URP Lit / built-in Standard transparent-surface setup, so acrylic-glass materials
        /// actually alpha-blend instead of rendering as solid color with an ignored alpha channel.</summary>
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

        private static Mesh _sharedCubeMesh;

        /// <summary>Unity's own built-in Cube mesh asset (correct winding/normals/UVs guaranteed) - reused
        /// as raw material for <see cref="CreateBevelledSlabMesh"/> instead of hand-rolling geometry.</summary>
        private static Mesh GetSharedCubeMesh()
        {
            if (_sharedCubeMesh == null)
            {
                GameObject temp = GameObject.CreatePrimitive(PrimitiveType.Cube);
                _sharedCubeMesh = temp.GetComponent<MeshFilter>().sharedMesh;
                UnityEngine.Object.DestroyImmediate(temp);
            }
            return _sharedCubeMesh;
        }

        /// <summary>
        /// Builds a "two-tier plateau" mesh - a full-footprint base slab with a slightly inset, thinner
        /// cap stacked on top - entirely out of <see cref="GetSharedCubeMesh"/> combined via
        /// <see cref="Mesh.CombineMeshes"/>, so the stepped edge always renders correctly with zero
        /// hand-rolled triangle-winding risk. Reads as a bevelled/faceted cut under raking light.
        /// </summary>
        private static Mesh CreateBevelledSlabMesh(Vector3 size, float bevelRatio, string meshName)
        {
            const float capHeightRatio = 0.45f;
            float capHeight = size.y * capHeightRatio;
            float baseHeight = size.y - capHeight;
            float inset = Mathf.Min(size.x, size.z) * Mathf.Clamp01(bevelRatio);

            float capX = Mathf.Max(0.02f, size.x - inset * 2f);
            float capZ = Mathf.Max(0.02f, size.z - inset * 2f);

            var combine = new CombineInstance[2];
            combine[0].mesh = GetSharedCubeMesh();
            combine[0].transform = Matrix4x4.TRS(
                new Vector3(0f, -size.y * 0.5f + baseHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(size.x, Mathf.Max(0.001f, baseHeight), size.z));

            combine[1].mesh = GetSharedCubeMesh();
            combine[1].transform = Matrix4x4.TRS(
                new Vector3(0f, size.y * 0.5f - capHeight * 0.5f, 0f), Quaternion.identity,
                new Vector3(capX, Mathf.Max(0.001f, capHeight), capZ));

            Mesh mesh = new Mesh { name = meshName };
            mesh.CombineMeshes(combine, true, true);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Bevelled-mesh counterpart to <see cref="CreateDefaultVisual"/>, for tile-like flat
        /// pieces (tiles, home-base floors, the center piece, the border) where a visible facet reads well.</summary>
        private static Transform CreateBevelledVisual(Transform parent, Vector3 localScale, Color color, string materialName,
            float metallic = 0.1f, float smoothness = 0.55f, bool transparent = false, float bevelRatio = 0.14f,
            Color? emissionColor = null, float emissionIntensity = 0f)
        {
            GameObject visual = new GameObject("Visual");
            visual.transform.SetParent(parent, false);
            visual.transform.localPosition = Vector3.zero;

            MeshFilter filter = visual.AddComponent<MeshFilter>();
            filter.sharedMesh = CreateBevelledSlabMesh(localScale, bevelRatio, $"{materialName}_Mesh");

            MeshRenderer renderer = visual.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = CreateColorMaterial(color, materialName, metallic, smoothness, transparent, emissionColor, emissionIntensity);

            return visual.transform;
        }

        // --- Optional 3D character-model tokens -------------------------------------------------
        // Dormant by default: BuildTokens() falls back to the plain Cylinder placeholder goti for any
        // color with no entry here. To use real character models as tokens, populate this array
        // (index = (int)PlayerColor, i.e. Red/Green/Yellow/Blue) with prefab references - every spawned
        // instance is auto-measured and scaled to fit a tile (see SpawnCharacterModel), regardless of the
        // source model's authored import scale.
        // Fraction of a tile's width a character's footprint should fill - leaves a little breathing
        // room so neighbouring tiles' avatars never visually overlap.
        private const float CharacterFootprintRatio = 0.8f;
        // Fallback only, used if a model has no measurable Renderer bounds yet at spawn time.
        private const float CharacterModelScale = 0.12f * LayoutScale;
        private static readonly GameObject[] CharacterModelPrefabsByColor = new GameObject[4];

        /// <summary>
        /// Instantiates a 3D character-model prefab under a token's stable anchor, strips any colliders
        /// it brought with it (tokens are moved by script, never physics), and uniformly scales it so its
        /// own measured footprint - whatever the source model's authored/import scale actually is - fits
        /// <see cref="CharacterFootprintRatio"/> of the current <see cref="CellSize"/>. This is what keeps
        /// the board's tile spacing and the character's on-tile size proportional to each other even if
        /// the character prefab is swapped for a differently-scaled model later.
        /// </summary>
        private static Transform SpawnCharacterModel(GameObject modelPrefab, Transform parent)
        {
            if (modelPrefab == null) return null;

            GameObject instance = UnityEngine.Object.Instantiate(modelPrefab, parent);
            instance.name = "Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one; // measure at the model's own natural scale first

            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                UnityEngine.Object.DestroyImmediate(collider);
            }

            instance.transform.localScale = Vector3.one * ComputeCharacterFitScale(instance);

            return instance.transform;
        }

        /// <summary>Measures a character's actual rendered footprint (the wider of its X/Z bounds, combined
        /// across every Renderer in its hierarchy) and returns the uniform scale that fits it to
        /// <see cref="CharacterFootprintRatio"/> of a tile. Falls back to <see cref="CharacterModelScale"/>
        /// if the model has no measurable geometry (e.g. a prefab with no Renderer at all).</summary>
        private static float ComputeCharacterFitScale(GameObject instance)
        {
            Renderer[] renderers = instance.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return CharacterModelScale;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            float widestFootprintAxis = Mathf.Max(bounds.size.x, bounds.size.z);
            if (widestFootprintAxis <= 0.0001f)
            {
                return CharacterModelScale;
            }

            float targetFootprint = CellSize * CharacterFootprintRatio;
            return targetFootprint / widestFootprintAxis;
        }
    }
#endif
}
