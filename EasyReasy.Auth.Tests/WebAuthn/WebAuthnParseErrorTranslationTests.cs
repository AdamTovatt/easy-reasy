namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The mapping from what a parser reported to the reason the ceremony reports has to be total. A parse
    /// error added later with no mapping would otherwise reach an application as whichever arm the switch
    /// ended in — a reason naming a check that never ran.
    /// </summary>
    [TestClass]
    public class WebAuthnParseErrorTranslationTests
    {
        [TestMethod]
        public void ToRegistrationReason_EveryParseError_HasAMapping()
        {
            foreach (WebAuthnParseError error in Enum.GetValues<WebAuthnParseError>())
            {
                // Fails by throwing out of the switch's final arm, which is the whole point of it throwing
                // rather than folding an unmapped error into a neighbouring reason.
                WebAuthnParseErrorTranslation.ToRegistrationReason(error);
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
                .Select(WebAuthnParseErrorTranslation.ToRegistrationReason)
                .ToArray();

            Assert.AreEqual(reasons.Length, reasons.Distinct().Count());
        }

        [TestMethod]
        public void ToRegistrationReason_UnmappedParseError_Throws()
        {
            // Proves the final arm is reachable at all: without this, the exhaustiveness test above would
            // pass just as well against a switch whose default silently returned a reason.
            Assert.ThrowsException<InvalidOperationException>(
                () => WebAuthnParseErrorTranslation.ToRegistrationReason((WebAuthnParseError)99));
        }
    }
}
