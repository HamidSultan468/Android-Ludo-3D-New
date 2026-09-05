using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using TangentLudoEmpire.Services; // AuditLog, BackendService

namespace TangentLudoEmpire.Core
{
    /// <summary>
    /// Local-data crypto + device-integrity checks. "Never trust the client": this raises the cost of
    /// tampering, it does not make the client authoritative - the backend still validates every reward.
    ///
    ///  * <see cref="Encrypt"/>/<see cref="Decrypt"/> - AES-256-CBC, key derived (PBKDF2-SHA256) from
    ///    <see cref="SecurityConfig.localDataKeyBase64"/> (or a backend-issued key when available).
    ///    Format: base64( 16-byte IV || ciphertext ).
    ///  * <see cref="GetHash"/> - SHA-256 hex, used to detect out-of-band edits of the save file.
    ///  * <see cref="IsRooted"/> - cheap su/Magisk probe (first line of defence only).
    ///  * <see cref="BindDevice"/> - records SystemInfo.deviceUniqueIdentifier for server-side binding.
    ///
    /// On a load-time hash mismatch: <see cref="AuditLog"/>.Log("TAMPER") and, per
    /// <see cref="SecurityConfig"/>, wipe the local save (gated on server confirmation when offline
    /// self-destruct would otherwise be possible).
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-250)] // before SaveService (-200)
    public class SecurityManager : MonoBehaviour
    {
        public static SecurityManager Instance { get; private set; }

        public string DeviceId { get; private set; } = "";
        public bool DeviceRooted { get; private set; }

        private SecurityConfig _cfg;
        private byte[] _key;   // 32 bytes, lazily derived
        private byte[] _serverKey;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()

            _cfg = SecurityConfig.Load();
            BindDevice();
            DeviceRooted = IsRooted();
            if (DeviceRooted)
                AuditLog.Log("DEVICE_ROOTED", before: "", after: DeviceId);
        }

        // ---------------------------------------------------------------- device ----

        /// <summary>Captures the device identity used for server-side account/device binding.</summary>
        public string BindDevice()
        {
            DeviceId = SystemInfo.deviceUniqueIdentifier;
            return DeviceId;
        }

        /// <summary>Best-effort root/jailbreak probe. Trivially defeated by a serious attacker; useful
        /// only as a signal to the backend.</summary>
        public bool IsRooted()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            string[] paths =
            {
                "/system/app/Superuser.apk", "/sbin/su", "/system/bin/su", "/system/xbin/su",
                "/data/local/xbin/su", "/data/local/bin/su", "/system/sd/xbin/su",
                "/system/bin/failsafe/su", "/data/local/su", "/su/bin/su",
                "/system/app/Magisk.apk", "/sbin/.magisk", "/cache/.disable_magisk",
                "/dev/com.koushikdutta.superuser.daemon/", "/system/xbin/daemonsu"
            };
            foreach (var p in paths)
            {
                try { if (File.Exists(p)) return true; } catch { /* ignore */ }
            }
            // "test-keys" build tag is a weak but common indicator
            try { if (Application.installMode == ApplicationInstallMode.Unknown) { /* no-op */ } } catch { }
            return false;
#else
            return false;
