using UnityEngine;

namespace LudoEmpire.Board
{
    public class Tile3D : MonoBehaviour
    {
        [Header("Tile Data")]
        public int tileID;
        public TileType tileType = TileType.Common;
        public PlayerColor colorOwner = PlayerColor.None;
        public bool isSafeTile = false;
        public Vector2Int gridCoordinate;

        [Header("Visual Components")]
        [SerializeField] private MeshRenderer mainRenderer;
        [SerializeField] private MeshRenderer glowBorderRenderer;

        private MaterialPropertyBlock _propBlock;

        private void Awake()
        {
            _propBlock = new MaterialPropertyBlock();
            if (mainRenderer == null) mainRenderer = GetComponent<MeshRenderer>();
        }

        /// <summary>Assigns (or replaces) the thin glow-rim renderer sitting just beneath this tile's main
        /// surface, so future <see cref="SetTileColor"/> calls re-color it automatically instead of it
        /// staying an orphaned, never-updated child.</summary>
        public void SetGlowBorder(MeshRenderer renderer)
        {
            glowBorderRenderer = renderer;
        }

        public void SetTileColor(Color baseColor, Color emissionColor, float emissionIntensity)
        {
            if (mainRenderer == null) mainRenderer = GetComponent<MeshRenderer>();
            if (mainRenderer == null) return;

            if (_propBlock == null) _propBlock = new MaterialPropertyBlock();

            mainRenderer.GetPropertyBlock(_propBlock);
            _propBlock.SetColor("_BaseColor", baseColor);
            _propBlock.SetColor("_EmissionColor", emissionColor * emissionIntensity);
            mainRenderer.SetPropertyBlock(_propBlock);

            if (glowBorderRenderer != null)
            {
                // The rim reads as a brighter halo bleeding out from under the tile, not a flat mirror
                // of the main surface - push its emission further so it visibly separates from the tile.
                _propBlock.Clear();
                glowBorderRenderer.GetPropertyBlock(_propBlock);
                _propBlock.SetColor("_BaseColor", emissionColor);
                _propBlock.SetColor("_EmissionColor", emissionColor * (emissionIntensity * 1.6f));
                glowBorderRenderer.SetPropertyBlock(_propBlock);
            }
        }

        public Vector3 GetTokenSurfacePosition()
        {
            return transform.position + new Vector3(0, 0.12f, 0);
        }
    }
}
