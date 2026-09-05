using System;

namespace TangentLudoEmpire.Payments
{
    /// <summary>Sandbox mock disbursement (Task 3). See <see cref="JazzCashPayoutMock"/>'s remarks - same
    /// story for Easypaisa's real (separate, not built here) disbursement product.</summary>
    public class EasypaisaPayoutMock : IPayoutGateway
    {
        public string GatewayName => "Easypaisa";

        private static int _counter = 100;

        public void Payout(decimal amount, string account, Action<bool, string> callback)
        {
            if (amount <= 0m) { callback?.Invoke(false, "INVALID_AMOUNT"); return; }
            if (string.IsNullOrWhiteSpace(account)) { callback?.Invoke(false, "INVALID_ACCOUNT"); return; }
            PaymentRunner.Instance.Run(PaymentRunner.SimulateResult(
                delaySeconds: 5f, successRate: 0.95f,
                okRef: "PAYOUT_EP_" + NextId(), failCode: "EASYPAISA_PAYOUT_FAILED", callback));
        }

        private static int NextId() => System.Threading.Interlocked.Increment(ref _counter);
    }
}
