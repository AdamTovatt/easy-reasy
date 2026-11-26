namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// The Microsoft name each reason the shared flow gives is recorded under, since audit records keep the names.
    /// </summary>
    [TestClass]
    public class MicrosoftAuthServiceReasonTests
    {
        /// <summary>
        /// Reasons only rules this package never sets can give: every tenant segment resolves, and no expected identity,
        /// acr or amr rule is set; the caller binding is the sign-in endpoint's own check.
        /// </summary>
        private static readonly OpenIdConnectAuthFailureReason[] Unreachable =
        [
            OpenIdConnectAuthFailureReason.UnknownProvider,
            OpenIdConnectAuthFailureReason.IdentityMismatch,
            OpenIdConnectAuthFailureReason.MissingAcr,
            OpenIdConnectAuthFailureReason.AcrNotAccepted,
            OpenIdConnectAuthFailureReason.MissingAmr,
            OpenIdConnectAuthFailureReason.AmrNotAccepted,
            OpenIdConnectAuthFailureReason.CallerBindingMismatch,
        ];

        [TestMethod]
        public void ToMicrosoftReason_EveryReachableReason_KeepsItsNameOrIsTheAuthenticationContextsForARequiredClaim()
        {
            foreach (OpenIdConnectAuthFailureReason reason in Enum.GetValues<OpenIdConnectAuthFailureReason>().Except(Unreachable))
            {
                string expected = reason is OpenIdConnectAuthFailureReason.MissingRequiredClaim or OpenIdConnectAuthFailureReason.RequiredClaimMismatch
                    ? nameof(MicrosoftAuthFailureReason.MissingAuthenticationContext)
                    : reason.ToString();

                Assert.AreEqual(expected, MicrosoftAuthService.ToMicrosoftReason(reason).ToString(), reason.ToString());
            }
        }

        [TestMethod]
        public void ToMicrosoftReason_AReasonNoRuleOfThePackageCanCause_Throws()
        {
            foreach (OpenIdConnectAuthFailureReason reason in Unreachable)
            {
                Assert.ThrowsException<System.Diagnostics.UnreachableException>(() => MicrosoftAuthService.ToMicrosoftReason(reason), reason.ToString());
            }
        }
    }
}
