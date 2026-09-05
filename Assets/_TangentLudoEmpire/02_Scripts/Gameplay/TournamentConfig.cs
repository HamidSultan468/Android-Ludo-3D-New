using UnityEngine;

namespace TangentLudoEmpire.Tournament
{
    /// <summary>
    /// Task C (Phase 4.2 section): app-wide prize-split config - genuinely a ScriptableObject this time
    /// (unlike Phase 3.3's "KYCManager" ask, this really is one shared value for every tournament, not
    /// per-player data). Own file for the same MonoScript-identity reason as SecurityConfig/PaymentConfig.
    /// Auto-created with these defaults by Phase4Tools if the asset is missing.
    /// </summary>
    [CreateAssetMenu(fileName = "TournamentConfig", menuName = "Tangent Ludo Empire/Tournament Config", order = 0)]
    public class TournamentConfig : ScriptableObject
    {
        [Tooltip("Fraction of the prize pool paid to 1st place.")]
        [Range(0f, 1f)] public float WinnerPercent = 0.7f;

        [Tooltip("Fraction of the prize pool paid to 2nd place (DeclareWinner's optional runnerUpId).")]
        [Range(0f, 1f)] public float RunnerUp = 0.2f;

        [Tooltip("Fraction kept by the platform. WinnerPercent + RunnerUp + PlatformFee should sum to 1.0.")]
        [Range(0f, 1f)] public float PlatformFee = 0.1f;

        public int DefaultMaxPlayers = 8;

        private static TournamentConfig _cached;

        public static TournamentConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<TournamentConfig>("TournamentConfig");
            if (_cached == null)
            {
                Debug.LogWarning("[TournamentConfig] No Resources/TournamentConfig asset - using in-memory defaults " +
                                 "(70/20/10). Run a Phase 4 menu item once to generate it.");
                _cached = CreateInstance<TournamentConfig>();
            }
            return _cached;
        }
    }
}
