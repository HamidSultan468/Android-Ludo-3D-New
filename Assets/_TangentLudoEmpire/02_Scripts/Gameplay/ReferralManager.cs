using System.Linq;
using UnityEngine;
using TangentLudoEmpire.Core;
using TangentLudoEmpire.Wallet;
using TangentLudoEmpire.Services;

namespace TangentLudoEmpire.Social
{
    /// <summary>
    /// Task C.3. "On first deposit of friend, give 50 to both" - this device can only ever credit ITS
    /// OWN wallet. When the local player's first successful deposit lands, they get +50 if they'd
    /// applied a friend's code. The REFERRER's +50 needs a backend call (a different device/account) -
    /// TODO (Firebase/backend): POST {referrerCode, newPlayerId} so the referrer's own account gets
    /// credited server-side; not built here, this mock only proves the local half of the loop.
    /// </summary>
    [DisallowMultipleComponent]
    public class ReferralManager : MonoBehaviour
    {
        public const string ReferralFile = "tle_referral_v1.enc";
        public const decimal ReferralBonus = 50m;

        public static ReferralManager Instance { get; private set; }

        private ReferralData _data;
        private bool _bound;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            if (Application.isPlaying) DontDestroyOnLoad(gameObject); // Editor/test code (headless tools) would otherwise hit InvalidOperationException here and abort the rest of Awake()
            EnsureData();
        }

        private void OnEnable() { TryBind(); }
        private void OnDisable() { if (_bound && MoneyWallet.Instance != null) MoneyWallet.Instance.OnTransaction -= HandleTransaction; _bound = false; }
        private void Update() { if (!_bound) TryBind(); } // MoneyWallet may not exist yet the first tick

        private void TryBind()
        {
            if (_bound || MoneyWallet.Instance == null) return;
            MoneyWallet.Instance.OnTransaction += HandleTransaction;
            _bound = true;
        }

        /// <summary>Lazy-init guard rather than trusting Awake() already ran - see AntiFraudManager's
        /// class remarks (Phase 3.3) for why this pattern exists everywhere it touches _data now.</summary>
        private void EnsureData()
        {
            if (_data != null) return;
            _data = SaveService.Instance != null ? SaveService.Instance.LoadEncrypted<ReferralData>(ReferralFile) : null;
            _data ??= new ReferralData();
        }

        /// <summary>Task C.3: "TLE" + Random.Range(1000,9999) - generated once, then persisted.</summary>
        public string GenerateCode()
        {
            EnsureData();
            if (!string.IsNullOrEmpty(_data.MyCode)) return _data.MyCode;
            _data.MyCode = "TLE" + UnityEngine.Random.Range(1000, 9999);
            Persist();
            AuditLog.Log("REFERRAL_CODE_GENERATED", _data.MyCode, "");
            return _data.MyCode;
        }

        public string MyCode { get { EnsureData(); return string.IsNullOrEmpty(_data.MyCode) ? GenerateCode() : _data.MyCode; } }

        /// <summary>Records a friend's code as pending - the bonus itself only pays out on this
        /// player's first successful deposit (see HandleTransaction).</summary>
        public void ApplyReferral(string code)
        {
            EnsureData();
            if (string.IsNullOrWhiteSpace(code)) return;
            if (!string.IsNullOrEmpty(_data.AppliedCode)) { Debug.LogWarning("[ReferralManager] A referral code was already applied - ignoring."); return; }
            _data.AppliedCode = code.Trim().ToUpperInvariant();
            Persist();
            AuditLog.Log("REFERRAL_APPLIED", _data.AppliedCode, "");
        }

        private void HandleTransaction(Transaction tx)
        {
            EnsureData();
            if (tx.Type != TxType.Deposit || tx.Status != TxStatus.Success) return;
            if (string.IsNullOrEmpty(_data.AppliedCode) || _data.BonusGranted) return;

            bool isFirstDeposit = (MoneyWallet.Instance?.GetHistory() ?? System.Array.Empty<Transaction>())
                .Count(t => t.Type == TxType.Deposit && t.Status == TxStatus.Success) == 1;
            if (!isFirstDeposit) return;

            _data.BonusGranted = true;
            Persist();
            MoneyWallet.Instance?.AddFunds(ReferralBonus, "REFERRAL_BONUS");
            AuditLog.Log("REFERRAL_BONUS_GRANTED", _data.AppliedCode, $"{ReferralBonus:0.00} to local player");
            Debug.Log($"[ReferralManager] First-deposit referral bonus paid ({ReferralBonus:0.00}). " +
                      $"TODO(backend): also credit the referrer behind code {_data.AppliedCode}.");
        }

        private void Persist()
        {
            bool ok = SaveService.Instance != null && SaveService.Instance.SaveEncrypted(ReferralFile, _data);
            if (!ok) Debug.LogError("[ReferralManager] SaveEncrypted failed - referral state not persisted!");
        }
    }
}
