namespace EasyReasy.Auth.Saml.Tests
{
    [TestClass]
    public class SamlAuthFailureReasonExtensionsTests
    {
        [TestMethod]
        public void ToExternalAuthFailureReason_EveryReason_IsInvalidTokenForAFailedFlowOrResponseAndRejectedForARefusedAccount()
        {
            SamlAuthFailureReason[] rejected =
            [
                SamlAuthFailureReason.MissingIdentity,
                SamlAuthFailureReason.IdentityMismatch,
                SamlAuthFailureReason.StaleAuthentication,
                SamlAuthFailureReason.MissingAuthnContextClass,
                SamlAuthFailureReason.AuthnContextClassNotAccepted,
            ];

            foreach (SamlAuthFailureReason reason in Enum.GetValues<SamlAuthFailureReason>())
            {
                ExternalAuthFailureReason expected = rejected.Contains(reason) ? ExternalAuthFailureReason.Rejected : ExternalAuthFailureReason.InvalidToken;
                Assert.AreEqual(expected, reason.ToExternalAuthFailureReason(), reason.ToString());
            }
        }

        [TestMethod]
        public void ToExternalAuthFailureReason_ValueThatIsNoMember_Throws()
        {
            Assert.ThrowsException<ArgumentOutOfRangeException>(() => ((SamlAuthFailureReason)999).ToExternalAuthFailureReason());
        }
    }
}
