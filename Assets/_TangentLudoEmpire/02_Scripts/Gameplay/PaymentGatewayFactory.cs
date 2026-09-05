using System.Collections.Generic;
using UnityEngine;

namespace TangentLudoEmpire.Payments
{
    /// <summary>Known cash providers.</summary>
    public enum PaymentProvider { Mock, JazzCash, Easypaisa, Card }

    /// <summary>
    /// Single place that decides which <see cref="IPaymentGateway"/> the app uses. Everything else
    /// depends on the interface, never on a concrete gateway.
    ///
    ///  * <see cref="Current"/> - the one "default" gateway for call sites that haven't picked a
    ///    provider (kept for the Phase 3 <c>WithdrawPopup</c>; defaults to <see cref="PaymentProvider.Mock"/>
    ///    since JazzCash/Easypaisa don't support withdrawals).
    ///  * <see cref="GetGateway"/> - the Phase 3.1 API: an explicit provider (JazzCash/Easypaisa,
    ///    picked from <c>DepositPopup</c>'s dropdown), cached per provider, built from the shared
    ///    <see cref="PaymentConfig"/> asset.
    /// </summary>
    public static class PaymentGatewayFactory
    {
        private static IPaymentGateway _overrideCurrent;
        private static readonly Dictionary<PaymentProvider, IPaymentGateway> _cache = new Dictionary<PaymentProvider, IPaymentGateway>();

        /// <summary>The provider <see cref="Current"/> resolves to when nothing overrides it.</summary>
        public static PaymentProvider Active { get; private set; } = PaymentProvider.Mock;

        public static IPaymentGateway Current => _overrideCurrent ?? GetGateway(Active);

        /// <summary>Returns the (cached) gateway for a specific provider, injecting <see cref="PaymentConfig"/>.</summary>
        public static IPaymentGateway GetGateway(PaymentProvider provider)
        {
            if (_cache.TryGetValue(provider, out var g) && g != null) return g;
            g = Create(provider);
            _cache[provider] = g;
            return g;
        }

        private static IPaymentGateway Create(PaymentProvider provider)
        {
            var cfg = PaymentConfig.Load();
            switch (provider)
            {
                case PaymentProvider.JazzCash:  return new JazzCashGateway(cfg);
                case PaymentProvider.Easypaisa: return new EasypaisaGateway(cfg);
                case PaymentProvider.Card:
                    Debug.LogWarning("[PaymentGatewayFactory] Card gateway not implemented yet - falling back to Mock.");
                    return new MockPaymentGateway();
                case PaymentProvider.Mock:
                default:
                    return new MockPaymentGateway();
            }
        }

        /// <summary>Test/DI hook - override <see cref="Current"/> with a specific instance (e.g. a
        /// deterministic fake in a unit test). Does not affect <see cref="GetGateway"/> lookups.</summary>
        public static void Override(IPaymentGateway gateway) => _overrideCurrent = gateway;

        /// <summary>Clears any cached gateway instances so the next call rebuilds from the latest
        /// <see cref="PaymentConfig"/> - useful after editing the config asset mid-session.</summary>
        public static void InvalidateCache() => _cache.Clear();
    }
}
