using System;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Kyc
{
    /// <summary>
    /// Task 1's "KYCManager ScriptableObject" split into two, the same way SecurityConfig/PaymentConfig
    /// (shared config) are kept separate from WalletData/MoneyWallet (per-player data): this class is the
    /// MonoBehaviour singleton that owns and persists <see cref="KycData"/> for the CURRENT player, the
    /// same pattern <see cref="TangentLudoEmpire.Wallet.MoneyWallet"/> uses for <c>WalletData</c>.
    /// </summary>
    [DisallowMultipleComponent]
    [DefaultExecutionOrder(-140)] // same tier as MoneyWallet - after SecurityManager/SaveService
    public class KycManager : MonoBehaviour
    {
        public const string KycFile = "tle_kyc_v1.enc";

        public static KycManager Instance { get; private set; }

        public event Action<bool> OnVerificationChanged;

        private KycData _data;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            EnsureData();
        }

        /// <summary>Lazy-init guard rather than trusting Awake() already ran - a component created via
        /// AddComponent() and used on the very next line is not reliably guaranteed to have finished
        /// Awake() first in every Editor-mode context (confirmed by real NullReferenceExceptions this
        /// exact pattern caught in AntiFraudManager and GameHistoryManager during Phase 3.3/4 testing).</summary>
        private void EnsureData()
        {
            if (_data != null) return;
            _data = SaveService.Instance != null ? SaveService.Instance.LoadEncrypted<KycData>(KycFile) : null;
            if (_data == null)
            {
                _data = KycData.CreateNew();
                Persist();
                Debug.Log("[KycManager] Fresh encrypted KYC record created (unverified).");
            }
            else
            {
                Debug.Log($"[KycManager] Loaded KYC record: verified={_data.IsVerified}.");
            }
        }

        // ---------------------------------------------------------------- reads ----

        public bool IsVerified { get { EnsureData(); return _data.IsVerified; } }
        public string MaskedCnic { get { EnsureData(); return _data.MaskedCnic; } }
        public DateTime? VerifiedAt { get { EnsureData(); return _data.VerifiedAt; } }

        /// <summary>Task 1: the gate <see cref="TangentLudoEmpire.Wallet.MoneyWallet.RequestWithdraw"/>
        /// checks before letting any withdrawal proceed.</summary>
        public bool CanWithdraw() => IsVerified;

        // ---------------------------------------------------------------- writes ----

        /// <summary>Records a verification pass. In production this is called only after a real KYC
        /// check (a backend call to a verification provider) succeeds - never purely on client input;
        /// this method itself doesn't validate the CNIC/phone, it just records the result.</summary>
        public void SetVerified(string cnic, string phoneNumber)
        {
            EnsureData();
            _data.IsVerified = true;
            _data.CNIC = cnic ?? "";
            _data.PhoneNumber = phoneNumber ?? "";
            _data.VerifiedAt = DateTime.UtcNow;
            Persist();
            AuditLog.Log("KYC_VERIFIED", "", $"cnic={_data.MaskedCnic}"); // never the full CNIC
            OnVerificationChanged?.Invoke(true);
            Debug.Log($"[KycManager] Verified (cnic={_data.MaskedCnic}).");
        }

        public void Revoke(string reason)
        {
            EnsureData();
            _data.IsVerified = false;
            Persist();
            AuditLog.Log("KYC_REVOKED", "", reason ?? "");
            OnVerificationChanged?.Invoke(false);
        }

        private void Persist()
        {
            bool ok = SaveService.Instance != null && SaveService.Instance.SaveEncrypted(KycFile, _data);
            if (!ok) Debug.LogError("[KycManager] SaveEncrypted failed - KYC change not persisted!");
        }

#if UNITY_EDITOR
        /// <summary>EDITOR ONLY - compiled out of every player build. Used by Phase3Tools/AdminDashboard.</summary>
        public void Editor_SetVerified(bool verified, string cnic = "35202-0000000-0", string phone = "03000000000")
        {
            if (verified) SetVerified(cnic, phone);
            else Revoke("EDITOR_TEST");
        }
#endif
    }
}
