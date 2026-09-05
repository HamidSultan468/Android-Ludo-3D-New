using System;
using System.IO;
using System.Security.Cryptography;
using System.Linq;
using UnityEditor;
using UnityEngine;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Core.EditorTools
{
    /// <summary>
    /// <c>Tangent/Phase 2/Validate Security</c>: creates <c>Resources/SecurityConfig.asset</c> with
    /// fresh keys if missing, checks that every security service is a proper singleton, runs an in-editor
    /// AES round-trip + a mock tamper test, and prints a pass/fail summary.
    /// </summary>
    public static class Phase2SecurityTools
    {
        private const string ResourcesDir = "Assets/_TangentLudoEmpire/Resources";
        private const string ConfigPath   = ResourcesDir + "/SecurityConfig.asset";

        [MenuItem("Tangent Ludo Empire/Phase 2/Validate Security")]
        public static void ValidateSecurity()
        {
            int pass = 0, fail = 0;
            void Check(bool ok, string label)
            {
                if (ok) { pass++; Debug.Log($"[Phase2] PASS  {label}"); }
                else    { fail++; Debug.LogError($"[Phase2] FAIL  {label}"); }
            }

            // 1. Config asset + keys
            var cfg = EnsureConfig();
            Check(cfg != null, "SecurityConfig asset present");
            Check(cfg != null && IsBase64Len(cfg.localDataKeyBase64, 32), "AES-256 local key present (32 bytes base64)");
            Check(cfg != null && IsBase64Len(cfg.kdfSaltBase64, 16), "KDF salt present");
            Check(cfg != null && !string.IsNullOrEmpty(cfg.hmacSecret), "HMAC secret present");

            // 2. Singleton shape (a public static 'Instance' property)
            foreach (var t in new[]
            {
                typeof(SecurityManager),
                typeof(TangentLudoEmpire.Services.BackendService),
                typeof(TangentLudoEmpire.Services.AuditLog),
                typeof(TangentLudoEmpire.Services.AntiCheatManager),
                typeof(SaveService),
            })
            {
                var prop = t.GetProperty("Instance", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
                Check(prop != null && typeof(MonoBehaviour).IsAssignableFrom(t), $"{t.Name} is a MonoBehaviour singleton");
            }

            // 3. AES round-trip + mock tamper (pure editor crypto, mirrors SecurityManager)
            if (cfg != null && IsBase64Len(cfg.localDataKeyBase64, 32))
            {
                try
                {
                    byte[] key = DeriveKey(cfg);
                    string plain = "{\"coins\":1234,\"gamesWon\":7}";
                    string blob = AesEncrypt(key, plain);
                    string back = AesDecrypt(key, blob);
                    Check(back == plain, "AES-256 encrypt/decrypt round-trips");

                    // mock tamper: flip a byte in the ciphertext -> decrypt must fail or produce different text
                    var raw = Convert.FromBase64String(blob);
                    raw[raw.Length - 1] ^= 0xFF;
                    string tampered = null;
                    try { tampered = AesDecrypt(key, Convert.ToBase64String(raw)); } catch { /* expected */ }
                    Check(tampered != plain, "Mock tamper is detected (corrupt ciphertext != original)");

                    // hash-verify path
                    string h = Sha256Hex(plain);
                    Check(Sha256Hex(plain) == h && Sha256Hex(plain + " ") != h, "SHA-256 hash detects a 1-char edit");
                }
                catch (Exception e)
                {
                    Check(false, "Crypto self-test threw: " + e.Message);
                }
            }

            Debug.Log($"Phase 2 Security Validation Complete  -  {pass} passed, {fail} failed.");
            if (fail > 0)
                EditorUtility.DisplayDialog("Phase 2 Security", $"{fail} check(s) FAILED - see Console.", "OK");
        }

        // ---- helpers ----

        private static SecurityConfig EnsureConfig()
        {
            Directory.CreateDirectory(ResourcesDir);
            var cfg = AssetDatabase.LoadAssetAtPath<SecurityConfig>(ConfigPath);
            bool created = false;
            if (cfg == null)
            {
                cfg = ScriptableObject.CreateInstance<SecurityConfig>();
                created = true;
            }

            bool dirty = created;
            if (!IsBase64Len(cfg.localDataKeyBase64, 32)) { cfg.localDataKeyBase64 = RandomBase64(32); dirty = true; }
            if (!IsBase64Len(cfg.kdfSaltBase64, 16))      { cfg.kdfSaltBase64 = RandomBase64(16); dirty = true; }
            if (string.IsNullOrEmpty(cfg.hmacSecret))     { cfg.hmacSecret = RandomBase64(32); dirty = true; }

            if (created) AssetDatabase.CreateAsset(cfg, ConfigPath);
            if (dirty) { EditorUtility.SetDirty(cfg); AssetDatabase.SaveAssets(); }
            if (created || dirty) Debug.Log($"[Phase2] {(created ? "Created" : "Updated")} {ConfigPath}");
            return cfg;
        }

        private static string RandomBase64(int bytes)
        {
            var b = new byte[bytes];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(b);
            return Convert.ToBase64String(b);
        }

        private static bool IsBase64Len(string s, int expectedBytes)
        {
            if (string.IsNullOrEmpty(s)) return false;
            try { return Convert.FromBase64String(s).Length == expectedBytes; } catch { return false; }
        }

        private static byte[] DeriveKey(SecurityConfig cfg)
        {
            byte[] baseKey = Convert.FromBase64String(cfg.localDataKeyBase64);
            byte[] salt = Convert.FromBase64String(cfg.kdfSaltBase64);
            using var kdf = new Rfc2898DeriveBytes(baseKey, salt, Mathf.Max(10000, cfg.kdfIterations), HashAlgorithmName.SHA256);
            return kdf.GetBytes(32);
        }

        private static string AesEncrypt(byte[] key, string plain)
        {
            using var aes = Aes.Create();
            aes.KeySize = 256; aes.Key = key; aes.GenerateIV(); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var enc = aes.CreateEncryptor();
            byte[] pt = System.Text.Encoding.UTF8.GetBytes(plain);
            byte[] ct = enc.TransformFinalBlock(pt, 0, pt.Length);
            byte[] outBuf = aes.IV.Concat(ct).ToArray();
            return Convert.ToBase64String(outBuf);
        }

        private static string AesDecrypt(byte[] key, string blob)
        {
            byte[] all = Convert.FromBase64String(blob);
            byte[] iv = all.Take(16).ToArray();
            byte[] ct = all.Skip(16).ToArray();
            using var aes = Aes.Create();
            aes.KeySize = 256; aes.Key = key; aes.IV = iv; aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var dec = aes.CreateDecryptor();
            byte[] pt = dec.TransformFinalBlock(ct, 0, ct.Length);
            return System.Text.Encoding.UTF8.GetString(pt);
        }

        private static string Sha256Hex(string s)
        {
            using var sha = SHA256.Create();
            return string.Concat(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(s)).Select(b => b.ToString("x2")));
        }
    }
}
