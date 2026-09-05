using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
#if UNITY_EDITOR || UNITY_STANDALONE
using System.Net;
using System.Net.Sockets;
using System.Threading;
#endif
using UnityEngine;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Local sandbox-testing callback receiver for JazzCash/Easypaisa's browser redirect
    /// (<c>http://localhost:8081/callback?...</c>). EDITOR / STANDALONE ONLY, and dev-loop only.
    ///
    /// Built on raw <see cref="TcpListener"/> + hand-rolled HTTP/1.1 GET parsing, not
    /// <c>System.Net.HttpListener</c> - this project's Api Compatibility Level is .NET Standard 2.0
    /// (<c>ProjectSettings.asset: apiCompatibilityLevel: 6</c>), which does not include HttpListener;
    /// TcpListener/TcpClient are in netstandard2.0 and compile everywhere the <c>#if</c> below allows.
    ///
    /// <b>PRODUCTION NOTE - this does NOT work on a shipped Android/iOS build.</b> A real phone's browser
    /// cannot reach "localhost" back into a different app's process, and there is no meaningful listening
    /// server on-device to redirect to anyway. A real integration needs one of:
    ///   (a) the gateway's redirect hits your BACKEND's public HTTPS webhook, which then pushes the
    ///       result to the client over your own API/websocket, or
    ///   (b) an app-registered custom URL scheme / Android App Link that the OS hands back to this app
    ///       as an Intent, which the app then confirms with your backend.
    /// Neither is wired up here. This listener exists purely so the sandbox flow is testable from the
    /// Unity Editor (or a Standalone dev build) during development - see MIGRATION_NOTES Phase 3.2.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaymentCallbackListener : MonoBehaviour
    {
        public const int Port = 8081;
        public const string CallbackPath = "/callback";
        public static string CallbackUrl => $"http://localhost:{Port}{CallbackPath}";

        private const float TimeoutSeconds = 120f;

        public static PaymentCallbackListener Instance { get; private set; }

        /// <summary>orderId, success, txnId, amount. Always raised on the Unity main thread.</summary>
        public event Action<string, bool, string, decimal> OnPaymentComplete;

        public bool IsListening { get; private set; }

        private float _startedAt;
        private readonly Queue<Action> _mainThreadQueue = new Queue<Action>();
        private readonly object _queueLock = new object();

#if UNITY_EDITOR || UNITY_STANDALONE
        private TcpListener _listener;
        private Thread _listenThread;
        private volatile bool _stopRequested;
#endif

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
        }

        private void OnDestroy()
        {
            if (Instance == this) StopListening();
        }

        /// <summary>Starts (or no-ops if already running) the local listener. Auto-stops after one
        /// callback or <see cref="TimeoutSeconds"/>, whichever comes first (Task 2.5).</summary>
        public void StartListening()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (IsListening) return;
            try
            {
                _listener = new TcpListener(IPAddress.Loopback, Port);
                _listener.Start();
                IsListening = true;
                _stopRequested = false;
                _startedAt = Time.realtimeSinceStartup;
                _listenThread = new Thread(ListenLoop) { IsBackground = true, Name = "PaymentCallbackListener" };
                _listenThread.Start();
                Debug.Log($"[PaymentCallbackListener] Listening on {CallbackUrl} (stops after 1 callback or {TimeoutSeconds:0}s).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[PaymentCallbackListener] Failed to start on port {Port}: {e.Message}");
                IsListening = false;
            }
#else
            Debug.LogWarning("[PaymentCallbackListener] Not available on this platform (Editor/Standalone only) - " +
                              "see the class remarks for what a shipped build needs instead.");
#endif
        }

        public void StopListening()
        {
#if UNITY_EDITOR || UNITY_STANDALONE
            if (!IsListening) return;
            _stopRequested = true;
            try { _listener?.Stop(); } catch { /* ignore - shutting down */ }
            IsListening = false;
            Debug.Log("[PaymentCallbackListener] Stopped.");
#endif
        }

        private void Update()
        {
            // Drain anything the background listen thread queued, on the main thread.
            while (true)
            {
                Action a;
                lock (_queueLock)
                {
                    if (_mainThreadQueue.Count == 0) break;
                    a = _mainThreadQueue.Dequeue();
                }
                a();
            }

            if (IsListening && Time.realtimeSinceStartup - _startedAt > TimeoutSeconds)
            {
                Debug.LogWarning($"[PaymentCallbackListener] No callback within {TimeoutSeconds:0}s - stopping.");
                StopListening();
            }
        }

