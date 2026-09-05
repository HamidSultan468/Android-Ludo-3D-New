using System;

namespace TangentLudoEmpire.Social
{
    /// <summary>Not explicitly asked for a persisted file by Task C.3, but a referral code that changed
    /// every launch (or forgot which friend's code you applied) would be useless - added the same way
    /// KYC/fraud tracking got their own small encrypted files. tle_referral_v1.enc.</summary>
    [Serializable]
    internal class ReferralData
    {
        public string MyCode = "";
        public string AppliedCode = "";     // a friend's code THIS player entered
        public bool BonusGranted;           // true once the first-deposit bonus has paid out (never twice)
    }
}
