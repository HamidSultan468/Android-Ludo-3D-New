using System;

namespace TangentLudoEmpire.Kyc
{
    /// <summary>
    /// PER-PLAYER identity-verification record. Task 1 asked for this as a ScriptableObject named
    /// "KYCManager" - a ScriptableObject asset is one shared thing baked into the project (right for
    /// <see cref="TangentLudoEmpire.Payments.PaymentConfig"/>/<see cref="TangentLudoEmpire.Core.SecurityConfig"/>,
    /// app-wide config), not per-user data like a CNIC/phone/verification status, which needs to be a
    /// different value for every player - see <see cref="KycManager"/>'s remarks for the full split.
    ///
    /// Persisted the same way <see cref="TangentLudoEmpire.Wallet.WalletData"/> is: AES-256 + SHA-256 via
    /// <see cref="TangentLudoEmpire.Core.SaveService.SaveEncrypted{T}"/> at <c>tle_kyc_v1.enc</c>.
    /// </summary>
    [Serializable]
    public class KycData
    {
        public bool IsVerified;
        public string CNIC = "";
        public string PhoneNumber = "";
        public string VerifiedAtIso = ""; // DateTime as round-trip ("o") string - JsonUtility has no native DateTime support

        public DateTime? VerifiedAt
        {
            get => DateTime.TryParse(VerifiedAtIso, null, System.Globalization.DateTimeStyles.RoundtripKind, out var d) ? d : (DateTime?)null;
            set => VerifiedAtIso = value.HasValue ? value.Value.ToUniversalTime().ToString("o") : "";
        }

        public static KycData CreateNew() => new KycData { IsVerified = false };

        /// <summary>Never print a full CNIC to a log - it's a national ID number, treat it like any other
        /// PII. Shows only the last 4 digits (the common "look, this is probably the same person" check).</summary>
        public string MaskedCnic => string.IsNullOrEmpty(CNIC) ? "" : (CNIC.Length <= 4 ? "****" : new string('*', CNIC.Length - 4) + CNIC.Substring(CNIC.Length - 4));
    }
}
