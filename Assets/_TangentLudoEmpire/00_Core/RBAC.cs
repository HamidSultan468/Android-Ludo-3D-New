using UnityEngine;

namespace TangentLudoEmpire.Core
{
    /// <summary>Server-assigned account role. Ordinal order = privilege level (CEO is highest).</summary>
    public enum Role
    {
        Player = 0,
        Pro = 1,
        Staff = 2,
        Manager = 3,
        CEO = 4
    }

    // SecurityConfig (the ScriptableObject) lives in its own file, SecurityConfig.cs - a ScriptableObject
    // class must match its filename to get a MonoScript / a usable .asset.

    /// <summary>Role-based access checks. Current role comes from the server-issued profile
    /// (<see cref="TangentLudoEmpire.Services.BackendService"/>); it is never read from client-writable storage.</summary>
    public static class RBAC
    {
        /// <summary>The caller's current role. Defaults to <see cref="Role.Player"/> until the backend
        /// says otherwise.</summary>
        public static Role CurrentRole
        {
            get
            {
                var svc = TangentLudoEmpire.Services.BackendService.Instance;
                return svc != null ? svc.CurrentRole : Role.Player;
            }
        }

        /// <summary>True if the current role is at least <paramref name="required"/>.</summary>
        public static bool HasPermission(Role required) => (int)CurrentRole >= (int)required;

        /// <summary>Phase 3 wallet gate: may the current user spend from their real-money wallet?
        /// Every real player can, UNLESS anti-cheat has locked the wallet (server-confirmed drift) or
        /// flagged the account. Role does not grant extra spend rights - it only ever restricts.</summary>
        public static bool CanSpend()
        {
            var ac = TangentLudoEmpire.Services.AntiCheatManager.Instance;
            if (ac != null && (ac.WalletLocked || ac.Flagged))
            {
                TangentLudoEmpire.Services.AuditLog.Log("SPEND_DENY", before: $"role={CurrentRole}",
                    after: $"walletLocked={ac.WalletLocked} flagged={ac.Flagged}");
                return false;
            }

            // Phase 5.1: gameplay anti-cheat freeze (GameplayAntiCheatManager -> WalletSecurityBridge.FreezeAccount)
            // is a distinct gate from the payment-side AntiCheatManager above - either one alone is enough to deny.
            var wallet = TangentLudoEmpire.Wallet.MoneyWallet.Instance;
            if (wallet != null && wallet.IsFrozen)
            {
                TangentLudoEmpire.Services.AuditLog.Log("SPEND_DENY", before: $"role={CurrentRole}", after: "walletFrozen=true");
                return false;
            }
            return true;
        }

        /// <summary>HasPermission with an audit trail for denied privileged actions.</summary>
        public static bool Demand(Role required, string action)
        {
            bool ok = HasPermission(required);
            if (!ok)
                TangentLudoEmpire.Services.AuditLog.Log("RBAC_DENY",
                    before: $"role={CurrentRole}", after: $"needed={required} for {action}");
            return ok;
        }
    }
}
