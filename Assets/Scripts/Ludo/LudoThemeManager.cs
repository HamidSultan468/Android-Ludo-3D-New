using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
#endif

namespace LudoEmpire.Ludo
{
    /// <summary>What role a themed board piece plays, so <see cref="LudoThemeManager"/> knows which
    /// theme fields (and which tint) apply to it.</summary>
    public enum LudoThemeElementKind
    {
        BoardBase,
        TrackTile,
        HomeBaseFloor,
        CenterHome,
        Token,
        Dice
    }

    /// <summary>
    /// Swaps board materials, tile/token prefabs, and environmental lighting at runtime across the
    /// Glass, Stone, Jungle and Cyberpunk presets. Discovers every <see cref="LudoThemedElement"/>
    /// under <see cref="boardRoot"/> and re-skins only their swappable visual children - it never
    /// touches the anchor Transforms that <see cref="LudoBoardLogic"/> and
    /// <see cref="LudoTokenVisualBinder"/> track, so a theme switch can never break grid tracking,
    /// in-flight moves, or turn state.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-50)]
    public class LudoThemeManager : MonoBehaviour
    {
        private const string PrefsKey = "LudoEmpire_SelectedTheme";
        private const string UnlockPrefsPrefix = "LudoEmpire_ThemeUnlocked_";

        [Header("Available Themes")]
        [Tooltip("Populated automatically by 'Ludo Tools/Setup Theme Manager'. One entry per LudoThemeId is expected but not required.")]
        [SerializeField] private LudoThemeData[] themes = Array.Empty<LudoThemeData>();
        [SerializeField] private LudoThemeId startingTheme = LudoThemeId.Glass;
        [Tooltip("Remembers the last theme picked (PlayerPrefs) and restores it on the next play session.")]
        [SerializeField] private bool persistSelection = true;

        [Header("Scene References (auto-populated by 'Setup Theme Manager')")]
        [Tooltip("Root of the generated board hierarchy. Every LudoThemedElement under this transform is discovered automatically.")]
        [SerializeField] private Transform boardRoot;
        [SerializeField] private Light sunLight;
        [SerializeField] private Light fillLight;

        public static LudoThemeManager Instance { get; private set; }

        /// <summary>Raised every time a theme finishes applying.</summary>
        public event Action<LudoThemeData> OnThemeChanged;
        /// <summary>Raised the moment a locked theme is successfully purchased and permanently unlocked.</summary>
        public event Action<LudoThemeId> OnThemeUnlocked;
        /// <summary>Raised when <see cref="SelectTheme"/> can't unlock a locked theme (e.g. insufficient coins), with a short UI-ready reason.</summary>
        public event Action<LudoThemeId, string> OnThemePurchaseFailed;
        public LudoThemeData CurrentTheme { get; private set; }
        public IReadOnlyList<LudoThemeData> Themes => themes;

        private readonly Dictionary<LudoThemeId, LudoThemeData> _themeLookup = new Dictionary<LudoThemeId, LudoThemeData>();
        private readonly HashSet<LudoThemeId> _unlockedThemes = new HashSet<LudoThemeId>();
        private readonly Dictionary<LudoThemedElement, List<Material>> _ownedMaterials = new Dictionary<LudoThemedElement, List<Material>>();
        private LudoThemedElement[] _cachedElements = Array.Empty<LudoThemedElement>();

        private bool _defaultsCaptured;
        private Material _defaultSkybox;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning($"[LudoThemeManager] Duplicate LudoThemeManager on '{name}'; keeping the first one.", this);
                Destroy(gameObject);
                return;
            }

