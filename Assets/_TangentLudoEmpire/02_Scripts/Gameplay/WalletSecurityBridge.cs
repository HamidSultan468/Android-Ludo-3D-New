using UnityEngine;
using UnityEngine.UI;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Wallet.UI;
using TangentLudoEmpire.Services;
using TangentLudoEmpire.Core;

namespace TangentLudoEmpire.Security
{
    /// <summary>
    /// Task A.3. Static class per spec. Wires <see cref="TangentLudoEmpire.Bridge.WalletGameBridge.OnSuspiciousActivity"/>
    /// (fired by <see cref="GameplayAntiCheatManager"/>) to actually freezing the wallet and telling the
    /// player, so a human doesn't have to remember to call FreezeAccount by hand from every cheat call site.
    /// </summary>
    public static class WalletSecurityBridge
    {
        private static GameObject _popupRoot;
        private static Text _reasonLabel;
        private static bool _subscribed;

        /// <summary>Idempotent - safe to call from RuntimeInitializeOnLoadMethod or a manager's Awake.
        /// Subscribes exactly once even across domain-reload-free repeated calls (Editor test harness).</summary>
        public static void EnsureSubscribed()
        {
            if (_subscribed) return;
            _subscribed = true;
            TangentLudoEmpire.Bridge.WalletGameBridge.OnSuspiciousActivity += (playerId, reason) => FreezeAccount(playerId, reason);
        }

        /// <summary>Freezes the wallet (<see cref="MoneyWallet.SetFrozen"/>) and shows the
        /// "Account Under Review" popup. Safe to call before MoneyWallet exists (audits and returns).</summary>
        public static void FreezeAccount(string playerId, string reason)
        {
            var wallet = MoneyWallet.Instance;
            if (wallet == null)
            {
                AuditLog.Log("FREEZE_ACCOUNT_NO_WALLET", playerId ?? "", reason ?? "");
                Debug.LogWarning("[WalletSecurityBridge] FreezeAccount called before MoneyWallet exists.");
                return;
            }

            wallet.SetFrozen(true, $"{playerId}:{reason}");
            AuditLog.Log("ACCOUNT_FROZEN", playerId ?? "", reason ?? "");
            ShowUnderReviewPopup(reason);
        }

        /// <summary>Admin/support unfreeze path (not spec'd explicitly, but FreezeAccount with no inverse
        /// would be a one-way trapdoor with no recovery - kept minimal, RBAC-gated).</summary>
        public static bool UnfreezeAccount(string playerId, string reason)
        {
            if (!RBAC.Demand(Role.Staff, "UNFREEZE_ACCOUNT")) return false;
            var wallet = MoneyWallet.Instance;
            if (wallet == null) return false;
            wallet.SetFrozen(false, $"{playerId}:{reason}");
            AuditLog.Log("ACCOUNT_UNFROZEN", playerId ?? "", reason ?? "");
            HidePopup();
            return true;
        }

        // ---------------------------------------------------------------- popup (self-building, WalletUiKit) ----

        private static void ShowUnderReviewPopup(string reason)
        {
            if (_popupRoot == null) BuildPopup();
            _reasonLabel.text = string.IsNullOrEmpty(reason) ? "" : $"Reason: {reason}";
            _popupRoot.SetActive(true);
        }

        private static void HidePopup()
        {
            if (_popupRoot != null) _popupRoot.SetActive(false);
        }

        private static void BuildPopup()
        {
            var canvas = WalletUiKit.EnsureCanvas("WalletCanvas");
            _popupRoot = WalletUiKit.Container(canvas.transform, "AccountUnderReviewPopup", new Vector2(640, 340), Vector2.zero, WalletUiKit.Panel);
            WalletUiKit.Label(_popupRoot.transform, "Account Under Review", 32, new Vector2(600, 50), new Vector2(0, 120));
            WalletUiKit.Label(_popupRoot.transform,
                "Unusual activity was detected on this account. Spending is temporarily paused while our team reviews it.",
                22, new Vector2(580, 100), new Vector2(0, 30), TextAnchor.UpperCenter);
            _reasonLabel = WalletUiKit.Label(_popupRoot.transform, "", 18, new Vector2(580, 40), new Vector2(0, -40));
            WalletUiKit.Btn(_popupRoot.transform, "OK", new Vector2(200, 70), new Vector2(0, -130), () => _popupRoot.SetActive(false));
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build.</summary>
        public static bool Editor_IsPopupVisible => _popupRoot != null && _popupRoot.activeSelf;
#endif
    }
}
