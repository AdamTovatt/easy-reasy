using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml;

namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// <see cref="ISamlAuthService.BeginAsync"/>: the AuthnRequest an identity provider receives, the binding cookie, the
    /// sealed pending request, and the requests it refuses.
    /// </summary>
    [TestClass]
    public class SamlAuthBeginTests
    {
        [TestMethod]
        public async Task BeginAsync_KnownIdentityProvider_RedirectsToItsSingleSignOnUrlWithTheAuthnRequest()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeBrowser browser = new FakeBrowser(host.Clock);

            SamlAuthBeginResult? result = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());

            Assert.IsNotNull(result);
            SamlRedirectReader request = new SamlRedirectReader(result.RedirectUrl);
            Assert.AreEqual(SamlFlowHost.IdentityProvider.SingleSignOnUrl.AbsoluteUri, request.Url.GetLeftPart(UriPartial.Path));
            Assert.AreEqual("AuthnRequest", request.Root.LocalName);
            Assert.AreEqual(SamlFlowHost.IdentityProvider.SingleSignOnUrl.AbsoluteUri, request.Root.GetAttribute("Destination"));
            Assert.AreEqual(SamlValidationHarness.AssertionConsumerServiceUrl, request.Root.GetAttribute("AssertionConsumerServiceURL"));
            Assert.AreEqual("urn:oasis:names:tc:SAML:2.0:bindings:HTTP-POST", request.Root.GetAttribute("ProtocolBinding"));
            Assert.AreEqual(SamlValidationHarness.ServiceProviderEntityId, request.Root["Issuer", FakeSamlResponse.AssertionNamespace]!.InnerText);
            Assert.AreEqual(FakeSamlResponse.PersistentNameIdFormat, request.Child("NameIDPolicy")!.GetAttribute("Format"));
            Assert.AreEqual(string.Empty, request.Root.GetAttribute("ForceAuthn"));
            Assert.IsNull(request.Child("RequestedAuthnContext"));
            Assert.IsFalse(request.Query.ContainsKey("Signature"));
        }

        [TestMethod]
        public async Task Begin_KnownIdentityProvider_SetsAHostOnlyLaxBindingCookie()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            HttpResponseMessage response = await host.PostBeginAsync(new FakeBrowser(host.Clock));

            Microsoft.Net.Http.Headers.SetCookieHeaderValue cookie = Microsoft.Net.Http.Headers.SetCookieHeaderValue.ParseList(response.Headers.GetValues("Set-Cookie").ToList()).Single();
            StringAssert.StartsWith(cookie.Name.Value, "__Host-easyreasy-saml-");
            Assert.IsTrue(cookie.Secure);
            Assert.IsTrue(cookie.HttpOnly);
            Assert.AreEqual(Microsoft.Net.Http.Headers.SameSiteMode.Lax, cookie.SameSite);
            Assert.AreEqual("/", cookie.Path.Value);
            Assert.AreEqual(host.Options.RequestLifetime + host.Options.CompletionLifetime, cookie.MaxAge);
        }

        [TestMethod]
        public async Task BeginAsync_KnownIdentityProvider_StoresThePendingRequestSealedUnderTheHashOfItsId()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeBrowser browser = new FakeBrowser(host.Clock);

            SamlAuthBeginResult? result = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request => request.CallerBinding = "record-42"));

            SamlAuthRecord record = host.Store.PendingRequests.Single();
            string requestId = new SamlRedirectReader(result!.RedirectUrl).RequestId;
            Assert.AreEqual(SecureToken.Hash(requestId), record.Hash);
            Assert.AreEqual((host.Clock.GetUtcNow() + host.Options.RequestLifetime).UtcDateTime, record.ExpiresAt);
            Assert.IsFalse(record.SealedValue.Contains("record-42", StringComparison.Ordinal));
            Assert.IsFalse(record.SealedValue.Contains(SamlFlowHost.IdentityProviderKey, StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task BeginAsync_UnknownIdentityProvider_ReturnsNullWithoutCookieOrPendingRequest()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeBrowser browser = new FakeBrowser(host.Clock);

            SamlAuthBeginResult? result = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request => request.IdentityProviderKey = "nobody"));

            Assert.IsNull(result);
            Assert.AreEqual(0, browser.Cookies.Count);
            Assert.AreEqual(0, host.Store.PendingRequests.Count);
        }

        [TestMethod]
        public async Task BeginAsync_ForceAuthenticationAndAcceptedClasses_AreSentInTheAuthnRequest()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthBeginResult? result = await host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest(request =>
            {
                request.ForceAuthentication = true;
                request.AcceptedAuthnContextClassRefs = ["urn:example:mfa", "urn:example:hardware-key"];
            }));

            SamlRedirectReader reader = new SamlRedirectReader(result!.RedirectUrl);
            Assert.AreEqual("true", reader.Root.GetAttribute("ForceAuthn"));
            XmlElement requested = reader.Child("RequestedAuthnContext")!;
            Assert.AreEqual("exact", requested.GetAttribute("Comparison"));
            CollectionAssert.AreEqual(
                new[] { "urn:example:mfa", "urn:example:hardware-key" },
                requested.ChildNodes.OfType<XmlElement>().Select(element => element.InnerText).ToArray());
        }

        [TestMethod]
        public async Task BeginAsync_AttributeIdentitySource_AsksForNoNameIdFormat()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(identitySource: SamlIdentitySource.Attribute("employeeId"));

            SamlAuthBeginResult? result = await host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest());

            Assert.IsNull(new SamlRedirectReader(result!.RedirectUrl).Child("NameIDPolicy"));
        }

        [TestMethod]
        public async Task BeginAsync_SigningCertificate_SignsTheQueryStringVerifiablyWithItsPublicKey()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync(signingCertificate: SamlTestCertificates.ServiceProviderDecryption);

            SamlAuthBeginResult? result = await host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest());

            // The HTTP-Redirect binding signs the URL-encoded SAMLRequest, RelayState and SigAlg exactly as they appear.
            string query = new Uri(result!.RedirectUrl).Query.TrimStart('?');
            string[] parts = query.Split('&');
            string signedPart = string.Join("&", parts.Where(part => !part.StartsWith("Signature=", StringComparison.Ordinal)));
            string signature = Uri.UnescapeDataString(parts.Single(part => part.StartsWith("Signature=", StringComparison.Ordinal))["Signature=".Length..]);
            Assert.AreEqual("http://www.w3.org/2001/04/xmldsig-more#rsa-sha256", new SamlRedirectReader(result.RedirectUrl).Query["SigAlg"].ToString());
            using RSA publicKey = SamlTestCertificates.PublicOnly(SamlTestCertificates.ServiceProviderDecryption).GetRSAPublicKey()!;
            Assert.IsTrue(publicKey.VerifyData(Encoding.UTF8.GetBytes(signedPart), Convert.FromBase64String(signature), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));
        }

        [TestMethod]
        public async Task BeginAsync_IdentityProviderWantsSignedRequestsWithoutSigningCertificate_Throws()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            host.Resolver.IdentityProviders[SamlFlowHost.IdentityProviderKey] = new ConfiguredSamlIdentityProvider(SamlFlowHost.IdentityProvider.MetadataXml(wantsSignedRequests: true));

            await Assert.ThrowsExceptionAsync<InvalidOperationException>(() => host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest()));
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("signed-in")]
        [DataRow("//evil.example/path")]
        [DataRow("/\\evil.example")]
        [DataRow("https://evil.example/")]
        [DataRow("/signed-in#done")]
        public async Task BeginAsync_CompletionPathOffThisSiteOrWithAFragment_Throws(string completionPath)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeBrowser browser = new FakeBrowser(host.Clock);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(
                () => host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest(request => request.CompletionPath = completionPath)));

            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.CompletionPath));
            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task BeginAsync_SeveralInvalidFields_ReportsEveryProblemAtOnce()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest(request =>
            {
                request.CompletionPath = "relative";
                request.MaxAuthenticationAge = TimeSpan.Zero;
                request.AcceptedAuthnContextClassRefs = [" "];
            })));

            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.CompletionPath));
            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.MaxAuthenticationAge));
            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.AcceptedAuthnContextClassRefs));
        }

        [TestMethod]
        public async Task BeginAsync_BlankKeyNullCallerBindingAndNoAcceptedClasses_ReportsEachWithoutResolving()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest(request =>
            {
                request.IdentityProviderKey = " ";
                request.CallerBinding = null!;
                request.AcceptedAuthnContextClassRefs = [];
            })));

            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.IdentityProviderKey));
            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.CallerBinding));
            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.AcceptedAuthnContextClassRefs));
            Assert.AreEqual(0, host.Resolver.Calls);
        }

        [TestMethod]
        public async Task BeginAsync_ExpectedIdentityAtAnotherIdentityProvider_Throws()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest(
                request => request.ExpectedIdentity = new SamlIdentity(SamlFlowHost.IdentityProviderKey, "https://other-idp.example.test/metadata", FakeSamlIdentityProvider.DefaultNameId))));

            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.ExpectedIdentity));
        }

        [TestMethod]
        public async Task BeginAsync_ExpectedIdentityThroughAnotherKey_Throws()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            SamlIdentity otherKeysIdentity = new SamlIdentity("other-customer", SamlFlowHost.IdentityProvider.EntityId, FakeSamlIdentityProvider.DefaultNameId);

            ArgumentException exception = await Assert.ThrowsExceptionAsync<ArgumentException>(() => host.ServiceBeginAsync(new FakeBrowser(host.Clock), SamlFlowHost.AppRequest(
                request => request.ExpectedIdentity = otherKeysIdentity)));

            StringAssert.Contains(exception.Message, nameof(SamlAuthBeginRequest.ExpectedIdentity));
            Assert.AreEqual(0, host.Store.PendingRequests.Count);
        }

        [TestMethod]
        public async Task BeginAsync_BrowserAlreadyHoldingTheMostPendingFlows_ClearsThemAndKeepsOnlyTheNewOne()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeBrowser browser = new FakeBrowser(host.Clock);
            List<string> abandoned = new List<string>();
            for (int flow = 0; flow < FlowBindingCookie.MaxPendingFlows; flow++)
            {
                SamlAuthBeginResult? begun = await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());
                abandoned.Add(begun!.RedirectUrl);
            }

            Assert.AreEqual(FlowBindingCookie.MaxPendingFlows, browser.Cookies.Count);

            string code = await host.BeginAndAnswerAsync(browser);
            SamlAuthCompletionResult newest = await host.ServiceCompleteAsync(browser, code);
            SamlAuthCompletionResult oldest = await host.ServiceCompleteAsync(browser, await host.AnswerAsync(abandoned[0]));

            Assert.IsTrue(newest.Success, newest.ToString());
            Assert.AreEqual(SamlAuthFailureReason.BindingCookieMissing, oldest.FailureReason);
        }

        [TestMethod]
        public async Task BeginAsync_BrowserHoldingFewerThanTheMostPendingFlows_KeepsThemAll()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            FakeBrowser browser = new FakeBrowser(host.Clock);
            for (int flow = 0; flow < FlowBindingCookie.MaxPendingFlows - 1; flow++)
            {
                await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());
            }

            await host.ServiceBeginAsync(browser, SamlFlowHost.AppRequest());

            Assert.AreEqual(FlowBindingCookie.MaxPendingFlows, browser.Cookies.Count);
        }
    }
}
