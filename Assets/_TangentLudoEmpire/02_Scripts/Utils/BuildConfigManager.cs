using UnityEngine;

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Task D.1. Project-wide production/mock switch - own file for the same MonoScript-identity reason as
    /// every other ScriptableObject config (SecurityConfig, PaymentConfig, TournamentConfig).
    ///
    /// SCOPE NOTE (deferred tech debt - see MIGRATION_NOTES Phase 5): deliberately narrower than
    /// <see cref="TangentLudoEmpire.Payments.PaymentConfig.IsProductionMode"/>, which already switches
    /// JazzCash/Easypaisa between sandbox and live URLs on its own. <see cref="IsProduction"/> instead
    /// gates the GENERIC backend API base URL (a future non-payment backend endpoint - today
    /// <see cref="SecurityConfig.apiBaseUrl"/> is still set by hand) and the <c>PRODUCTION_BUILD</c>
    /// scripting define (toggled by <c>Tangent Ludo Empire/Phase 5/Sync Production Define</c>, see
    /// Phase5Tools). A future pass should unify both flags rather than requiring two toggles kept in sync
    /// by hand - not done here to avoid silently changing payment gateway behaviour as a side effect of
    /// this task.
    /// </summary>
    [CreateAssetMenu(fileName = "BuildConfig", menuName = "Tangent Ludo Empire/Build Config", order = 0)]
    public class BuildConfigManager : ScriptableObject
    {
        [Tooltip("false (default): EffectiveApiBaseUrl resolves to MockApiBaseUrl - safe for internal/dev builds. " +
                 "true: resolves to ProductionApiBaseUrl - only for a real Play Store release build.")]
        public bool IsProduction = false;

        [Header("Mock (default)")]
        public string MockApiBaseUrl = "https://mock.tangent-ludo.local/api";

        [Header("Production")]
        [Tooltip("TODO: fill in before a real production build.")]
        public string ProductionApiBaseUrl = "";

        /// <summary>The URL callers should actually use, gated by <see cref="IsProduction"/>.</summary>
        public string EffectiveApiBaseUrl => IsProduction ? ProductionApiBaseUrl : MockApiBaseUrl;

        private static BuildConfigManager _cached;

        public static BuildConfigManager Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<BuildConfigManager>("BuildConfig");
            if (_cached == null)
            {
                Debug.LogWarning("[BuildConfigManager] No Resources/BuildConfig asset - using in-memory Mock defaults. " +
                                 "Run 'Tangent Ludo Empire/Phase 5/Run Security Test' once to generate it.");
                _cached = CreateInstance<BuildConfigManager>();
            }
            return _cached;
        }
    }
}
