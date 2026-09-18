namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The mappings from what a parser or a shared check reported to the reason the ceremony reports have
    /// to be total. A member added later with no mapping would otherwise reach an application as whichever
    /// arm the switch ended in — a reason naming a check that never ran.
    /// </summary>
    [TestClass]
    public class WebAuthnFailureTranslationTests
    {
        [TestMethod]
        public void ToRegistrationReason_EveryParseError_HasAMapping()
        {
            foreach (WebAuthnParseError error in Enum.GetValues<WebAuthnParseError>())
            {
                // Fails by throwing out of the switch's final arm, which is the whole point of it throwing
                // rather than folding an unmapped error into a neighbouring reason.
                WebAuthnFailureTranslation.ToRegistrationReason(error);
            }
        }

        [TestMethod]
        public void ToRegistrationReason_DistinctParseErrors_StayDistinct()
        {
            // Not an invariant — an observation about the errors that exist now. Every one of them today
            // describes a failure an audit log would want told apart from the others, and collapsing two
            // onto one reason would pass the totality test above while losing that. If a parse error is
            // added that genuinely shares a reason with an existing one, relax this assertion; do not
            // invent a distinct reason to keep it green. Totality is the property that must hold forever.
            WebAuthnRegistrationFailureReason[] reasons = Enum.GetValues<WebAuthnParseError>()
                .Select(WebAuthnFailureTranslation.ToRegistrationReason)
                .ToArray();

            Assert.AreEqual(reasons.Length, reasons.Distinct().Count());
        }

        [TestMethod]
        public void ToAuthenticationReason_EveryParseError_HasAMapping()
        {
            foreach (WebAuthnParseError error in Enum.GetValues<WebAuthnParseError>())
            {
                WebAuthnFailureTranslation.ToAuthenticationReason(error);
            }
        }

        [TestMethod]
        public void ToAuthenticationReason_TheStoredCredentialErrors_ShareOneReason()
        {
            // Not an oversight and not a case to split: on this path the key came out of the application's
            // own store, so "will not parse" and "states an algorithm we do not verify" are one fact about
            // the stored value. Totality is what must hold; distinctness is only ever an observation.
            Assert.AreEqual(
                WebAuthnFailureTranslation.ToAuthenticationReason(WebAuthnParseError.MalformedPublicKey),
                WebAuthnFailureTranslation.ToAuthenticationReason(WebAuthnParseError.UnsupportedAlgorithm));
        }

        [TestMethod]
        public void ToRegistrationReason_EveryCeremonyCheck_HasAMapping()
        {
            foreach (WebAuthnCeremonyCheck check in Enum.GetValues<WebAuthnCeremonyCheck>())
            {
                WebAuthnFailureTranslation.ToRegistrationReason(check);
            }
        }

        [TestMethod]
        public void ToAuthenticationReason_EveryCeremonyCheck_HasAMapping()
        {
            foreach (WebAuthnCeremonyCheck check in Enum.GetValues<WebAuthnCeremonyCheck>())
            {
                WebAuthnFailureTranslation.ToAuthenticationReason(check);
            }
        }

        [TestMethod]
        public void ToRegistrationReason_DistinctCeremonyChecks_StayDistinct()
        {
            // The shared checks are the ones both ceremonies make, and each names a different thing that
            // went wrong; two of them landing on one reason would mean an audit log could not say which
            // check rejected a ceremony. Relaxable on the same terms as the parse-error map above.
            WebAuthnRegistrationFailureReason[] reasons = Enum.GetValues<WebAuthnCeremonyCheck>()
                .Select(WebAuthnFailureTranslation.ToRegistrationReason)
                .ToArray();

            Assert.AreEqual(reasons.Length, reasons.Distinct().Count());
        }

        [TestMethod]
        public void ToAuthenticationReason_DistinctCeremonyChecks_StayDistinct()
        {
            WebAuthnAuthenticationFailureReason[] reasons = Enum.GetValues<WebAuthnCeremonyCheck>()
                .Select(WebAuthnFailureTranslation.ToAuthenticationReason)
                .ToArray();

            Assert.AreEqual(reasons.Length, reasons.Distinct().Count());
        }

        [TestMethod]
        public void ToAuthenticationReason_UnmappedParseError_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => WebAuthnFailureTranslation.ToAuthenticationReason((WebAuthnParseError)99));
        }

        [TestMethod]
        public void ToRegistrationReason_UnmappedParseError_Throws()
        {
            // Proves the final arm is reachable at all: without this, the exhaustiveness test above would
            // pass just as well against a switch whose default silently returned a reason.
            Assert.ThrowsException<InvalidOperationException>(
                () => WebAuthnFailureTranslation.ToRegistrationReason((WebAuthnParseError)99));
        }

        [TestMethod]
        public void ToRegistrationReason_UnmappedCeremonyCheck_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => WebAuthnFailureTranslation.ToRegistrationReason((WebAuthnCeremonyCheck)99));
        }

        [TestMethod]
        public void ToAuthenticationReason_UnmappedCeremonyCheck_Throws()
        {
            Assert.ThrowsException<InvalidOperationException>(
                () => WebAuthnFailureTranslation.ToAuthenticationReason((WebAuthnCeremonyCheck)99));
        }
    }
}