            Instance = this;
            RebuildThemeLookup();
            LoadUnlockedThemes();
            RefreshElementCache();
        }

        private void Start()
        {
            LudoThemeId initial = startingTheme;
            if (persistSelection)
            {
                string saved = PlayerPrefs.GetString(PrefsKey, string.Empty);
                if (!string.IsNullOrEmpty(saved) && Enum.TryParse(saved, out LudoThemeId parsedId))
                {
                    initial = parsedId;
                }
            }

            if (!IsThemeUnlocked(initial))
            {
                // Saved/starting theme is no longer unlocked (corrupted prefs, edited asset, etc.) -
                // fall back to the configured starting theme, or any unlocked theme as a last resort.
                Debug.LogWarning($"[LudoThemeManager] Theme '{initial}' isn't unlocked; falling back.", this);
                initial = IsThemeUnlocked(startingTheme) ? startingTheme : FirstUnlockedThemeOrDefault();
            }

            ApplyTheme(initial);
        }

        private void OnDestroy()
        {
            foreach (var materials in _ownedMaterials.Values)
            {
                if (materials == null) continue;
                foreach (var mat in materials) DestroyMaterialInstance(mat);
            }
            _ownedMaterials.Clear();

            if (Instance == this) Instance = null;
        }

        /// <summary>Re-scans <see cref="boardRoot"/> (or this object's own hierarchy if unset) for themable elements. Call after rebuilding the board.</summary>
        public void RefreshElementCache()
        {
            Transform scanRoot = boardRoot != null ? boardRoot : transform;
            _cachedElements = scanRoot.GetComponentsInChildren<LudoThemedElement>(true);
        }

        private void RebuildThemeLookup()
        {
            _themeLookup.Clear();
            if (themes == null) return;
            foreach (var theme in themes)
            {
                if (theme == null) continue;
                _themeLookup[theme.themeId] = theme;
            }
        }

        private void LoadUnlockedThemes()
        {
            _unlockedThemes.Clear();
            foreach (var theme in _themeLookup.Values)
            {
                if (theme == null) continue;
                if (theme.IsFreeByDefault || PlayerPrefs.GetInt(UnlockPrefsPrefix + theme.themeId, 0) == 1)
                {
                    _unlockedThemes.Add(theme.themeId);
                }
            }
        }

        private LudoThemeId FirstUnlockedThemeOrDefault()
        {
            foreach (var id in _unlockedThemes) return id;
            return LudoThemeId.Glass; // Glass is free by convention, so this should only ever be reached with an empty theme list.
        }

        /// <summary>The theme data asset for a given id, or null if it isn't assigned on this manager.</summary>
        public LudoThemeData GetThemeData(LudoThemeId id)
        {
            _themeLookup.TryGetValue(id, out LudoThemeData data);
            return data;
        }

        /// <summary>Coins required to unlock a theme (0 for a free/already-owned theme, or an unrecognized id).</summary>
        public int GetThemeCost(LudoThemeId id)
        {
            LudoThemeData data = GetThemeData(id);
            return data != null ? Mathf.Max(0, data.unlockCost) : 0;
        }

        /// <summary>True if this theme can be applied right now without a purchase (free themes always count as unlocked).</summary>
        public bool IsThemeUnlocked(LudoThemeId id)
        {
            if (_themeLookup.TryGetValue(id, out LudoThemeData data) && data != null && data.IsFreeByDefault) return true;
            return _unlockedThemes.Contains(id);
        }

        /// <summary>
        /// The single entry point UI should call for a button click: applies the theme immediately if
        /// it's already unlocked, otherwise attempts to purchase it first via <see cref="CurrencyManager"/>
        /// (deduct coins -&gt; unlock permanently -&gt; persist -&gt; apply). On insufficient funds (or no
        /// currency manager / unknown theme) the theme is left unchanged and <see cref="OnThemePurchaseFailed"/> fires.
        /// </summary>
        public bool SelectTheme(LudoThemeId id)
        {
            if (_themeLookup.Count == 0) RebuildThemeLookup();

            if (!_themeLookup.TryGetValue(id, out LudoThemeData data) || data == null)
            {
                Debug.LogWarning($"[LudoThemeManager] SelectTheme: '{id}' is not assigned on '{name}'.", this);
                OnThemePurchaseFailed?.Invoke(id, "Theme unavailable.");
                return false;
            }

            return IsThemeUnlocked(id) ? ApplyTheme(id) : TryPurchaseTheme(id, data);
        }

        private bool TryPurchaseTheme(LudoThemeId id, LudoThemeData data)
        {
            int cost = Mathf.Max(0, data.unlockCost);

            CurrencyManager currency = CurrencyManager.Instance;
            if (currency == null)
            {
                Debug.LogWarning("[LudoThemeManager] No CurrencyManager in the scene; can't purchase themes.", this);
                OnThemePurchaseFailed?.Invoke(id, "Shop unavailable.");
                return false;
            }

            if (!currency.TrySpendCoins(cost, $"ThemeUnlock:{id}"))
            {
                OnThemePurchaseFailed?.Invoke(id, "Not Enough Coins!");
                return false;
            }

            UnlockTheme(id);
            return ApplyTheme(id);
        }

        private void UnlockTheme(LudoThemeId id)
        {
            if (!_unlockedThemes.Add(id)) return; // already unlocked - never charge or fire the event twice

            PlayerPrefs.SetInt(UnlockPrefsPrefix + id, 1);
            PlayerPrefs.Save();
            OnThemeUnlocked?.Invoke(id);
        }

        /// <summary>
        /// Switches every registered board element, the light rig, and the skybox/fog to the given
        /// theme. Purely visual - never reads or writes any <see cref="LudoBoardLogic"/> state. Refuses
        /// (logging a warning) if the theme is still locked - go through <see cref="SelectTheme"/> to
        /// purchase-then-apply from UI instead.
        /// </summary>
        public bool ApplyTheme(LudoThemeId id)
        {
            if (_themeLookup.Count == 0) RebuildThemeLookup();

            if (!_themeLookup.TryGetValue(id, out LudoThemeData data) || data == null)
            {
                Debug.LogWarning($"[LudoThemeManager] Theme '{id}' is not assigned on '{name}'. " +
                                  "Run 'Ludo Tools/Setup Theme Manager' or assign it manually in the Inspector.", this);
                return false;
            }

            if (!IsThemeUnlocked(id))
            {
                Debug.LogWarning($"[LudoThemeManager] Theme '{id}' is locked (costs {data.unlockCost} coins); use SelectTheme() to purchase it first.", this);
                return false;
            }

            if (_cachedElements == null || _cachedElements.Length == 0) RefreshElementCache();

            CurrentTheme = data;
            ApplyLighting(data);

            foreach (var element in _cachedElements)
            {
                if (element == null) continue; // hierarchy may have changed since the last cache refresh
                ApplyElement(element, data);
            }

            if (persistSelection)
            {
                PlayerPrefs.SetString(PrefsKey, id.ToString());
                PlayerPrefs.Save();
            }

            OnThemeChanged?.Invoke(data);
            return true;
        }

        private void ApplyElement(LudoThemedElement element, LudoThemeData data)
        {
            switch (element.Kind)
            {
                case LudoThemeElementKind.BoardBase:
                    ApplySurface(element, data.boardBasePrefab, data.boardMaterial, null);
                    break;
                case LudoThemeElementKind.TrackTile:
                    ApplySurface(element, data.tilePrefab, data.tileMaterial, ResolveTint(element, data));
                    break;
                case LudoThemeElementKind.HomeBaseFloor:
                    ApplySurface(element, data.homeBasePrefab, data.homeBaseMaterial, ResolveTint(element, data));
                    break;
                case LudoThemeElementKind.CenterHome:
                    ApplySurface(element, data.centerHomePrefab, data.centerHomeMaterial, null);
                    break;
                case LudoThemeElementKind.Token:
                    ApplySurface(element, data.tokenPrefab, data.tokenMaterial, ResolveTint(element, data));
                    break;
                case LudoThemeElementKind.Dice:
                    ApplyDice(element, data);
                    break;
            }
        }

        private static Color? ResolveTint(LudoThemedElement element, LudoThemeData data)
        {
            if (!element.UseColorTint) return null;
            if (element.OwnerColor.HasValue) return data.GetPlayerTint(element.OwnerColor.Value);
            return element.FixedTint;
        }

        /// <summary>Handles every non-dice element: optionally instances a theme prefab, then applies/tints a material.</summary>
        private void ApplySurface(LudoThemedElement element, GameObject prefab, Material material, Color? tint)
        {
            if (prefab != null)
            {
                SwapVisualPrefab(element, prefab);
            }
            ApplyMaterialAndTint(element, material, tint);
        }

        private void SwapVisualPrefab(LudoThemedElement element, GameObject prefab)
        {
            Transform slot = element.VisualSlot;
            if (slot == null || prefab == null) return;

            ReleaseTrackedMaterials(element);

            for (int i = slot.childCount - 1; i >= 0; i--)
            {
                GameObject child = slot.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
            }

            GameObject instance = Instantiate(prefab, slot);
            instance.name = "Visual";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;

            // Theme prefabs are visual-only; strip any incidental colliders so a re-themed
            // tile/token/home-base can never introduce an unexpected physics interaction.
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
            {
                if (Application.isPlaying) Destroy(collider); else DestroyImmediate(collider);
            }
        }

        /// <summary>
        /// Applies (and/or tints) a material across every renderer under the element's visual slot.
        /// If <paramref name="template"/> is null, each renderer's own current material is cloned and
        /// retinted in place instead - so a theme that only defines tints (no bespoke material) still
        /// works, and a bespoke prefab's authored look is preserved unless the theme overrides it.
        /// </summary>
        private void ApplyMaterialAndTint(LudoThemedElement element, Material template, Color? tint)
        {
            Transform slot = element.VisualSlot;
            if (slot == null) return;

            Renderer[] renderers = slot.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return;

            ReleaseTrackedMaterials(element);

            var owned = new List<Material>(renderers.Length);
            foreach (var renderer in renderers)
            {
                Material baseMaterial = template != null ? template : renderer.sharedMaterial;
                if (baseMaterial == null) continue;

                Material instance = new Material(baseMaterial) { name = $"{baseMaterial.name} (LudoTheme Instance)" };
                if (tint.HasValue) ApplyColorToMaterial(instance, tint.Value);

                renderer.sharedMaterial = instance;
                owned.Add(instance);
            }

            if (owned.Count > 0) _ownedMaterials[element] = owned;
        }

        private void ApplyDice(LudoThemedElement element, LudoThemeData data)
        {
            if (element == null) return;

            if (data.diceMaterial != null)
            {
                Renderer renderer = element.GetComponent<Renderer>();
                if (renderer != null)
                {
                    ReleaseTrackedMaterials(element);
                    Material instance = new Material(data.diceMaterial) { name = $"{data.diceMaterial.name} (LudoTheme Instance)" };
                    renderer.sharedMaterial = instance;
                    _ownedMaterials[element] = new List<Material> { instance };
                }
            }

            if (data.diceMesh != null)
            {
                MeshFilter filter = element.GetComponent<MeshFilter>();
                if (filter != null) filter.sharedMesh = data.diceMesh;

                // Only kept in sync if the dice happens to use a MeshCollider; the default
                // primitive-cube dice uses a BoxCollider, which stays correct regardless of mesh.
                MeshCollider meshCollider = element.GetComponent<MeshCollider>();
                if (meshCollider != null) meshCollider.sharedMesh = data.diceMesh;
            }
        }

        private void ApplyLighting(LudoThemeData data)
        {
            CaptureRenderSettingsDefaultsOnce();

            if (sunLight != null)
            {
                sunLight.color = data.sunColor;
                sunLight.intensity = data.sunIntensity;
                sunLight.transform.rotation = Quaternion.Euler(data.sunEulerRotation);
            }

            if (fillLight != null)
            {
                fillLight.color = data.sunColor;
                fillLight.intensity = data.sunIntensity * 0.3f;
            }

            RenderSettings.skybox = data.skyboxMaterial != null ? data.skyboxMaterial : _defaultSkybox;
            RenderSettings.ambientLight = data.ambientLightColor;
            RenderSettings.ambientIntensity = Mathf.Max(0f, data.ambientIntensity);
            RenderSettings.fog = data.fogEnabled;
            RenderSettings.fogColor = data.fogColor;
            RenderSettings.fogDensity = Mathf.Max(0f, data.fogDensity);

            DynamicGI.UpdateEnvironment();
        }

        private void CaptureRenderSettingsDefaultsOnce()
        {
            if (_defaultsCaptured) return;
            _defaultSkybox = RenderSettings.skybox;
            _defaultsCaptured = true;
        }

        private static void ApplyColorToMaterial(Material mat, Color color)
        {
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);
            if (mat.HasProperty("_EmissionColor") && mat.IsKeywordEnabled("_EMISSION"))
            {
                mat.SetColor("_EmissionColor", color * 0.6f);
            }
        }

        private void ReleaseTrackedMaterials(LudoThemedElement element)
        {
            if (_ownedMaterials.TryGetValue(element, out var list) && list != null)
            {
                foreach (var mat in list) DestroyMaterialInstance(mat);
            }
            _ownedMaterials.Remove(element);
        }

        private void DestroyMaterialInstance(Material mat)
        {
            if (mat == null) return;
            if (Application.isPlaying) Destroy(mat); else DestroyImmediate(mat);
        }

