using UnityEngine;

namespace LudoGame.VFX
{
    /// <summary>
    /// Makes an object's material glow like the neon-red pieces in this
    /// project's art style, and lets it pulse automatically or be set to a
    /// fixed brightness (e.g. brighter during a player's turn).
    ///
    /// Setup (in the Unity Editor):
    /// 1. Put this script on any object with a Renderer (a token, a board
    ///    cell, a UI-adjacent 3D piece, etc.).
    /// 2. On that object's MATERIAL, tick the "Emission" checkbox once (in
    ///    the Inspector) and pick roughly the color you want to glow - this
    ///    script only adjusts brightness/color at runtime, it can't turn
    ///    emission on for a material that has it off.
    /// 3. Set "Glow Color" and tweak Min/Max Intensity + Pulse Speed to taste.
    /// 4. Leave "Pulse Automatically" on for a constant idle glow, or call
    ///    SetIntensity(value) from another script (e.g. TurnIndicatorUI) to
    ///    flare it up during that player's turn, then ResumeAutoPulse() after.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class NeonGlowController : MonoBehaviour
    {
        [Header("Glow Settings")]
        [SerializeField] private Color glowColor = Color.red;
        [SerializeField] private float minIntensity = 0.5f;
        [SerializeField] private float maxIntensity = 2.5f;
        [SerializeField] private float pulseSpeed = 2f;
        [SerializeField] private bool pulseAutomatically = true;

        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        private Renderer targetRenderer;
        private MaterialPropertyBlock propertyBlock;
        private float manualIntensity;

        private void Awake()
        {
            targetRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            manualIntensity = maxIntensity;
        }

        private void Update()
        {
            float intensity = pulseAutomatically
                ? Mathf.Lerp(minIntensity, maxIntensity, (Mathf.Sin(Time.time * pulseSpeed) + 1f) * 0.5f)
                : manualIntensity;

            ApplyGlow(intensity);
        }

        /// <summary>Stops the automatic pulse and holds the glow at a fixed intensity.</summary>
        public void SetIntensity(float intensity)
        {
            pulseAutomatically = false;
            manualIntensity = intensity;
        }

        /// <summary>Re-enables the automatic pulsing animation.</summary>
        public void ResumeAutoPulse()
        {
            pulseAutomatically = true;
        }

        public void SetGlowColor(Color color)
        {
            glowColor = color;
        }

        private void ApplyGlow(float intensity)
        {
            targetRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorId, glowColor * intensity);
            targetRenderer.SetPropertyBlock(propertyBlock);
        }
    }
}
