using System;
using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Services
{
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-260)]
    public class BackendService : MonoBehaviour
    {
        public static BackendService Instance { get; private set; }

        [Header("Runtime (populated from SecurityConfig + login)")]
        [SerializeField] private string apiBaseUrl = "";
        [SerializeField] private string jwtToken = "";
        [SerializeField] private string deviceId = "";

        private string _hmacSecret = "";
        private string _refreshToken = "";
        private bool _refreshing;
        private readonly List<string> _pendingTamperReports = new List<string>();

        public bool IsOnline =>!string.IsNullOrEmpty(apiBaseUrl) && _reachable;
        private bool _reachable;

        public string UserId { get; private set; } = "local";
        public Role CurrentRole { get; private set; } = Role.Player;
        public UserProfile UserProfile { get; private set; }

        private void Awake()
        {
            if (Instance!= null && Instance!= this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject);

            var cfg = SecurityConfig.Load();
            apiBaseUrl = cfg.apiBaseUrl?? "";
            _hmacSecret = cfg.hmacSecret?? "";
            deviceId = SystemInfo.deviceUniqueIdentifier;
            _reachable =!string.IsNullOrEmpty(apiBaseUrl);

            // FIX: Editor میں offline ہو تو بھی گیم چلے
            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                Debug.LogWarning("[BackendService] No apiBaseUrl - Running in OFFLINE/EDITOR mode. Rewards will be auto-approved.");
                #if!UNITY_EDITOR
                if (BuildConfigManager.Load().IsProduction)
                    Debug.LogError("[BackendService] PRODUCTION build with no real SecurityConfig.apiBaseUrl configured!");
                #endif
            }
        }

        public void LoginGuest(Action<bool> done = null)
        {
            // FIX: Editor میں fake login کر دو
            #if UNITY_EDITOR
            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                UserId = "editor_local";
                CurrentRole = Role.Player;
                Debug.Log("[BackendService] Editor Offline Login Success");
                done?.Invoke(true);
                return;
            }
            #endif

            if (string.IsNullOrEmpty(apiBaseUrl)) { done?.Invoke(false); return; }
            string body = JsonUtility.ToJson(new GuestLoginReq { deviceId = deviceId, platform = Application.platform.ToString() });
            StartCoroutine(Send("auth/guest", body, resp =>
            {
                if (resp.ok && TryParse(resp.text, out AuthResp a))
                {
                    jwtToken = a.token; _refreshToken = a.refreshToken;
                    UserId = string.IsNullOrEmpty(a.userId)? "local" : a.userId;
                    CurrentRole = ParseRole(a.role);
                    if (!string.IsNullOrEmpty(a.dataKeyBase64))
                        SecurityManager.Instance?.SetServerKey(a.dataKeyBase64);
                    AuditLog.Log("LOGIN_GUEST", "", UserId);
                }
                done?.Invoke(resp.ok);
            }, allowRefresh: false));
        }

        public void RefreshToken(Action<bool> done = null)
        {
            if (_refreshing || string.IsNullOrEmpty(_refreshToken)) { done?.Invoke(false); return; }
            _refreshing = true;
            string body = JsonUtility.ToJson(new RefreshReq { refreshToken = _refreshToken });
            StartCoroutine(Send("auth/refresh", body, resp =>
            {
                _refreshing = false;
                if (resp.ok && TryParse(resp.text, out AuthResp a))
                {
                    jwtToken = a.token;
                    if (!string.IsNullOrEmpty(a.refreshToken)) _refreshToken = a.refreshToken;
                }
                done?.Invoke(resp.ok);
            }, allowRefresh: false));
        }

        public void ApiPost(string endpoint, string jsonPayload, Action<bool> onDone = null)
        {
            if (string.IsNullOrEmpty(apiBaseUrl)) { onDone?.Invoke(false); return; }
            StartCoroutine(Send(endpoint, jsonPayload, r => onDone?.Invoke(r.ok)));
        }

        public void ValidateReward(string type, long amount, Action<bool> result)
        {
            // FIX: Editor میں ہمیشہ true کر دو
            #if UNITY_EDITOR
            if (string.IsNullOrEmpty(apiBaseUrl))
            {
                Debug.Log($"[BackendService] OFFLINE MODE: Auto-approved reward {type}:{amount}");
                AuditLog.Log("REWARD_OK_OFFLINE", type, amount.ToString());
                result?.Invoke(true);
                return;
            }
            #endif

            if (string.IsNullOrEmpty(apiBaseUrl)) { AuditLog.Log("REWARD_DENY_OFFLINE", type, amount.ToString()); result?.Invoke(false); return; }

            string body = JsonUtility.ToJson(new ValidateReq { type = type, amount = amount, deviceId = deviceId, nonce = Guid.NewGuid().ToString("N") });
            StartCoroutine(Send("reward/validate", body, resp =>
            {
                bool ok = resp.ok && TryParse(resp.text, out ValidateResp v) && v.valid;
                AuditLog.Log(ok? "REWARD_OK" : "REWARD_DENY", $"{type}:{amount}", resp.ok? resp.text : $"http {resp.code}");
                result?.Invoke(ok);
            }));
        }

        public void GetServerCoins(Action<long> result)
        {
            if (string.IsNullOrEmpty(apiBaseUrl)) { result?.Invoke(-1); return; }
            StartCoroutine(Send("wallet/get", "{}", resp =>
            {
                long coins = resp.ok && TryParse(resp.text, out WalletResp w)? w.coins : -1;
                result?.Invoke(coins);
            }));
        }

        public void QueueTamperReport(string context)
        {
            _pendingTamperReports.Add(context);
            if (IsOnline) ApiPost("security/tamper", JsonUtility.ToJson(new TamperReq { context = context, deviceId = deviceId }));
        }

        private struct Resp { public bool ok; public long code; public string text; }

        private IEnumerator Send(string endpoint, string body, Action<Resp> done, bool allowRefresh = true)
        {
            string url = apiBaseUrl.TrimEnd('/') + "/" + endpoint.TrimStart('/');
            byte[] payload = Encoding.UTF8.GetBytes(body?? "");
            string sig = Sign(body?? "");

            const int maxAttempts = 3;
            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                using var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST)
                {
                    uploadHandler = new UploadHandlerRaw(payload),
                    downloadHandler = new DownloadHandlerBuffer(),
                    timeout = 15
                };
                req.SetRequestHeader("Content-Type", "application/json");
                req.SetRequestHeader("X-Device-Id", deviceId);
                req.SetRequestHeader("X-Signature", sig);
                if (!string.IsNullOrEmpty(jwtToken)) req.SetRequestHeader("Authorization", "Bearer " + jwtToken);

                yield return req.SendWebRequest();

                long code = req.responseCode;
                bool networkOk = req.result == UnityWebRequest.Result.Success;
                _reachable = networkOk || code > 0;

                if (networkOk && code >= 200 && code < 300)
                {
                    done?.Invoke(new Resp { ok = true, code = code, text = req.downloadHandler.text });
                    yield break;
                }

                if (code == 401 && allowRefresh)
                {
                    bool refreshed = false;
                    RefreshToken(r => refreshed = r);
                    float t = 0f;
                    while (!refreshed && t < 6f) { t += Time.unscaledDeltaTime; yield return null; }
                    if (refreshed) { sig = Sign(body?? ""); continue; }
                }

                if (code >= 400 && code < 500 && code!= 401)
                {
                    done?.Invoke(new Resp { ok = false, code = code, text = req.downloadHandler.text });
                    yield break;
                }

                if (attempt < maxAttempts)
                    yield return new WaitForSecondsRealtime(Mathf.Pow(2f, attempt));
            }

            _reachable = false;
            done?.Invoke(new Resp { ok = false, code = 0, text = "" });
        }

        private string Sign(string body)
        {
            if (string.IsNullOrEmpty(_hmacSecret)) return "";
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(_hmacSecret));
            byte[] mac = h.ComputeHash(Encoding.UTF8.GetBytes(body));
            var sb = new StringBuilder(mac.Length * 2);
            foreach (byte b in mac) sb.Append(b.ToString("x2"));
            return sb.ToString();
        }

        private static Role ParseRole(string s) =>
            Enum.TryParse(s?? "", true, out Role r)? r : Role.Player;

        private static bool TryParse<T>(string json, out T value)
        {
            try { value = JsonUtility.FromJson<T>(json); return value!= null; }
            catch { value = default; return false; }
        }

        [Serializable] private class GuestLoginReq { public string deviceId; public string platform; }
        [Serializable] private class RefreshReq { public string refreshToken; }
        [Serializable] private class AuthResp { public string token; public string refreshToken; public string userId; public string role; public string dataKeyBase64; }
        [Serializable] private class ValidateReq { public string type; public long amount; public string deviceId; public string nonce; }
        [Serializable] private class ValidateResp { public bool valid; public long grantedAmount; }
        [Serializable] private class WalletResp { public long coins; }
        [Serializable] private class TamperReq { public string context; public string deviceId; }
    }
}