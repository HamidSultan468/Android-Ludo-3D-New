using System.Collections.Generic;
using UnityEngine;

namespace TangentLudoEmpire.Payments
{
    /// <summary>Cash-out counterpart to <see cref="PaymentGatewayFactory"/>, reusing the same
    /// <see cref="PaymentProvider"/> enum (a provider is a provider, whichever direction the money moves).</summary>
    public static class PayoutGatewayFactory
    {
        private static readonly Dictionary<PaymentProvider, IPayoutGateway> _cache = new Dictionary<PaymentProvider, IPayoutGateway>();

        public static IPayoutGateway GetGateway(PaymentProvider provider)
        {
            if (_cache.TryGetValue(provider, out var g) && g != null) return g;
            g = Create(provider);
            _cache[provider] = g;
            return g;
        }

        private static IPayoutGateway Create(PaymentProvider provider)
        {
            switch (provider)
            {
                case PaymentProvider.JazzCash:  return new JazzCashPayoutMock();
                case PaymentProvider.Easypaisa: return new EasypaisaPayoutMock();
                default:
                    Debug.LogWarning($"[PayoutGatewayFactory] No payout gateway for {provider} - falling back to JazzCash mock.");
                    return new JazzCashPayoutMock();
            }
        }

        public static void InvalidateCache() => _cache.Clear();
    }
}
