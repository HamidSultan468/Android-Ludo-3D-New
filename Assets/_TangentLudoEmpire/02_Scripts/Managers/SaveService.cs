using System;
using System.IO;
using UnityEngine;

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// The single owner of persisted player data.
    ///
    /// PHASE 2: the profile is stored AES-256 encrypted at
    /// <c>Application.persistentDataPath/tle_profile_v1.enc</c> inside a small envelope that also
    /// carries a SHA-256 of the plaintext. On load the hash is re-checked; a mismatch (out-of-band edit)
    /// is a TAMPER event - <see cref="SecurityManager"/> logs it and, per <see cref="SecurityConfig"/>,
    /// wipes the save (coins reset to 0). No PlayerPrefs are used for storage; the only PlayerPrefs
    /// access is a one-time read to migrate the legacy keys out.
    ///
    /// Migration order on load: .enc -> .enc.bak -> Phase-1 plaintext .json -> legacy PlayerPrefs -> new.
    /// Nothing here changes how coins are earned (<c>CurrencyManager</c> still does that);
    /// <see cref="WalletManager"/> mirrors balance changes into the profile.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-200)] // after SecurityManager (-250), before CurrencyManager (-100)
    public class SaveService : MonoBehaviour
    {
        public static SaveService Instance { get; private set; }

        public UserProfile Profile { get; private set; }
        public event Action<UserProfile> OnProfileChanged;

        private string EncPath   => Path.Combine(Application.persistentDataPath, "tle_profile_v1.enc");
        private string BakPath   => EncPath + ".bak";
        private string JsonPath  => Path.Combine(Application.persistentDataPath, AppConstants.PROFILE_FILE_NAME); // Phase 1 file

        private SecurityManager Sec => SecurityManager.Instance;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            Profile = LoadProfile();
        }

        // ---------------------------------------------------------------- load ----

        public UserProfile LoadProfile()
        {
            // 1 + 2: encrypted file (or its backup)
            var p = TryReadEncrypted(EncPath) ?? TryReadEncrypted(BakPath);
            if (p != null) { Debug.Log($"[SaveService] Loaded .enc profile ({p.coins} coins)."); return p; }

            // 3: Phase 1 plaintext JSON -> upgrade to .enc
            try
            {
                if (File.Exists(JsonPath))
                {
                    var j = UserProfile.FromJson(File.ReadAllText(JsonPath));
                    if (j != null)
                    {
                        Debug.Log("[SaveService] Upgrading Phase-1 .json profile to encrypted .enc.");
                        if (SaveProfile(j)) { SafeDelete(JsonPath); }
                        return Profile ?? j;
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning($"[SaveService] .json upgrade failed: {e.Message}"); }

            // 4: legacy PlayerPrefs -> migrate out (one-time), then delete the keys
            var migrated = MigrateFromLegacyPlayerPrefs();
            if (migrated != null) return migrated;

            // 5: brand new
            var created = UserProfile.CreateNew();
            SaveProfile(created);
            Debug.Log("[SaveService] Created a fresh encrypted profile.");
            return created;
        }

        private UserProfile TryReadEncrypted(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                Envelope env = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if (env == null || string.IsNullOrEmpty(env.data)) return null;

                string json = Sec != null ? Sec.Decrypt(env.data) : null;
                if (json == null)
                {
                    // Could not decrypt (bad key / corrupt) - treat as tamper/unreadable.
                    Sec?.HandleTamper($"decrypt-failed:{Path.GetFileName(path)}", WipeSave);
                    return null;
                }

                string expected = env.hash;
                if (Sec != null && !string.IsNullOrEmpty(expected) && !Sec.VerifyHash(json, expected))
                {
                    Sec.HandleTamper($"hash-mismatch:{Path.GetFileName(path)}", WipeSave);
                    return null;
                }

                return UserProfile.FromJson(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] read {Path.GetFileName(path)} failed: {e.Message}");
                return null;
            }
        }

        private UserProfile MigrateFromLegacyPlayerPrefs()
        {
            // We only ever READ PlayerPrefs here, to move data OUT of it.
            try
            {
                if (PlayerPrefs.GetInt(AppConstants.SAVE_KEY_MIGRATED, 0) == 1) return null;
                bool hasLegacy = PlayerPrefs.HasKey(AppConstants.LEGACY_KEY_COINS) || PlayerPrefs.HasKey(AppConstants.LEGACY_KEY_GAMES_WON);
                if (!hasLegacy) return null;

                var profile = UserProfile.CreateNew();
                string rawCoins = PlayerPrefs.GetString(AppConstants.LEGACY_KEY_COINS, "0");
                profile.coins = long.TryParse(rawCoins, out long c) ? Math.Max(0, c) : 0;
                profile.gamesWon = Math.Max(0, PlayerPrefs.GetInt(AppConstants.LEGACY_KEY_GAMES_WON, 0));

                if (!SaveProfile(profile)) return null;                 // write .enc
                var readback = TryReadEncrypted(EncPath);               // verify round-trip
                if (readback == null || readback.coins != profile.coins)
                {
                    Debug.LogError("[SaveService] Legacy migration verify failed - keeping PlayerPrefs, will retry.");
                    return null;
                }

                PlayerPrefs.DeleteKey(AppConstants.LEGACY_KEY_COINS);
                PlayerPrefs.DeleteKey(AppConstants.LEGACY_KEY_GAMES_WON);
                PlayerPrefs.DeleteKey(AppConstants.SAVE_KEY_PROFILE); // drop the old Phase-1 mirror too
                PlayerPrefs.SetInt(AppConstants.SAVE_KEY_MIGRATED, 1);
                PlayerPrefs.Save();

                Debug.Log($"[SaveService] Migrated legacy PlayerPrefs -> encrypted .enc ({profile.coins} coins).");
                TangentLudoEmpire.Services.AuditLog.Log("SAVE_MIGRATED", "PlayerPrefs", $"{profile.coins} coins");
                return readback;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Legacy migration threw ({e.Message}); PlayerPrefs untouched.");
                return null;
            }
        }

        // ---------------------------------------------------------------- save ----

        public bool SaveProfile(UserProfile p)
        {
            if (p == null) return false;
            p.updatedAt = DateTime.UtcNow.ToString("o");
            if (string.IsNullOrEmpty(p.createdAt)) p.createdAt = p.updatedAt;

            bool ok = WriteEncrypted(p);
            if (ok)
            {
                Profile = p;
                OnProfileChanged?.Invoke(p);
            }
            return ok;
        }

        private bool WriteEncrypted(UserProfile p)
        {
            try
            {
                string json = p.ToJson();

                Envelope env;
                if (Sec != null)
                {
                    env = new Envelope { v = 2, hash = Sec.GetHash(json), data = Sec.Encrypt(json) };
                    if (string.IsNullOrEmpty(env.data)) throw new Exception("encrypt returned empty");
                }
                else
                {
                    // SecurityManager not up yet - fail safe to a plaintext file so the game still runs.
                    Debug.LogWarning("[SaveService] SecurityManager unavailable - writing UNENCRYPTED fallback.");
                    File.WriteAllText(JsonPath, json);
                    return true;
                }

                string envJson = JsonUtility.ToJson(env);
                string tmp = EncPath + ".tmp";
                File.WriteAllText(tmp, envJson);

                if (File.Exists(EncPath))
                {
                    SafeDelete(BakPath);
                    File.Move(EncPath, BakPath); // keep the previous good copy
                }
                File.Move(tmp, EncPath);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] Encrypted write failed: {e.Message}");
                return false;
            }
        }

        // ---------------------------------------------------------------- mutations / helpers ----

        public void SetCoins(long coins)
        {
            if (Profile == null) Profile = LoadProfile();
            if (Profile.coins == coins) return;
            long before = Profile.coins;
            Profile.coins = Math.Max(0, coins);
            SaveProfile(Profile);
            TangentLudoEmpire.Services.AuditLog.Log("COINS_SAVE", before.ToString(), Profile.coins.ToString());
        }

        public void SetGamesWon(int wins)
        {
            if (Profile == null) Profile = LoadProfile();
            if (Profile.gamesWon == wins) return;
            Profile.gamesWon = Math.Max(0, wins);
            SaveProfile(Profile);
        }

        /// <summary>Tamper response: reset progression to zero and persist. Identity is kept.</summary>
        public void WipeSave()
        {
            var fresh = UserProfile.CreateNew();
            if (Profile != null)
            {
                fresh.profileId = Profile.profileId;      // keep identity
                fresh.displayName = Profile.displayName;
                fresh.createdAt = Profile.createdAt;
            }
            fresh.coins = 0;
            fresh.diamonds = 0;
            fresh.gamesWon = 0;
            Profile = fresh;
            WriteEncrypted(fresh);
            SafeDelete(BakPath);
            OnProfileChanged?.Invoke(fresh);
            Debug.LogWarning("[SaveService] Save WIPED (coins reset to 0).");
        }

        private static void SafeDelete(string path)
        {
            try { if (File.Exists(path)) File.Delete(path); } catch { /* ignore */ }
        }

        // ---------------------------------------------------------------- generic encrypted blobs (Phase 3+) ----
        //
        // Same envelope + crypto as the profile, for any other [Serializable] object (e.g. the wallet).
        // File name is relative to Application.persistentDataPath. A ".bak" is kept on every write and a
        // hash mismatch on load is reported to SecurityManager as a TAMPER (no wipe - the caller owns
        // recovery, since there is no generic "reset" for arbitrary types).

        private string BlobPath(string fileName) => Path.Combine(Application.persistentDataPath, fileName);

        public bool EncryptedFileExists(string fileName) => File.Exists(BlobPath(fileName));

        public void DeleteEncrypted(string fileName)
        {
            string p = BlobPath(fileName);
            SafeDelete(p); SafeDelete(p + ".bak"); SafeDelete(p + ".tmp");
        }

        /// <summary>Serialise <paramref name="obj"/> to JSON, AES-256 encrypt, wrap in the SHA-256 envelope,
        /// and write atomically (keeping a ".bak"). Returns false on any failure.</summary>
        public bool SaveEncrypted<T>(string fileName, T obj) where T : class
        {
            if (obj == null) return false;
            try
            {
                string json = JsonUtility.ToJson(obj);
                if (Sec == null)
                {
                    Debug.LogWarning($"[SaveService] SecurityManager unavailable - '{fileName}' NOT written (encrypted stores require it).");
                    return false;
                }

                var env = new Envelope { v = 3, hash = Sec.GetHash(json), data = Sec.Encrypt(json) };
                if (string.IsNullOrEmpty(env.data)) throw new Exception("encrypt returned empty");

                string path = BlobPath(fileName);
                string tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonUtility.ToJson(env));
                if (File.Exists(path))
                {
                    SafeDelete(path + ".bak");
                    File.Move(path, path + ".bak");
                }
                File.Move(tmp, path);
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"[SaveService] SaveEncrypted<{typeof(T).Name}>('{fileName}') failed: {e.Message}");
                return false;
            }
        }

        /// <summary>Read + decrypt + hash-verify <paramref name="fileName"/> (falling back to its ".bak").
        /// Returns null if the file is missing, unreadable, or fails its hash (a TAMPER is logged in that
        /// last case).</summary>
        public T LoadEncrypted<T>(string fileName) where T : class
        {
            string path = BlobPath(fileName);
            string json = ReadBlobPlaintext(path) ?? ReadBlobPlaintext(path + ".bak");
            if (json == null) return null;
            try { return JsonUtility.FromJson<T>(json); }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] LoadEncrypted<{typeof(T).Name}>('{fileName}') parse failed: {e.Message}");
                return null;
            }
        }

        private string ReadBlobPlaintext(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                Envelope env = JsonUtility.FromJson<Envelope>(File.ReadAllText(path));
                if (env == null || string.IsNullOrEmpty(env.data)) return null;

                string json = Sec != null ? Sec.Decrypt(env.data) : null;
                if (json == null)
                {
                    Sec?.HandleTamper($"decrypt-failed:{Path.GetFileName(path)}", null);
                    return null;
                }
                if (Sec != null && !string.IsNullOrEmpty(env.hash) && !Sec.VerifyHash(json, env.hash))
                {
                    Sec.HandleTamper($"hash-mismatch:{Path.GetFileName(path)}", null);
                    return null;
                }
                return json;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[SaveService] read blob {Path.GetFileName(path)} failed: {e.Message}");
                return null;
            }
        }

        [Serializable]
        private class Envelope
        {
            public int v = 2;
            public string hash;   // SHA-256 hex of the plaintext profile JSON
            public string data;   // AES-256 blob (base64: IV || ciphertext)
        }
    }
}
