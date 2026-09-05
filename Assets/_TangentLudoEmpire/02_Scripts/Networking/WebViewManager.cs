using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Opens a payment page for the user to complete 3D-Secure / OTP, then resolves once
    /// <see cref="PaymentCallbackListener"/> reports the matching order's redirect (or a 120s timeout).
    ///
    /// No BestHTTP / UniWebView package is installed in this project, so this always uses
    /// <see cref="Application.OpenURL"/> (the device's external browser) rather than an in-app WebView -
    /// functionally equivalent for a redirect-based checkout (the whole point is the gateway's own 3D
    /// Secure page, which is exactly what a real WebView would also just be showing). If BestHTTP/
    /// UniWebView is added later, swap the one <see cref="Application.OpenURL"/> call below for an
    /// embedded view - nothing else in the payment flow needs to change.
    /// </summary>
    [DisallowMultipleComponent]
    public class WebViewManager : MonoBehaviour
    {
        private const float TimeoutSeconds = 120f;

        public static WebViewManager Instance { get; private set; }

        private class PendingFlow
        {
            public Action<bool, string> Callback;
            public Coroutine Timeout;
        }

        private readonly Dictionary<string, PendingFlow> _pending = new Dictionary<string, PendingFlow>();

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
        }

        private void OnEnable()
        {
            if (PaymentCallbackListener.Instance != null)
                PaymentCallbackListener.Instance.OnPaymentComplete += HandleCallback;
        }

        private void OnDisable()
        {
            if (PaymentCallbackListener.Instance != null)
                PaymentCallbackListener.Instance.OnPaymentComplete -= HandleCallback;
        }

        /// <summary>Sends the user to <paramref name="url"/> and waits for the gateway's redirect for
        /// <paramref name="orderId"/> to reach <see cref="PaymentCallbackListener"/>. Never returns
        /// synchronously - <paramref name="onComplete"/> fires once, on the main thread, with either the
        /// gateway's result or a TIMEOUT after 120s.</summary>
        public void OpenPaymentUrl(string url, string orderId, Action<bool, string> onComplete)
        {
            if (string.IsNullOrEmpty(orderId)) { onComplete?.Invoke(false, "INVALID_ORDER_ID"); return; }
            if (string.IsNullOrEmpty(url)) { onComplete?.Invoke(false, "INVALID_URL"); return; }

            // Late-bind the subscription in case the listener spawned after this manager did.
            if (PaymentCallbackListener.Instance != null)
            {
                PaymentCallbackListener.Instance.OnPaymentComplete -= HandleCallback; // no duplicate subscription
                PaymentCallbackListener.Instance.OnPaymentComplete += HandleCallback;
                PaymentCallbackListener.Instance.StartListening(); // idempotent if already running
            }
            else
            {
                Debug.LogError("[WebViewManager] No PaymentCallbackListener in the scene - the payment can never confirm. " +
                                "GameServices should have spawned one.");
            }

            var flow = new PendingFlow { Callback = onComplete, Timeout = StartCoroutine(TimeoutRoutine(orderId)) };
            _pending[orderId] = flow;

            Debug.Log($"[WebViewManager] Opening payment page for order {orderId} (external browser).");
            Application.OpenURL(url);
        }

        /// <summary>True if a flow opened via <see cref="OpenPaymentUrl"/> is still waiting on this
        /// order (i.e. the app stayed alive through the whole browser round trip). Phase 3.5:
        /// <see cref="DeepLinkManager"/> checks this to decide whether to resolve through here (so the
        /// UI's own callback fires) or, on a cold start where nothing is waiting in memory, confirm
        /// straight against the wallet's durable Pending row instead.</summary>
        public bool HasPendingFlow(string orderId) => !string.IsNullOrEmpty(orderId) && _pending.ContainsKey(orderId);

        /// <summary>Resolves the pending flow for <paramref name="orderId"/>, if any - same effect
        /// whether the signal came from <see cref="PaymentCallbackListener"/>'s event (Editor/Standalone)
        /// or a direct call from <see cref="DeepLinkManager"/> (Android). A stale/unknown order is a
        /// silent no-op (Timeout already fired, or nothing was ever opened for it).</summary>
        public void ResolvePayment(string orderId, bool success, string txnId, decimal amount)
        {
            if (!_pending.TryGetValue(orderId, out var flow)) return;
            _pending.Remove(orderId);
            if (flow.Timeout != null) StopCoroutine(flow.Timeout);
            flow.Callback?.Invoke(success, txnId);
        }

        private void HandleCallback(string orderId, bool success, string txnId, decimal amount) =>
            ResolvePayment(orderId, success, txnId, amount);

        private IEnumerator TimeoutRoutine(string orderId)
        {
            yield return new WaitForSecondsRealtime(TimeoutSeconds);
            if (_pending.TryGetValue(orderId, out var flow))
            {
                _pending.Remove(orderId);
                Debug.LogWarning($"[WebViewManager] Payment flow for order {orderId} timed out after {TimeoutSeconds:0}s.");
                flow.Callback?.Invoke(false, "TIMEOUT");
            }
        }
    }
}
