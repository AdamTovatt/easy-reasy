namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// A response must answer the request this sign-in sent, in the response and in the bearer confirmation. That is
    /// what stops a captured response from being replayed into a later sign-in, and an unsolicited one from being
    /// accepted at all; the request itself being consumed once is the caller's.
    /// </summary>
    [TestClass]
    public class SamlRequestCorrelationTests
    {
        [TestMethod]
        public void Validate_CapturedResponseReplayedIntoLaterSignIn_IsRefused()
        {
            SamlValidationHarness earlierSignIn = new SamlValidationHarness();
            string captured = earlierSignIn.Respond().ToFormValue();
            earlierSignIn.Validate(captured);
            SamlValidationHarness laterSignIn = new SamlValidationHarness();

            SamlResponseRejectedException rejection = laterSignIn.Reject(captured);

            SamlRejectionAssert.ByPackage(rejection, "response's InResponseTo");
        }

        [TestMethod]
        public void Validate_UnsolicitedResponse_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.InResponseTo = null;
            response.ConfirmationInResponseTo = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "response's InResponseTo");
        }

        [TestMethod]
        public void Validate_ConfirmationAnswersAnotherRequest_IsRefused()
        {
            // The response's InResponseTo is unsigned when only the assertion is signed, so the signed confirmation
            // must name the request too.
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.ConfirmationInResponseTo = "_another-request";

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "bearer confirmation's InResponseTo");
        }

        [TestMethod]
        public void Validate_ConfirmationWithoutInResponseTo_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.ConfirmationInResponseTo = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "bearer confirmation's InResponseTo");
        }

        [TestMethod]
        public void Validate_FailureStatus_IsRefused()
        {
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = harness.Respond();
            response.StatusCode = "urn:oasis:names:tc:SAML:2.0:status:Responder";

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "status is Responder");
        }
    }
}