#if UNITY_EDITOR || UNITY_STANDALONE
        // ---------------------------------------------------------------- background listen thread ----

        private void ListenLoop()
        {
            try
            {
                while (!_stopRequested)
                {
                    if (!_listener.Pending()) { Thread.Sleep(100); continue; }

                    using TcpClient client = _listener.AcceptTcpClient();
                    using NetworkStream stream = client.GetStream();

                    string requestLine = ReadLine(stream);
                    DrainHeaders(stream);
                    WriteSimpleHtmlResponse(stream);

                    string path = ExtractPathAndQuery(requestLine, out string query);
                    if (!string.IsNullOrEmpty(path) && path.StartsWith(CallbackPath, StringComparison.OrdinalIgnoreCase))
                    {
                        var data = ParseQuery(query);
                        lock (_queueLock) { _mainThreadQueue.Enqueue(() => HandleCallbackData(data)); }
                        break; // one callback then stop (Task 2.5)
                    }
                }
            }
            catch (SocketException) { /* listener was Stop()'d - expected on shutdown */ }
            catch (ObjectDisposedException) { /* same */ }
            catch (Exception e) { Debug.LogError("[PaymentCallbackListener] listen loop error: " + e.Message); }
        }

        private static string ReadLine(NetworkStream stream)
        {
            var sb = new StringBuilder();
            int b;
            while ((b = stream.ReadByte()) != -1)
            {
                if (b == '\n') break;
                if (b != '\r') sb.Append((char)b);
            }
            return sb.ToString();
        }

        private static void DrainHeaders(NetworkStream stream)
        {
            string line;
            do { line = ReadLine(stream); } while (!string.IsNullOrEmpty(line));
        }

        private static void WriteSimpleHtmlResponse(NetworkStream stream)
        {
            const string body = "<html><body>Payment received - you can close this window and return to the app.</body></html>";
            byte[] bodyBytes = Encoding.UTF8.GetBytes(body);
            string header = "HTTP/1.1 200 OK\r\nContent-Type: text/html; charset=utf-8\r\nContent-Length: " +
                             bodyBytes.Length + "\r\nConnection: close\r\n\r\n";
            byte[] headerBytes = Encoding.ASCII.GetBytes(header);
            stream.Write(headerBytes, 0, headerBytes.Length);
            stream.Write(bodyBytes, 0, bodyBytes.Length);
            stream.Flush();
        }

        private static string ExtractPathAndQuery(string requestLine, out string query)
        {
            query = "";
            // "GET /callback?a=1&b=2 HTTP/1.1"
            if (string.IsNullOrEmpty(requestLine)) return "";
            var parts = requestLine.Split(' ');
            if (parts.Length < 2) return "";
            string target = parts[1];
            int qi = target.IndexOf('?');
            if (qi >= 0) { query = target.Substring(qi + 1); return target.Substring(0, qi); }
            return target;
        }
#endif

        /// <summary>Pure parsing, no networking - safe to call from a headless editor test.</summary>
        public static Dictionary<string, string> ParseQuery(string query)
        {
            var result = new Dictionary<string, string>();
            if (string.IsNullOrEmpty(query)) return result;
            foreach (var pair in query.TrimStart('?').Split('&'))
            {
                if (string.IsNullOrEmpty(pair)) continue;
                int eq = pair.IndexOf('=');
                string key = eq >= 0 ? Uri.UnescapeDataString(pair.Substring(0, eq)) : Uri.UnescapeDataString(pair);
                string val = eq >= 0 ? Uri.UnescapeDataString(pair.Substring(eq + 1)) : "";
                result[key] = val;
            }
            return result;
        }

        // ---------------------------------------------------------------- main-thread handling ----

        /// <summary>Detects which gateway's callback shape this is, verifies its hash (Task 5 -
        /// CALLBACK_TAMPERED on mismatch), and fires <see cref="OnPaymentComplete"/>. Runs on the main
        /// thread (queued from <see cref="ListenLoop"/>). Exposed <c>internal</c> so
        /// <c>Phase3Tools</c>'s headless test can drive it directly without a live socket.</summary>
        internal void HandleCallbackData(Dictionary<string, string> data)
        {
            IsListening = false;

            bool isJazzCash = data.ContainsKey("pp_ResponseCode");
            bool isEasypaisa = !isJazzCash && data.ContainsKey("responseCode");

            if (!isJazzCash && !isEasypaisa)
            {
                Debug.LogWarning("[PaymentCallbackListener] Callback didn't match a known gateway shape - ignored.");
                return;
            }

            var provider = isJazzCash ? PaymentProvider.JazzCash : PaymentProvider.Easypaisa;
            var gateway = PaymentGatewayFactory.GetGateway(provider);

            string orderId = isJazzCash
                ? (data.TryGetValue("pp_BillReference", out var r1) ? r1 : "")
                : (data.TryGetValue("orderId", out var r2) ? r2 : "");

            if (!gateway.ValidateCallbackHash(data))
            {
                // ValidateCallbackHash already logged CALLBACK_TAMPERED with its own context.
                AuditLog.Log("CALLBACK_TAMPERED", provider.ToString(), orderId);
                OnPaymentComplete?.Invoke(orderId, false, "HASH_MISMATCH", 0m);
                return;
            }

            bool success = isJazzCash
                ? (data.TryGetValue("pp_ResponseCode", out var rc1) && rc1 == "000")
                : (data.TryGetValue("responseCode", out var rc2) && rc2 == "0000");

            string txnId = isJazzCash
                ? (data.TryGetValue("pp_TxnRefNo", out var t1) ? t1 : orderId)
                : (data.TryGetValue("transactionId", out var t2) ? t2 : orderId);

            decimal amount = 0m;
            if (isJazzCash && data.TryGetValue("pp_Amount", out var pa) && long.TryParse(pa, out var paisa))
                amount = paisa / 100m;
            else if (isEasypaisa && data.TryGetValue("amount", out var am) &&
                     decimal.TryParse(am, NumberStyles.Number, CultureInfo.InvariantCulture, out var d))
                amount = d;

            Debug.Log(success
                ? $"[PaymentCallbackListener] {provider} SUCCESS: {txnId}"
                : $"[PaymentCallbackListener] {provider} FAILED (order {orderId}).");

            OnPaymentComplete?.Invoke(orderId, success, txnId, amount);
        }
    }
}