#if UNITY_EDITOR
        /// <summary>
        /// Editor-only tool that links a scene's <see cref="LudoThemeManager"/> to the Glass, Stone,
        /// Jungle and Cyberpunk presets - creating default theme assets the first time it runs so
        /// runtime switching works immediately, with no manual material authoring required.
        /// </summary>
        public static class LudoThemeManagerSetupTool
        {
            private const string ThemeAssetFolder = "Assets/Art/Ludo/Themes";

            [MenuItem("Ludo Tools/Setup Theme Manager")]
            public static void SetupThemeManager()
            {
                GameObject boardRootGO = GameObject.Find("LudoBoard_Root");
                if (boardRootGO == null)
                {
                    bool proceed = EditorUtility.DisplayDialog(
                        "No Ludo Board Found",
                        "Could not find a 'LudoBoard_Root' in the active scene. Run 'Ludo Tools/Build 3D Ludo Scene' " +
                        "first for the best result, or continue to set up a standalone Theme Manager you can wire up manually?",
                        "Continue Anyway", "Cancel");
                    if (!proceed) return;
                }

                Undo.SetCurrentGroupName("Setup Ludo Theme Manager");
                int undoGroup = Undo.GetCurrentGroup();

                GameObject managerGO = boardRootGO != null
                    ? boardRootGO.transform.Find("ThemeManager")?.gameObject
                    : GameObject.Find("ThemeManager");

                if (managerGO == null)
                {
                    managerGO = new GameObject("ThemeManager");
                    if (boardRootGO != null) managerGO.transform.SetParent(boardRootGO.transform, false);
                    Undo.RegisterCreatedObjectUndo(managerGO, "Setup Ludo Theme Manager");
                }

                LudoThemeManager manager = managerGO.GetComponent<LudoThemeManager>();
                if (manager == null) manager = Undo.AddComponent<LudoThemeManager>(managerGO);

                LudoThemeData[] themeAssets =
                {
                    LoadOrCreateTheme(LudoThemeId.Glass),
                    LoadOrCreateTheme(LudoThemeId.Stone),
                    LoadOrCreateTheme(LudoThemeId.Jungle),
                    LoadOrCreateTheme(LudoThemeId.Cyberpunk),
                };

                Light sun = null;
                Light fill = null;
                if (boardRootGO != null)
                {
                    Transform rig = boardRootGO.transform.Find("LightRig");
                    Transform sunT = rig != null ? rig.Find("SunLight") : null;
                    Transform fillT = rig != null ? rig.Find("FillLight") : null;
                    sun = sunT != null ? sunT.GetComponent<Light>() : null;
                    fill = fillT != null ? fillT.GetComponent<Light>() : null;
                }

                var so = new SerializedObject(manager);
                SerializedProperty themesProp = so.FindProperty("themes");
                SerializedProperty boardRootProp = so.FindProperty("boardRoot");
                SerializedProperty sunProp = so.FindProperty("sunLight");
                SerializedProperty fillProp = so.FindProperty("fillLight");

                if (themesProp == null || boardRootProp == null || sunProp == null || fillProp == null)
                {
                    Debug.LogError("[LudoThemeManagerSetupTool] LudoThemeManager's serialized field names have changed; " +
                                    "update SetupThemeManager() to match (expected 'themes', 'boardRoot', 'sunLight', 'fillLight').");
                    Undo.CollapseUndoOperations(undoGroup);
                    return;
                }

                themesProp.arraySize = themeAssets.Length;
                for (int i = 0; i < themeAssets.Length; i++)
                {
                    themesProp.GetArrayElementAtIndex(i).objectReferenceValue = themeAssets[i];
                }
                if (boardRootGO != null) boardRootProp.objectReferenceValue = boardRootGO.transform;
                sunProp.objectReferenceValue = sun;
                fillProp.objectReferenceValue = fill;

                so.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(manager);

                manager.RefreshElementCache();

                AssetDatabase.SaveAssets();

                Selection.activeGameObject = managerGO;
                EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
                Undo.CollapseUndoOperations(undoGroup);

                Debug.Log("[LudoThemeManagerSetupTool] LudoThemeManager linked with Glass/Stone/Jungle/Cyberpunk presets.");
            }

            private static LudoThemeData LoadOrCreateTheme(LudoThemeId id)
            {
                LudoThemeData existing = FindExistingTheme(id);
                if (existing != null)
                {
                    // An earlier version of this tool created default materials without embedding them
                    // in the asset (AssetDatabase.AddObjectToAsset), so they were orphaned in memory and
                    // serialized back out as null references. Backfill them here rather than leaving a
                    // theme with no visual materials at all - but only if nothing has ever been set, so
                    // a hand-authored material is never overwritten.
                    if (HasNoMaterialsAssigned(existing))
                    {
                        string existingPath = AssetDatabase.GetAssetPath(existing);
                        ApplyDefaultMaterials(existing, id);
                        EmbedMaterials(existing, existingPath);
                    }

                    existing.unlockCost = GetDefaultUnlockCost(id);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }

                if (!AssetDatabase.IsValidFolder(ThemeAssetFolder)) CreateFolderRecursive(ThemeAssetFolder);

                LudoThemeData asset = ScriptableObject.CreateInstance<LudoThemeData>();
                asset.themeId = id;
                asset.displayName = id.ToString();
                asset.unlockCost = GetDefaultUnlockCost(id);
                ApplyDefaultLightingAndTints(asset, id);

                string assetPath = AssetDatabase.GenerateUniqueAssetPath($"{ThemeAssetFolder}/Theme_{id}.asset");
                AssetDatabase.CreateAsset(asset, assetPath);

                // Materials can only be embedded (AddObjectToAsset) once the main asset already exists
                // on disk - doing it before CreateAsset is what caused the orphaned-material bug above.
                ApplyDefaultMaterials(asset, id);
                EmbedMaterials(asset, assetPath);

                Debug.Log($"[LudoThemeManagerSetupTool] Created default theme asset at '{assetPath}'.");
                return asset;
            }

            private static LudoThemeData FindExistingTheme(LudoThemeId id)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:LudoThemeData"))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    LudoThemeData existing = AssetDatabase.LoadAssetAtPath<LudoThemeData>(path);
                    if (existing != null && existing.themeId == id) return existing;
                }
                return null;
            }

            private static bool HasNoMaterialsAssigned(LudoThemeData data)
            {
                return data.boardMaterial == null && data.tileMaterial == null && data.homeBaseMaterial == null &&
                       data.centerHomeMaterial == null && data.tokenMaterial == null && data.diceMaterial == null;
            }

            /// <summary>Embeds every material this theme owns as a sub-asset of its .asset file, so it's
            /// actually saved to disk instead of being an orphaned in-memory object.</summary>
            private static void EmbedMaterials(LudoThemeData asset, string assetPath)
            {
                Material[] materials =
                {
                    asset.boardMaterial, asset.tileMaterial, asset.homeBaseMaterial,
                    asset.centerHomeMaterial, asset.tokenMaterial, asset.diceMaterial,
                };

                foreach (Material mat in materials)
                {
                    if (mat == null) continue;
                    if (!string.IsNullOrEmpty(AssetDatabase.GetAssetPath(mat))) continue; // already a saved asset - don't re-add
                    AssetDatabase.AddObjectToAsset(mat, assetPath);
                }

                EditorUtility.SetDirty(asset);
            }

            private static int GetDefaultUnlockCost(LudoThemeId id)
            {
                return id switch
                {
                    LudoThemeId.Glass => 0,
                    LudoThemeId.Stone => 200,
                    LudoThemeId.Jungle => 500,
                    LudoThemeId.Cyberpunk => 1000,
                    _ => 0
                };
            }

            private static void CreateFolderRecursive(string path)
            {
                string[] parts = path.Split('/');
                string current = parts[0];
                for (int i = 1; i < parts.Length; i++)
                {
                    string next = $"{current}/{parts[i]}";
                    if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
                    current = next;
                }
            }

            /// <summary>Populates only the Material fields (never lighting/tints) - safe to call again on
            /// an existing asset to backfill materials without disturbing any hand-tuned lighting.</summary>
            private static void ApplyDefaultMaterials(LudoThemeData asset, LudoThemeId id)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");

                switch (id)
                {
                    case LudoThemeId.Glass:
                        asset.boardMaterial = MakeMaterial(shader, new Color(0.75f, 0.88f, 0.95f, 0.55f), "Glass_Board", transparent: true);
                        asset.tileMaterial = MakeMaterial(shader, new Color(0.85f, 0.93f, 0.98f, 0.65f), "Glass_Tile", transparent: true);
                        asset.homeBaseMaterial = MakeMaterial(shader, new Color(0.8f, 0.9f, 1f, 0.6f), "Glass_HomeBase", transparent: true);
                        asset.centerHomeMaterial = MakeMaterial(shader, new Color(0.9f, 0.95f, 1f, 0.7f), "Glass_Center", transparent: true);
                        asset.tokenMaterial = MakeMaterial(shader, new Color(1f, 1f, 1f, 0.75f), "Glass_Token", transparent: true);
                        asset.diceMaterial = MakeMaterial(shader, new Color(1f, 1f, 1f, 0.8f), "Glass_Dice", transparent: true);
                        break;

                    case LudoThemeId.Stone:
                        asset.boardMaterial = MakeMaterial(shader, new Color(0.55f, 0.53f, 0.5f), "Stone_Board");
                        asset.tileMaterial = MakeMaterial(shader, new Color(0.62f, 0.6f, 0.56f), "Stone_Tile");
                        asset.homeBaseMaterial = MakeMaterial(shader, new Color(0.5f, 0.48f, 0.45f), "Stone_HomeBase");
                        asset.centerHomeMaterial = MakeMaterial(shader, new Color(0.45f, 0.43f, 0.4f), "Stone_Center");
                        asset.tokenMaterial = MakeMaterial(shader, new Color(0.6f, 0.58f, 0.55f), "Stone_Token");
                        asset.diceMaterial = MakeMaterial(shader, new Color(0.65f, 0.63f, 0.6f), "Stone_Dice");
                        break;

                    case LudoThemeId.Jungle:
                        asset.boardMaterial = MakeMaterial(shader, new Color(0.36f, 0.27f, 0.16f), "Jungle_Board");
                        asset.tileMaterial = MakeMaterial(shader, new Color(0.3f, 0.45f, 0.2f), "Jungle_Tile");
                        asset.homeBaseMaterial = MakeMaterial(shader, new Color(0.25f, 0.4f, 0.18f), "Jungle_HomeBase");
                        asset.centerHomeMaterial = MakeMaterial(shader, new Color(0.4f, 0.55f, 0.25f), "Jungle_Center");
                        asset.tokenMaterial = MakeMaterial(shader, new Color(0.45f, 0.35f, 0.2f), "Jungle_Token");
                        asset.diceMaterial = MakeMaterial(shader, new Color(0.5f, 0.4f, 0.25f), "Jungle_Dice");
                        break;

                    case LudoThemeId.Cyberpunk:
                        asset.boardMaterial = MakeEmissiveMaterial(shader, new Color(0.08f, 0.08f, 0.12f), new Color(0.6f, 0.1f, 0.9f), "Cyberpunk_Board");
                        asset.tileMaterial = MakeEmissiveMaterial(shader, new Color(0.05f, 0.05f, 0.09f), new Color(0.1f, 0.8f, 0.9f), "Cyberpunk_Tile");
                        asset.homeBaseMaterial = MakeEmissiveMaterial(shader, new Color(0.06f, 0.05f, 0.1f), new Color(0.8f, 0.1f, 0.6f), "Cyberpunk_HomeBase");
                        asset.centerHomeMaterial = MakeEmissiveMaterial(shader, new Color(0.05f, 0.05f, 0.1f), new Color(0.9f, 0.2f, 0.9f), "Cyberpunk_Center");
                        asset.tokenMaterial = MakeEmissiveMaterial(shader, new Color(0.05f, 0.05f, 0.08f), new Color(0.2f, 0.9f, 1f), "Cyberpunk_Token");
                        asset.diceMaterial = MakeEmissiveMaterial(shader, new Color(0.05f, 0.05f, 0.08f), new Color(1f, 0.2f, 0.8f), "Cyberpunk_Dice");
                        break;
                }
            }

            /// <summary>Populates lighting, fog and player tints - only ever called for a brand-new asset,
            /// so it never overwrites lighting someone has hand-tuned on an existing theme.</summary>
            private static void ApplyDefaultLightingAndTints(LudoThemeData asset, LudoThemeId id)
            {
                switch (id)
                {
                    case LudoThemeId.Glass:
                        asset.ambientLightColor = new Color(0.75f, 0.85f, 0.95f);
                        asset.sunColor = new Color(0.9f, 0.95f, 1f);
                        asset.sunIntensity = 1.3f;
                        break;

                    case LudoThemeId.Stone:
                        asset.ambientLightColor = new Color(0.55f, 0.53f, 0.5f);
                        asset.sunColor = new Color(1f, 0.97f, 0.9f);
                        asset.sunIntensity = 1.1f;
                        break;

                    case LudoThemeId.Jungle:
                        asset.ambientLightColor = new Color(0.4f, 0.5f, 0.3f);
                        asset.sunColor = new Color(1f, 0.92f, 0.75f);
                        asset.sunIntensity = 1.4f;
                        asset.fogEnabled = true;
                        asset.fogColor = new Color(0.55f, 0.6f, 0.45f);
                        asset.fogDensity = 0.015f;
                        break;

                    case LudoThemeId.Cyberpunk:
                        asset.ambientLightColor = new Color(0.15f, 0.1f, 0.25f);
                        asset.sunColor = new Color(0.6f, 0.3f, 1f);
                        asset.sunIntensity = 0.9f;
                        asset.fogEnabled = true;
                        asset.fogColor = new Color(0.1f, 0.05f, 0.2f);
                        asset.fogDensity = 0.02f;
                        // Neon player tints read better against a dark cyberpunk board than the classic palette.
                        asset.redTint = new Color(1f, 0.15f, 0.4f);
                        asset.greenTint = new Color(0.15f, 1f, 0.6f);
                        asset.yellowTint = new Color(1f, 0.9f, 0.15f);
                        asset.blueTint = new Color(0.15f, 0.7f, 1f);
                        break;
                }
            }

            private static Material MakeMaterial(Shader shader, Color color, string name, bool transparent = false)
            {
                Material mat = new Material(shader) { name = name };
                if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color")) mat.SetColor("_Color", color);

                if (transparent) MakeTransparent(mat);
                return mat;
            }

            private static Material MakeEmissiveMaterial(Shader shader, Color baseColor, Color emission, string name)
            {
                Material mat = MakeMaterial(shader, baseColor, name);
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", emission * 1.5f);
                    mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                return mat;
            }

            private static void MakeTransparent(Material mat)
            {
                if (mat.HasProperty("_Surface"))
                {
                    // URP Lit / Simple Lit transparent setup.
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
                    // Built-in Standard shader transparent setup (fallback if URP isn't active).
                    mat.SetFloat("_Mode", 3f);
                    mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetInt("_ZWrite", 0);
                    mat.EnableKeyword("_ALPHABLEND_ON");
                    mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
                }
            }
        }
#endif
    }
}