#endif
        }

        // ---------------------------------------------------------------- hashing ----

        public string GetHash(string plain)
        {
            if (plain == null) plain = "";
            using var sha = SHA256.Create();
            byte[] h = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
            var sb = new StringBuilder(h.Length * 2);
            foreach (byte b in h) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        public bool VerifyHash(string plain, string expectedHex) =>
            !string.IsNullOrEmpty(expectedHex) &&
            string.Equals(GetHash(plain), expectedHex, StringComparison.OrdinalIgnoreCase);

        /// <summary>Alias used by the Phase 3 wallet: confirm <paramref name="plain"/> still hashes to
        /// <paramref name="expectedHex"/>. Same as <see cref="VerifyHash"/>.</summary>
        public bool ValidateChecksum(string plain, string expectedHex) => VerifyHash(plain, expectedHex);

        // ---------------------------------------------------------------- AES ----

        /// <summary>A backend-issued key (base64, 32 bytes) takes over from the bundled one once login
        /// completes, if <see cref="SecurityConfig.useServerKeyWhenAvailable"/>.</summary>
        public void SetServerKey(string base64Key)
        {
            try
            {
                var k = Convert.FromBase64String(base64Key ?? "");
                _serverKey = k.Length == 32 ? k : null;
                _key = null; // force re-derive
            }
            catch { _serverKey = null; }
        }

        private byte[] Key()
        {
            if (_key != null) return _key;

            if (_cfg.useServerKeyWhenAvailable && _serverKey != null)
            {
                _key = _serverKey;
                return _key;
            }

            byte[] baseKey;
            try { baseKey = Convert.FromBase64String(_cfg.localDataKeyBase64 ?? ""); }
            catch { baseKey = Array.Empty<byte>(); }

            if (baseKey.Length == 0)
            {
                // No configured key - derive a weak device-bound fallback so the game still runs.
                baseKey = Encoding.UTF8.GetBytes("tangent-fallback-" + SystemInfo.deviceUniqueIdentifier);
                Debug.LogWarning("[SecurityManager] SecurityConfig.localDataKeyBase64 is empty - using a weak device fallback key.");
            }

            byte[] salt;
            try { salt = Convert.FromBase64String(_cfg.kdfSaltBase64 ?? ""); }
            catch { salt = Array.Empty<byte>(); }
            if (salt.Length < 8) salt = Encoding.UTF8.GetBytes("tangent-fixed-salt-v1");

            using var kdf = new Rfc2898DeriveBytes(baseKey, salt, Mathf.Max(10000, _cfg.kdfIterations), HashAlgorithmName.SHA256);
            _key = kdf.GetBytes(32);
            return _key;
        }

        public string Encrypt(string plain)
        {
            if (plain == null) plain = "";
            try
            {
                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.Key = Key();
                aes.GenerateIV();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var enc = aes.CreateEncryptor();
                byte[] pt = Encoding.UTF8.GetBytes(plain);
                byte[] ct = enc.TransformFinalBlock(pt, 0, pt.Length);

                byte[] outBuf = new byte[aes.IV.Length + ct.Length];
                Buffer.BlockCopy(aes.IV, 0, outBuf, 0, aes.IV.Length);
                Buffer.BlockCopy(ct, 0, outBuf, aes.IV.Length, ct.Length);
                return Convert.ToBase64String(outBuf);
            }
            catch (Exception e)
            {
                Debug.LogError($"[SecurityManager] Encrypt failed: {e.Message}");
                return "";
            }
        }

        /// <summary>Returns null on failure (bad key, corrupt data, wrong format) - callers treat null as
        /// "unreadable / tampered".</summary>
        public string Decrypt(string blob)
        {
            if (string.IsNullOrEmpty(blob)) return null;
            try
            {
                byte[] all = Convert.FromBase64String(blob);
                if (all.Length <= 16) return null;

                byte[] iv = new byte[16];
                Buffer.BlockCopy(all, 0, iv, 0, 16);
                byte[] ct = new byte[all.Length - 16];
                Buffer.BlockCopy(all, 16, ct, 0, ct.Length);

                using var aes = Aes.Create();
                aes.KeySize = 256;
                aes.Key = Key();
                aes.IV = iv;
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var dec = aes.CreateDecryptor();
                byte[] pt = dec.TransformFinalBlock(ct, 0, ct.Length);
                return Encoding.UTF8.GetString(pt);
            }
            catch
            {
                return null;
            }
        }

        // ---------------------------------------------------------------- tamper response ----

        /// <summary>Called by <see cref="SaveService"/> when a loaded save fails its hash check.</summary>
        public void HandleTamper(string context, Action wipeAction)
        {
            AuditLog.Log("TAMPER", before: context, after: $"device={DeviceId} rooted={DeviceRooted}");
            Debug.LogWarning($"[SecurityManager] TAMPER detected ({context}).");

            if (!_cfg.wipeOnTamper) return;

            if (_cfg.requireServerConfirmationToLock &&
                (TangentLudoEmpire.Services.BackendService.Instance == null ||
                 !TangentLudoEmpire.Services.BackendService.Instance.IsOnline))
            {
                Debug.LogWarning("[SecurityManager] Offline - deferring wipe until the backend confirms the mismatch.");
                TangentLudoEmpire.Services.BackendService.Instance?.QueueTamperReport(context);
                return;
            }

            wipeAction?.Invoke();
            AuditLog.Log("SAVE_WIPED", before: context, after: "");
        }
    }
}
