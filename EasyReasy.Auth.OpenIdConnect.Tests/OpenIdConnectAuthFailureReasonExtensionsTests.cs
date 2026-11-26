namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    [TestClass]
    public class OpenIdConnectAuthFailureReasonExtensionsTests
    {
        [TestMethod]
        public void ToExternalAuthFailureReason_EveryReason_IsInvalidTokenForAFailedFlowOrTokenAndRejectedForARefusedAccount()
        {
            OpenIdConnectAuthFailureReason[] rejected =
            [
                OpenIdConnectAuthFailureReason.IdentityMismatch,
                OpenIdConnectAuthFailureReason.MissingAuthTime,
                OpenIdConnectAuthFailureReason.StaleAuthTime,
                OpenIdConnectAuthFailureReason.MissingAcr,
                OpenIdConnectAuthFailureReason.AcrNotAccepted,
                OpenIdConnectAuthFailureReason.MissingAmr,
                OpenIdConnectAuthFailureReason.AmrNotAccepted,
                OpenIdConnectAuthFailureReason.MissingRequiredClaim,
                OpenIdConnectAuthFailureReason.RequiredClaimMismatch,
            ];

            foreach (OpenIdConnectAuthFailureReason reason in Enum.GetValues<OpenIdConnectAuthFailureReason>())
            {
                ExternalAuthFailureReason expected = rejected.Contains(reason) ? ExternalAuthFailureReason.Rejected : ExternalAuthFailureReason.InvalidToken;
                Assert.AreEqual(expected, reason.ToExternalAuthFailureReason(), reason.ToString());
            }
        }
    }
}
