using UnityEngine;

namespace LudoEmpire.Ludo
{
    /// <summary>
    /// Marks one piece of the board as re-skinnable. Every element has a stable "anchor" transform
    /// (the GameObject this component lives on - the same Transform gameplay code tracks: a
    /// waypoint, a token's <see cref="LudoToken.Visual"/>, etc.) and a separate, swappable
    /// <see cref="VisualSlot"/> child holding the actual mesh. <see cref="LudoThemeManager"/> only
    /// ever destroys/recreates children under <see cref="VisualSlot"/> - the anchor itself is never
    /// touched, so re-theming can never desync grid tracking or move a token/waypoint.
    /// </summary>
    [DisallowMultipleComponent]
    public class LudoThemedElement : MonoBehaviour
    {
        [SerializeField] private LudoThemeElementKind kind;
        [Tooltip("Child transform whose contents get replaced on theme swap. Leave empty (Dice) to re-skin this GameObject's own Renderer/MeshFilter in place instead - required wherever physics components must never be recreated.")]
        [SerializeField] private Transform visualSlot;
        [SerializeField] private bool useColorTint = true;
        [SerializeField] private bool hasOwnerColor;
        [SerializeField] private PlayerColor ownerColor;
        [Tooltip("Used when this element has no owner color (e.g. a neutral or star track tile) - a fixed tint baked in at build time, independent of theme.")]
        [SerializeField] private Color fixedTint = Color.white;

        public LudoThemeElementKind Kind => kind;
        public Transform VisualSlot => visualSlot != null ? visualSlot : transform;
        public bool UseColorTint => useColorTint;
        public PlayerColor? OwnerColor => hasOwnerColor ? ownerColor : (PlayerColor?)null;
        public Color FixedTint => fixedTint;

        /// <summary>Called once by the scene builder right after this component is added.</summary>
        public void Configure(LudoThemeElementKind elementKind, Transform slot, PlayerColor? owner = null, Color? tint = null, bool tintEnabled = true)
        {
            kind = elementKind;
            visualSlot = slot;
            useColorTint = tintEnabled;
            hasOwnerColor = owner.HasValue;
            ownerColor = owner ?? default;
            fixedTint = tint ?? Color.white;
        }
    }
}
