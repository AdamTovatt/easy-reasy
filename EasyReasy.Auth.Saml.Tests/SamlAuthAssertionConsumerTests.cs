using System.Net;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The assertion consumer service between Begin and completion: a response that answers a pending request once is
    /// checked against the identity provider and the begin request's rules, and its outcome reaches the browser that began
    /// the sign-in.
    /// </summary>
    [TestClass]
    public class SamlAuthAssertionConsumerTests
    {
        private const string MfaClass = "urn:example:mfa";
        private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5);

        [TestMethod]
        public async Task Complete_GenuineResponse_ReturnsTheVerifiedUser()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request => request.CallerBinding = "link:user-7"));
            DateTimeOffset authenticatedAt = TruncateToSeconds(DateTimeOffset.UtcNow.AddMinutes(-1));

            string code = await host.AnswerAsync(begun!.RedirectUrl, response =>
            {
                response.AuthenticationInstant = authenticatedAt;
                response.AuthnContextClassRef = MfaClass;
                response.Attributes["email"] = ["person@example.test"];
                response.Attributes["groups"] = ["staff", "admins"];
            });
            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, code, "link:user-7");

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(SamlFlowHost.DefaultIdentity, result.UserInfo.Identity);
            Assert.AreEqual(authenticatedAt, result.UserInfo.AuthenticationInstant);
            Assert.AreEqual(MfaClass, result.UserInfo.AuthnContextClassRef);
            CollectionAssert.AreEqual(new[] { "person@example.test" }, result.UserInfo.Attributes["email"].ToArray());
            CollectionAssert.AreEqual(new[] { "staff", "admins" }, result.UserInfo.Attributes["groups"].ToArray());
            Assert.AreEqual(result.UserInfo.Identity, result.AttemptedIdentity);
        }

        [TestMethod]
        public async Task AssertionConsumer_GenuineResponse_RedirectsToTheCompletionPathWithACode()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(new BrowserCookieJar(), SamlFlowHost.AppRequest(request => request.CompletionPath = "/settings/linked?tab=sso"));

            HttpResponseMessage response = await host.PostAssertionAsync(host.Respond(begun!.RedirectUrl));

            Assert.AreEqual(HttpStatusCode.SeeOther, response.StatusCode);
            string location = response.Headers.Location!.OriginalString;
            StringAssert.StartsWith(location, "/settings/linked?tab=sso&code=");
            Assert.AreEqual("no-store", response.Headers.CacheControl?.ToString());
            Assert.AreEqual(1, host.Store.Completions.Count);
            Assert.AreEqual(0, host.Store.PendingRequests.Count);
        }

        [TestMethod]
        public async Task AssertionConsumer_SameResponsePostedTwice_IsRefusedTheSecondTime()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(new BrowserCookieJar(), SamlFlowHost.AppRequest());
            string samlResponse = host.Respond(begun!.RedirectUrl);

            HttpResponseMessage first = await host.PostAssertionAsync(samlResponse);
            HttpResponseMessage replay = await host.PostAssertionAsync(samlResponse);

            Assert.AreEqual(HttpStatusCode.SeeOther, first.StatusCode);
            Assert.AreEqual(HttpStatusCode.BadRequest, replay.StatusCode);
            Assert.AreEqual(1, host.Store.Completions.Count);
        }

        [TestMethod]
        public async Task AssertionConsumer_ResponseToARequestThisServerNeverSent_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            string samlResponse = SamlFlowHost.IdentityProvider.Respond("_never-sent", host.Options).ToFormValue();

            HttpResponseMessage response = await host.PostAssertionAsync(samlResponse);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(0, host.Store.Completions.Count);
        }

        [TestMethod]
        public async Task AssertionConsumer_UnsolicitedResponse_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(new BrowserCookieJar(), SamlFlowHost.AppRequest());

            HttpResponseMessage response = await host.PostAssertionAsync(host.Respond(begun!.RedirectUrl, samlResponse =>
            {
                samlResponse.InResponseTo = null;
                samlResponse.ConfirmationInResponseTo = null;
            }));

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.AreEqual(1, host.Store.PendingRequests.Count);
        }

        [TestMethod]
        [DataRow("not base64 at all")]
        [DataRow("PG5vdC14bWw+")]
        public async Task AssertionConsumer_ResponseThatIsNotXml_IsRefused(string samlResponse)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            HttpResponseMessage response = await host.PostAssertionAsync(samlResponse);

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task AssertionConsumer_PostThatIsNotAForm_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            HttpResponseMessage response = await host.Client.PostAsync(
                SamlAuthPaths.AssertionConsumerService,
                new StringContent("{}", System.Text.Encoding.UTF8, "application/json"));

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task AssertionConsumer_ExpiredPendingRequest_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(new BrowserCookieJar(), SamlFlowHost.AppRequest());
            host.Clock.Advance(host.Options.RequestLifetime);

            HttpResponseMessage response = await host.PostAssertionAsync(host.Respond(begun!.RedirectUrl));

            Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
        }

        [TestMethod]
        public async Task Complete_IdentityProviderRemovedAfterBegin_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());
            host.Resolver.IdentityProviders.Clear();

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, await host.AnswerAsync(begun!.RedirectUrl));

            AssertRefused(result, SamlAuthFailureReason.UnknownIdentityProvider, attemptedIdentity: null);
        }

        [TestMethod]
        public async Task Complete_IdentityProviderEntityIdChangedAfterBegin_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());
            FakeSamlIdentityProvider other = new FakeSamlIdentityProvider("https://other-idp.example.test/metadata", SamlTestCertificates.OtherIdentityProvider);
            host.Resolver.IdentityProviders[SamlFlowHost.IdentityProviderKey] = other.Provider;

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, await host.AnswerAsync(begun!.RedirectUrl));

            AssertRefused(result, SamlAuthFailureReason.IdentityProviderChanged, attemptedIdentity: null);
        }

        [TestMethod]
        public async Task Complete_ResponseFailingAValidatorCheck_IsRefusedAsInvalidResponse()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(customizeResponse: response => response.Recipient = "https://other-app.example.test/acs");

            AssertRefused(result, SamlAuthFailureReason.InvalidResponse, attemptedIdentity: null);
        }

        [TestMethod]
        public async Task Complete_ResponseSignedByTheIdentityProviderOfAnotherRequest_IsRefused()
        {
            // A response the other identity provider signed for this very request id still fails: the request named acme.
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());
            FakeSamlIdentityProvider other = new FakeSamlIdentityProvider("https://other-idp.example.test/metadata", SamlTestCertificates.OtherIdentityProvider);
            string samlResponse = other.Respond(new SamlRedirectReader(begun!.RedirectUrl).RequestId, host.Options).ToFormValue();

            HttpResponseMessage posted = await host.PostAssertionAsync(samlResponse);
            Assert.AreEqual(HttpStatusCode.SeeOther, posted.StatusCode, await posted.Content.ReadAsStringAsync());
            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, SamlFlowHost.ReadCode(posted.Headers.Location!));

            AssertRefused(result, SamlAuthFailureReason.InvalidResponse, attemptedIdentity: null);
        }

        [TestMethod]
        [DataRow("urn:oasis:names:tc:SAML:2.0:nameid-format:transient", null, null)]
        [DataRow("urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress", null, null)]
        [DataRow(FakeSamlResponse.PersistentNameIdFormat, "https://other-idp.example.test/metadata", null)]
        [DataRow(FakeSamlResponse.PersistentNameIdFormat, null, "https://other-app.example.test/saml")]
        public async Task Complete_NameIdThatIsNotAPersistentIdentityForThisPair_IsRefused(string format, string? nameQualifier, string? serviceProviderNameQualifier)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(customizeResponse: response =>
            {
                response.NameIdFormat = format;
                response.NameQualifier = nameQualifier;
                response.ServiceProviderNameQualifier = serviceProviderNameQualifier;
            });

            AssertRefused(result, SamlAuthFailureReason.MissingIdentity, attemptedIdentity: null);
        }

        [TestMethod]
        public async Task Complete_PersistentNameIdQualifiedForThisPair_IsAcceptedAsTheSameAccountAsUnqualified()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(customizeResponse: response =>
            {
                response.NameQualifier = SamlFlowHost.IdentityProvider.EntityId;
                response.ServiceProviderNameQualifier = SamlValidationHarness.ServiceProviderEntityId;
            });

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(SamlFlowHost.DefaultIdentity, result.UserInfo.Identity);
        }

        [TestMethod]
        public async Task Complete_AttributeIdentitySource_ReadsTheIdentityFromTheAttributeNotTheNameId()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(identitySource: SamlIdentitySource.Attribute("employeeId"));

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(customizeResponse: response =>
            {
                response.NameIdFormat = "urn:oasis:names:tc:SAML:2.0:nameid-format:transient";
                response.Attributes["employeeId"] = ["E-1001"];
            });

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(new SamlIdentity(SamlFlowHost.IdentityProviderKey, SamlFlowHost.IdentityProvider.EntityId, "E-1001"), result.UserInfo.Identity);
        }

        [TestMethod]
        public async Task Complete_AnotherKeysMetadataClaimingTheSameEntityId_NeverYieldsThatKeysAccounts()
        {
            // Another customer's administrator copies acme's entity id into metadata pinning their own certificate, and
            // signs an assertion for acme's user: it verifies against their key's certificate, under their key.
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeSamlIdentityProvider impostor = new FakeSamlIdentityProvider(SamlFlowHost.IdentityProvider.EntityId, SamlTestCertificates.OtherIdentityProvider);
            host.Resolver.IdentityProviders["other-customer"] = impostor.Provider;
            BrowserCookieJar browser = new BrowserCookieJar();
            SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request => request.IdentityProviderKey = "other-customer"));
            string samlResponse = impostor.Respond(new SamlRedirectReader(begun!.RedirectUrl).RequestId, host.Options, SamlFlowHost.DefaultIdentity.Subject).ToFormValue();

            HttpResponseMessage posted = await host.PostAssertionAsync(samlResponse);
            Assert.AreEqual(HttpStatusCode.SeeOther, posted.StatusCode, await posted.Content.ReadAsStringAsync());
            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, SamlFlowHost.ReadCode(posted.Headers.Location!));

            Assert.IsTrue(result.Success, result.ToString());
            Assert.AreEqual(new SamlIdentity("other-customer", SamlFlowHost.IdentityProvider.EntityId, SamlFlowHost.DefaultIdentity.Subject), result.UserInfo.Identity);
            Assert.AreNotEqual(SamlFlowHost.DefaultIdentity, result.UserInfo.Identity);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(2)]
        public async Task Complete_IdentityAttributeMissingOrMultiValued_IsRefused(int valueCount)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(identitySource: SamlIdentitySource.Attribute("employeeId"));

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(customizeResponse: response =>
            {
                if (valueCount > 0)
                {
                    response.Attributes["employeeId"] = Enumerable.Range(1, valueCount).Select(index => $"E-{index}").ToList();
                }
            });

            AssertRefused(result, SamlAuthFailureReason.MissingIdentity, attemptedIdentity: null);
        }

        [TestMethod]
        public async Task Complete_AnotherAccountThanExpected_IsRefusedNamingIt()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                request => request.ExpectedIdentity = SamlFlowHost.DefaultIdentity,
                response => response.NameId = "user-9999");

            AssertRefused(result, SamlAuthFailureReason.IdentityMismatch, new SamlIdentity(SamlFlowHost.IdentityProviderKey, SamlFlowHost.IdentityProvider.EntityId, "user-9999"));
        }

        [TestMethod]
        public async Task Complete_ExpectedAccount_IsAccepted()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(request => request.ExpectedIdentity = SamlFlowHost.DefaultIdentity);

            Assert.IsTrue(result.Success, result.ToString());
        }

        [TestMethod]
        public async Task Complete_AuthenticationOlderThanTheMaxAge_IsRefusedAsStale()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                request =>
                {
                    request.ForceAuthentication = true;
                    request.MaxAuthenticationAge = MaxAge;
                },
                response => response.AuthenticationInstant = host.Clock.GetUtcNow() - MaxAge - TimeSpan.FromSeconds(1));

            AssertRefused(result, SamlAuthFailureReason.StaleAuthentication, SamlFlowHost.DefaultIdentity);
        }

        [TestMethod]
        [DataRow(-240, DisplayName = "four minutes old")]
        [DataRow(-300, DisplayName = "exactly the max age")]
        [DataRow(60, DisplayName = "a minute ahead of the server clock, which counts as no age")]
        public async Task Complete_AuthenticationNoOlderThanTheMaxAge_IsAccepted(int secondsFromNow)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            // The fake writes the instant to the second, so the clock is put on a whole second for the boundary.
            host.Clock.Advance(TimeSpan.FromTicks(TimeSpan.TicksPerSecond - (host.Clock.GetUtcNow().Ticks % TimeSpan.TicksPerSecond)));

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                request => request.MaxAuthenticationAge = MaxAge,
                response => response.AuthenticationInstant = host.Clock.GetUtcNow().AddSeconds(secondsFromNow));

            Assert.IsTrue(result.Success, result.ToString());
        }

        [TestMethod]
        public async Task Complete_NoAuthnContextClassWhenClassesAreRequired_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            // An AuthnContext must hold something, so one without a class reference holds a declaration reference.
            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                request => request.AcceptedAuthnContextClassRefs = [MfaClass],
                response =>
                {
                    response.AuthnContextClassRef = null;
                    response.AuthnContextDeclRef = "urn:example:declaration";
                });

            AssertRefused(result, SamlAuthFailureReason.MissingAuthnContextClass, SamlFlowHost.DefaultIdentity);
        }

        [TestMethod]
        public async Task Complete_AuthnContextClassNotAccepted_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(request => request.AcceptedAuthnContextClassRefs = [MfaClass]);

            AssertRefused(result, SamlAuthFailureReason.AuthnContextClassNotAccepted, SamlFlowHost.DefaultIdentity);
        }

        [TestMethod]
        public async Task Complete_AcceptedAuthnContextClass_IsAccepted()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                request => request.AcceptedAuthnContextClassRefs = ["urn:other", MfaClass],
                response => response.AuthnContextClassRef = MfaClass);

            Assert.IsTrue(result.Success, result.ToString());
        }

        [TestMethod]
        public async Task Complete_EncryptedAssertion_IsDecryptedAndAccepted()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(decryptionCertificates: [SamlTestCertificates.ServiceProviderDecryption]);

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                customizeResponse: response => response.EncryptFor = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption));

            Assert.IsTrue(result.Success, result.ToString());
        }

        private static void AssertRefused(SamlAuthCompletionResult result, SamlAuthFailureReason reason, SamlIdentity? attemptedIdentity)
        {
            Assert.IsFalse(result.Success, result.ToString());
            Assert.AreEqual(reason, result.FailureReason);
            Assert.AreEqual(attemptedIdentity, result.AttemptedIdentity);
            Assert.IsNull(result.UserInfo);
        }

        private static DateTimeOffset TruncateToSeconds(DateTimeOffset time)
        {
            return time.AddTicks(-(time.Ticks % TimeSpan.TicksPerSecond));
        }
    }
}
