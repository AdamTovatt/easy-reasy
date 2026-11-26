using System.Reflection;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// A result carries at most one reason, and a failure always one: the factories are the only way to build one.
    /// </summary>
    [TestClass]
    public class MicrosoftAuthCompletionResultTests
    {
        private static readonly MicrosoftUserInfo UserInfo = new MicrosoftUserInfo
        {
            TenantId = FakeMicrosoftIdentityProvider.Tenant,
            ObjectId = FakeMicrosoftIdentityProvider.ObjectId,
            Nonce = "nonce",
            AuthenticationMethods = [],
            AuthenticationContextIds = [],
        };

        [TestMethod]
        public void Constructors_None_IsPublic()
        {
            Assert.AreEqual(0, typeof(MicrosoftAuthCompletionResult).GetConstructors(BindingFlags.Public | BindingFlags.Instance).Length);
        }

        [TestMethod]
        public void Succeeded_Always_CarriesNeitherReason()
        {
            MicrosoftAuthCompletionResult result = MicrosoftAuthCompletionResult.Succeeded(UserInfo, "header.payload.signature", "{}");

            Assert.IsTrue(result.Success);
            Assert.IsNull(result.FlowFailureReason);
            Assert.IsNull(result.MicrosoftFailureReason);
            Assert.IsNull(result.FailureDetail);
            Assert.IsNull(result.FailureCategory);
        }

        [TestMethod]
        [DataRow(OpenIdConnectAuthFailureReason.InvalidState, ExternalAuthFailureReason.InvalidToken)]
        [DataRow(OpenIdConnectAuthFailureReason.CallerBindingMismatch, ExternalAuthFailureReason.InvalidToken)]
        [DataRow(OpenIdConnectAuthFailureReason.InvalidToken, ExternalAuthFailureReason.InvalidToken)]
        [DataRow(OpenIdConnectAuthFailureReason.InvalidIssuer, ExternalAuthFailureReason.InvalidToken)]
        public void FailureCategory_AFlowFailureWithNoAccount_IsInvalidToken(OpenIdConnectAuthFailureReason reason, ExternalAuthFailureReason expected)
        {
            Assert.AreEqual(expected, MicrosoftAuthCompletionResult.Failed(reason).FailureCategory);
        }

        [TestMethod]
        [DataRow(OpenIdConnectAuthFailureReason.MissingAuthTime)]
        [DataRow(OpenIdConnectAuthFailureReason.StaleAuthTime)]
        public void FailureCategory_AFlowRuleRefusingAGenuineAccount_IsRejected(OpenIdConnectAuthFailureReason reason)
        {
            Assert.AreEqual(ExternalAuthFailureReason.Rejected, MicrosoftAuthCompletionResult.Refused(reason, UserInfo).FailureCategory);
        }

        [TestMethod]
        public void FailureCategory_EveryEntraReason_IsRejected()
        {
            foreach (MicrosoftAuthFailureReason reason in Enum.GetValues<MicrosoftAuthFailureReason>())
            {
                Assert.AreEqual(ExternalAuthFailureReason.Rejected, MicrosoftAuthCompletionResult.Refused(reason, UserInfo).FailureCategory, reason.ToString());
            }
        }

        [TestMethod]
        public void Failed_EveryFlowReason_CarriesOnlyThatReasonAndNoAccount()
        {
            foreach (OpenIdConnectAuthFailureReason reason in Enum.GetValues<OpenIdConnectAuthFailureReason>())
            {
                MicrosoftAuthCompletionResult result = MicrosoftAuthCompletionResult.Failed(reason);

                Assert.IsFalse(result.Success, reason.ToString());
                Assert.AreEqual(reason, result.FlowFailureReason);
                Assert.IsNull(result.MicrosoftFailureReason, reason.ToString());
                Assert.AreEqual(reason.ToString(), result.FailureDetail);
                Assert.IsNull(result.AttemptedSubject, reason.ToString());
            }
        }

        [TestMethod]
        public void Refused_EveryFlowReason_CarriesOnlyThatReasonAndTheAccount()
        {
            foreach (OpenIdConnectAuthFailureReason reason in Enum.GetValues<OpenIdConnectAuthFailureReason>())
            {
                MicrosoftAuthCompletionResult result = MicrosoftAuthCompletionResult.Refused(reason, UserInfo);

                Assert.IsFalse(result.Success, reason.ToString());
                Assert.AreEqual(reason, result.FlowFailureReason);
                Assert.IsNull(result.MicrosoftFailureReason, reason.ToString());
                Assert.AreEqual(reason.ToString(), result.FailureDetail);
                Assert.AreEqual($"{FakeMicrosoftIdentityProvider.TenantId}:{FakeMicrosoftIdentityProvider.ObjectId}", result.AttemptedSubject, reason.ToString());
                Assert.IsNull(result.UserInfo, reason.ToString());
            }
        }

        [TestMethod]
        public void Refused_EveryEntraReason_CarriesOnlyThatReasonAndTheAccount()
        {
            foreach (MicrosoftAuthFailureReason reason in Enum.GetValues<MicrosoftAuthFailureReason>())
            {
                MicrosoftAuthCompletionResult result = MicrosoftAuthCompletionResult.Refused(reason, UserInfo);

                Assert.IsFalse(result.Success, reason.ToString());
                Assert.AreEqual(reason, result.MicrosoftFailureReason);
                Assert.IsNull(result.FlowFailureReason, reason.ToString());
                Assert.AreEqual(reason.ToString(), result.FailureDetail);
                Assert.AreEqual($"{FakeMicrosoftIdentityProvider.TenantId}:{FakeMicrosoftIdentityProvider.ObjectId}", result.AttemptedSubject, reason.ToString());
                Assert.IsNull(result.UserInfo, reason.ToString());
            }
        }

        [TestMethod]
        public void FailureDetail_EveryNameVersion1Audited_IsStillAReasonName()
        {
            // Audit records written by EasyReasy.Auth.Microsoft 1.x carry these names; each must keep its meaning.
            string[] version1Names =
            [
                "InvalidState", "ExpiredState", "BindingCookieMissing", "BindingCookieMismatch", "CodeRedemptionFailed",
                "InvalidToken", "InvalidSignature", "InvalidAudience", "InvalidIssuer", "TokenExpired", "NonceMismatch",
                "PersonalAccount", "TenantMismatch", "TenantNotAllowed", "MissingAuthenticationContext", "MissingAuthTime",
                "StaleAuthTime", "CallerBindingMismatch",
            ];
            string[] names = Enum.GetNames<MicrosoftAuthFailureReason>().Concat(Enum.GetNames<OpenIdConnectAuthFailureReason>()).ToArray();

            CollectionAssert.IsSubsetOf(version1Names, names);
        }

        [TestMethod]
        public void MicrosoftAuthFailureReason_EveryMember_SharesNoNameWithAFlowReason()
        {
            // The audit record keeps only the name, so a shared name would make two checks indistinguishable.
            string[] shared = Enum.GetNames<MicrosoftAuthFailureReason>().Intersect(Enum.GetNames<OpenIdConnectAuthFailureReason>()).ToArray();

            Assert.AreEqual(0, shared.Length, string.Join(", ", shared));
        }
    }
}
