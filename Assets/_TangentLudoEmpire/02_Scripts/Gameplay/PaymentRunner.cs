using System;
using System.Collections;
using UnityEngine;

namespace TangentLudoEmpire.Payments
{
    /// <summary>
    /// Hidden helper so a plain (non-MonoBehaviour) gateway can still use coroutines for its simulated
    /// latency and always deliver the callback on the main thread. Auto-created on first use.
    /// </summary>
    [DisallowMultipleComponent]
    public class PaymentRunner : MonoBehaviour
    {
        private static PaymentRunner _instance;

        public static PaymentRunner Instance
        {
            get
            {
                if (_instance != null) return _instance;
                _instance = FindAnyObjectByType<PaymentRunner>();
                if (_instance == null)
                {
                    var go = new GameObject("PaymentRunner") { hideFlags = HideFlags.HideAndDontSave };
                    _instance = go.AddComponent<PaymentRunner>();
                    DontDestroyOnLoad(go);
                }
                return _instance;
            }
        }

        public Coroutine Run(IEnumerator routine) => StartCoroutine(routine);

        /// <summary>Shared "sandbox round trip" shape for every mock/sandbox gateway: wait
        /// <paramref name="delaySeconds"/>, then succeed with probability <paramref name="successRate"/>.
        /// Only Play mode ticks this (coroutines don't run in the Editor while not playing) - callers
        /// that also need an Edit-mode path handle that separately.</summary>
        public static IEnumerator SimulateResult(float delaySeconds, float successRate, string okRef,
                                                 string failCode, Action<bool, string> callback)
        {
            yield return new WaitForSecondsRealtime(delaySeconds);
            bool ok = UnityEngine.Random.value < successRate;
            callback?.Invoke(ok, ok ? okRef : failCode);
        }
    }
}
