using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>The four visual presets "Ludo Empire: 3D + Jungle Tycoon" ships with.</summary>
    public enum LudoThemeId
    {
        Glass,
        Stone,
        Jungle,
        Cyberpunk
    }

    /// <summary>
    /// A single re-skin of the Ludo board: materials/prefabs for every themable surface (board,
    /// tiles, home bases, center, tokens, dice), particle FX, and a skybox/lighting setup.
    /// Consumed by <see cref="LudoThemeManager"/>, which applies it to a live board without
    /// touching <see cref="LudoBoardLogic"/> state. All fields are optional - a theme that leaves
    /// a prefab or material empty simply falls back to whatever is already on the board, so a
    /// partially-authored theme never breaks the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "New LudoThemeData", menuName = "Ludo Empire/Theme Data")]
    public class LudoThemeData : ScriptableObject
    {
        [Header("Identity")]
        public LudoThemeId themeId = LudoThemeId.Glass;
        public string displayName = "Glass";
        [TextArea] public string description;
        public Sprite previewIcon;

        [Header("Board Base")]
        [Tooltip("Material applied to the board plate. Used as-is if boardBasePrefab is empty.")]
        public Material boardMaterial;
        [Tooltip("Optional full replacement visual for the board base (instanced under its stable anchor). Leave empty to just re-skin the built-in board plate.")]
        public GameObject boardBasePrefab;

        [Header("Track & Home-Stretch Tiles")]
        [Tooltip("Material template tiles are cloned+tinted from when no tilePrefab is supplied.")]
        public Material tileMaterial;
        [Tooltip("Optional prefab instanced (and then color-tinted) for every one of the 52 track tiles and the 4x6 home-stretch tiles.")]
        public GameObject tilePrefab;

        [Header("Player Home Bases")]
        public Material homeBaseMaterial;
        [Tooltip("Optional prefab instanced for each of the 4 player home-base floors.")]
        public GameObject homeBasePrefab;

        [Header("Center Home")]
        public Material centerHomeMaterial;
        public GameObject centerHomePrefab;

        [Header("Tokens (Gotis)")]
        public Material tokenMaterial;
        [Tooltip("Optional prefab instanced (and then color-tinted) for all 16 goti tokens.")]
        public GameObject tokenPrefab;

        [Header("Dice")]
        [Tooltip("Applied in place on the existing dice GameObject - the dice keeps its Rigidbody/Collider/LudoDiceRoller intact so physics never resets.")]
        public Material diceMaterial;
        [Tooltip("Optional mesh swapped onto the dice's existing MeshFilter (and MeshCollider, if present).")]
        public Mesh diceMesh;

        [Header("Particle FX")]
        public ParticleSystem captureFX;
        public ParticleSystem tokenHomeFX;
        public ParticleSystem diceSixRolledFX;
        [Tooltip("Optional idle/ambient effect (fireflies, sparks, dust motes...) that best matches this theme's mood.")]
        public ParticleSystem ambientFX;

        [Header("Skybox")]
        [Tooltip("Left empty to keep whichever skybox the scene had before any theme was ever applied.")]
        public Material skyboxMaterial;

        [Header("Lighting Rig")]
        public Color ambientLightColor = new Color(0.5f, 0.5f, 0.5f);
        [Range(0f, 8f)] public float ambientIntensity = 1f;
        public Color sunColor = Color.white;
        [Range(0f, 8f)] public float sunIntensity = 1.2f;
        [Tooltip("Euler angles for the directional 'sun' light.")]
        public Vector3 sunEulerRotation = new Vector3(50f, -30f, 0f);

        [Header("Fog")]
        public bool fogEnabled;
        public Color fogColor = Color.gray;
        [Range(0f, 0.5f)] public float fogDensity = 0.01f;

        [Header("Audio (optional)")]
        public AudioClip ambientMusic;
        public AudioClip tileClickSfx;

        [Header("Unlocking")]
        [Tooltip("Coins required to unlock this theme in the shop. 0 (or less) means it's free and unlocked by default - no purchase or PlayerPrefs entry needed.")]
        [Min(0)] public int unlockCost = 0;

        /// <summary>True for a theme that never needs to be purchased (e.g. the default Glass preset).</summary>
        public bool IsFreeByDefault => unlockCost <= 0;

        [Header("Player Color Tints")]
        [Tooltip("Per-theme restyle of the classic Red/Green/Yellow/Blue palette (e.g. neon tints for Cyberpunk). Tokens, start tiles, home stretches and home bases are all tinted from these.")]
        public Color redTint = new Color(0.85f, 0.15f, 0.15f);
        public Color greenTint = new Color(0.15f, 0.65f, 0.20f);
        public Color yellowTint = new Color(0.95f, 0.80f, 0.10f);
        public Color blueTint = new Color(0.15f, 0.35f, 0.90f);

        /// <summary>Resolves this theme's tint for a given player color. Never throws on an unmapped enum value.</summary>
        public Color GetPlayerTint(PlayerColor color)
        {
            return color switch
            {
                PlayerColor.Red => redTint,
                PlayerColor.Green => greenTint,
                PlayerColor.Yellow => yellowTint,
                PlayerColor.Blue => blueTint,
                _ => Color.white
            };
        }
    }
}
