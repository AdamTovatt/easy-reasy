namespace EasyReasy.Auth.Microsoft.Tests
{
    [TestClass]
    public class MicrosoftSignInServiceTests
    {
        [TestMethod]
        public void GetCategory_EveryReason_IsInvalidTokenForAFailedFlowOrTokenAndRejectedForARefusedAccount()
        {
            MicrosoftAuthFailureReason[] rejected =
            [
                MicrosoftAuthFailureReason.PersonalAccount,
                MicrosoftAuthFailureReason.TenantMismatch,
                MicrosoftAuthFailureReason.TenantNotAllowed,
                MicrosoftAuthFailureReason.MissingAuthenticationContext,
                MicrosoftAuthFailureReason.MissingAuthTime,
                MicrosoftAuthFailureReason.StaleAuthTime,
            ];

            foreach (MicrosoftAuthFailureReason reason in Enum.GetValues<MicrosoftAuthFailureReason>())
            {
                ExternalAuthFailureReason expected = rejected.Contains(reason) ? ExternalAuthFailureReason.Rejected : ExternalAuthFailureReason.InvalidToken;
                Assert.AreEqual(expected, MicrosoftSignInService.GetCategory(reason), reason.ToString());
            }
        }
    }
}
