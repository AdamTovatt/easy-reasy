using ITfoxtec.Identity.Saml2;
using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// An assertion is refused outside its times. The library checks some of them against the real clock; the package
    /// checks all of them against its own <see cref="TimeProvider"/>, which the tests that move the clock show. Each test
    /// sets every time it relies on, valid on the real clock unless the test is about the library refusing it.
    /// </summary>
    [TestClass]
    public class SamlTimeTests
    {
        [TestMethod]
        public void Validate_ConditionsExpired_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotBefore = now.AddHours(-2);
            response.ConditionsNotOnOrAfter = now.AddHours(-1);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<SecurityTokenExpiredException>(rejection);
        }

        [TestMethod]
        public void Validate_BearerConfirmationExpired_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConfirmationNotOnOrAfter = now.AddHours(-1);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "Assertion has expired");
        }

        [TestMethod]
        public void Validate_ConditionsNotYetValid_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(2));
            response.ConditionsNotBefore = now.AddHours(1);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<SecurityTokenNotYetValidException>(rejection);
        }

        [TestMethod]
        public void Validate_ConditionsExpiredOnPackageClock_IsRefused()
        {
            // Valid on the real clock the library uses, expired on the package's clock.
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(now.AddMinutes(30)));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotOnOrAfter = now.AddMinutes(5);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "The assertion's conditions expired");
        }

        [TestMethod]
        public void Validate_BearerConfirmationExpiredOnPackageClock_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(now.AddMinutes(30)));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConfirmationNotOnOrAfter = now.AddMinutes(5);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "The bearer confirmation expired");
        }

        [TestMethod]
        public void Validate_ConditionsNotYetValidOnPackageClock_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(now.AddMinutes(-30)));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotBefore = now.AddMinutes(-1);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "not valid before");
        }

        [TestMethod]
        public void Validate_NotBeforeWithinClockSkewOnPackageClock_IsAccepted()
        {
            DateTimeOffset now = WholeSecondNow();
            DateTimeOffset notBefore = now.AddMinutes(-1);
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(notBefore - SamlResponseValidator.AllowedClockSkew));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotBefore = notBefore;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual("user-1234", result.NameId);
        }

        [TestMethod]
        public void Validate_ConditionsJustBeforeExpiryPlusClockSkewOnPackageClock_IsAccepted()
        {
            DateTimeOffset now = WholeSecondNow();
            DateTimeOffset notOnOrAfter = now.AddMinutes(10);
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(notOnOrAfter + SamlResponseValidator.AllowedClockSkew - TimeSpan.FromSeconds(1)));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotOnOrAfter = notOnOrAfter;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual("user-1234", result.NameId);
        }

        [TestMethod]
        public void Validate_ConditionsAtExpiryPlusClockSkewOnPackageClock_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            DateTimeOffset notOnOrAfter = now.AddMinutes(10);
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(notOnOrAfter + SamlResponseValidator.AllowedClockSkew));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotOnOrAfter = notOnOrAfter;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "The assertion's conditions expired");
        }

        [TestMethod]
        public void Validate_BearerConfirmationJustBeforeExpiryPlusClockSkewOnPackageClock_IsAccepted()
        {
            DateTimeOffset now = WholeSecondNow();
            DateTimeOffset notOnOrAfter = now.AddMinutes(10);
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(notOnOrAfter + SamlResponseValidator.AllowedClockSkew - TimeSpan.FromSeconds(1)));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConfirmationNotOnOrAfter = notOnOrAfter;

            SamlValidatedAssertion result = harness.Validate(response.ToFormValue());

            Assert.AreEqual("user-1234", result.NameId);
        }

        [TestMethod]
        public void Validate_BearerConfirmationAtExpiryPlusClockSkewOnPackageClock_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            DateTimeOffset notOnOrAfter = now.AddMinutes(10);
            SamlValidationHarness harness = new SamlValidationHarness(timeProvider: new FakeTimeProvider(notOnOrAfter + SamlResponseValidator.AllowedClockSkew));
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConfirmationNotOnOrAfter = notOnOrAfter;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "The bearer confirmation expired");
        }

        [TestMethod]
        public void Validate_ConditionsWithoutNotOnOrAfter_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConditionsNotOnOrAfter = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByLibrary<SecurityTokenNoExpirationException>(rejection);
        }

        [TestMethod]
        public void Validate_BearerConfirmationWithoutNotOnOrAfter_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConfirmationNotOnOrAfter = null;

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            // ITfoxtec reads the missing time as 0001-01-01 and reports it as expired.
            SamlRejectionAssert.ByLibrary<Saml2RequestException>(rejection, "Assertion has expired");
        }

        [TestMethod]
        public void Validate_BearerConfirmationWithNotBefore_IsRefused()
        {
            DateTimeOffset now = WholeSecondNow();
            SamlValidationHarness harness = new SamlValidationHarness();
            FakeSamlResponse response = RespondValidUntil(harness, now, now.AddHours(1));
            response.ConfirmationNotBefore = now.AddMinutes(-1);

            SamlResponseRejectedException rejection = harness.Reject(response.ToFormValue());

            SamlRejectionAssert.ByPackage(rejection, "NotBefore");
        }

        /// <summary>
        /// The current time cut to whole seconds, which is all a SAML time carries, so an edge set from it is exact.
        /// </summary>
        private static DateTimeOffset WholeSecondNow()
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            return now.AddTicks(-(now.Ticks % TimeSpan.TicksPerSecond));
        }

        /// <summary>
        /// A response issued at <paramref name="issuedAt"/> whose conditions and bearer confirmation both run from a
        /// minute before it until <paramref name="validUntil"/>.
        /// </summary>
        private static FakeSamlResponse RespondValidUntil(SamlValidationHarness harness, DateTimeOffset issuedAt, DateTimeOffset validUntil)
        {
            FakeSamlResponse response = harness.Respond();
            response.IssueInstant = issuedAt;
            response.ConditionsNotBefore = issuedAt.AddMinutes(-1);
            response.ConditionsNotOnOrAfter = validUntil;
            response.ConfirmationNotOnOrAfter = validUntil;
            return response;
        }
    }
}
