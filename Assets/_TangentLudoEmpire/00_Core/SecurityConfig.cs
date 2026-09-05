using UnityEngine;

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Project-wide security settings, authored as an asset so nothing is baked into code.
    /// Created / repaired by <c>Tangent/Phase 2/Validate Security</c>. Must live in a <b>Resources</b>
    /// folder (<c>Assets/_TangentLudoEmpire/Resources/SecurityConfig.asset</c>) so <see cref="Load"/> can find it.
    ///
    /// SECURITY NOTE: a symmetric key that ships inside the APK is obfuscation, not cryptography -
    /// anyone can pull it out of the binary. <see cref="localDataKeyBase64"/> raises the bar for casual
    /// save-file editors; it is NOT a defence against a determined attacker. The real key must come from
    /// the backend after auth (<see cref="useServerKeyWhenAvailable"/>) and/or the platform keystore.
    /// </summary>
    [CreateAssetMenu(fileName = "SecurityConfig", menuName = "Tangent Ludo Empire/Security Config", order = 0)]
    public class SecurityConfig : ScriptableObject
    {
        [Header("Local data protection (obfuscation tier - see class summary)")]
        [Tooltip("Base64 32-byte key used to derive the AES-256 key for the local .enc save. " +
                 "Regenerate per project; never reuse across apps.")]
        public string localDataKeyBase64 = "";

        [Tooltip("Base64 salt for PBKDF2 key derivation.")]
        public string kdfSaltBase64 = "";

        [Min(10000)]
        public int kdfIterations = 100000;

        [Tooltip("HMAC-SHA256 secret used to sign outgoing API payloads.")]
        public string hmacSecret = "";

        [Header("Backend")]
        public string apiBaseUrl = "";           // empty = offline: every reward validation DENIES
        [Tooltip("Prefer a key delivered by the backend after login over the bundled one.")]
        public bool useServerKeyWhenAvailable = true;

        [Header("Enforcement")]
        [Tooltip("Wipe the local save when a tamper (hash mismatch) is detected on load.")]
        public bool wipeOnTamper = true;
        [Tooltip("Only lock/wipe when the backend is reachable and CONFIRMS the mismatch. " +
                 "Prevents offline play from self-destructing on a false positive.")]
        public bool requireServerConfirmationToLock = true;

        [Header("Anti-cheat limits")]
        public int dailyAdCap = 50;
        [Tooltip("More than this many game-starts inside speedHackWindowSeconds -> flag.")]
        public int speedHackGameLimit = 10;
        public float speedHackWindowSeconds = 60f;
        [Tooltip("Allowed |local - server| coin drift as a fraction of the server value before a wallet lock.")]
        [Range(0f, 1f)]
        public float walletToleranceFraction = 0.1f;

        [Header("Gameplay anti-cheat (Phase 5.1 - see TangentLudoEmpire.Security.GameplayAntiCheatManager)")]
        [Tooltip("Master switch for GameplayAntiCheatManager.ValidateMove's Rule 2 (illegal move) and Rule 3 (speed hack) checks.")]
        public bool EnableAntiCheat = true;
        [Tooltip("Rule 3: minimum seconds allowed between two moves from the same player before a speed-hack flag.")]
        public float MinMoveTime = 0.3f;
        [Tooltip("Rule 1: when true, GameplayAntiCheatManager.RequestRoll is the sole source of dice values " +
                 "(mock server-authoritative roll). Kept distinct from useServerKeyWhenAvailable above, which is about save-key delivery, not dice.")]
        public bool EnforceServerRoll = true;

        private static SecurityConfig _cached;

        /// <summary>Loads (and caches) the config from Resources. Returns a safe in-memory default if the
        /// asset is missing so the game never hard-fails - security features then run in their most
        /// conservative mode (offline, deny rewards).</summary>
        public static SecurityConfig Load()
        {
            if (_cached != null) return _cached;
            _cached = Resources.Load<SecurityConfig>("SecurityConfig");
            if (_cached == null)
            {
                Debug.LogWarning("[SecurityConfig] No Resources/SecurityConfig asset - running with conservative defaults. " +
                                 "Run 'Tangent/Phase 2/Validate Security' to create it.");
                _cached = CreateInstance<SecurityConfig>();
            }
            return _cached;
        }
    }
}
