using System;

namespace TangentLudoEmpire.Payments
{
    /// <summary>Sandbox mock disbursement (Task 3). Real JazzCash payouts need the separate
    /// Disbursement/IBFT API (server-to-server, merchant-authenticated) - not built here, this is a
    /// timed coin-flip standing in for it so the withdraw pipeline is testable end to end.</summary>
    public class JazzCashPayoutMock : IPayoutGateway
    {
        public string GatewayName => "JazzCash";

        private static int _counter = 100;

        public void Payout(decimal amount, string account, Action<bool, string> callback)
        {
            if (amount <= 0m) { callback?.Invoke(false, "INVALID_AMOUNT"); return; }
            if (string.IsNullOrWhiteSpace(account)) { callback?.Invoke(false, "INVALID_ACCOUNT"); return; }
            PaymentRunner.Instance.Run(PaymentRunner.SimulateResult(
                delaySeconds: 5f, successRate: 0.95f,
                okRef: "PAYOUT_JC_" + NextId(), failCode: "JAZZCASH_PAYOUT_FAILED", callback));
        }

        private static int NextId() => System.Threading.Interlocked.Increment(ref _counter);
    }
}
