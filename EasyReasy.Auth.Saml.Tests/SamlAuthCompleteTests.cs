namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// <see cref="ISamlAuthService.CompleteAsync"/>: a completion code is redeemed once, within its lifetime, by the browser
    /// that began the sign-in, whose binding cookie it then clears.
    /// </summary>
    [TestClass]
    public class SamlAuthCompleteTests
    {
        [TestMethod]
        public async Task CompleteAsync_CodeRedeemedTwice_IsRefusedTheSecondTime()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(browser);

            SamlAuthCompletionResult first = await host.ServiceCompleteAsync(browser, code);
            SamlAuthCompletionResult second = await host.ServiceCompleteAsync(browser, code);

            Assert.IsTrue(first.Success, first.ToString());
            Assert.AreEqual(SamlAuthFailureReason.UnknownCompletionCode, second.FailureReason);
        }

        [TestMethod]
        public async Task CompleteAsync_Success_ClearsTheBindingCookie()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(browser);
            Assert.AreEqual(1, browser.Cookies.Count);

            await host.ServiceCompleteAsync(browser, code);

            Assert.AreEqual(0, browser.Cookies.Count);
        }

        [TestMethod]
        public async Task CompleteAsync_BrowserWithoutTheBindingCookie_IsRefusedAndTheCodeIsSpent()
        {
            // Login CSRF, or a stolen code: another browser holds no cookie for the flow.
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar starter = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(starter);

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(new BrowserCookieJar(), code);
            SamlAuthCompletionResult retry = await host.ServiceCompleteAsync(starter, code);

            Assert.AreEqual(SamlAuthFailureReason.BindingCookieMissing, result.FailureReason);
            Assert.IsNull(result.AttemptedIdentity);
            Assert.AreEqual(SamlAuthFailureReason.UnknownCompletionCode, retry.FailureReason);
        }

        [TestMethod]
        public async Task CompleteAsync_BindingCookieOfAnotherFlow_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(browser);
            string name = browser.Cookies.Keys.Single();
            browser.Receive([$"{name}=forged-value; path=/; secure; httponly; samesite=lax"]);

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, code);

            Assert.AreEqual(SamlAuthFailureReason.BindingCookieMismatch, result.FailureReason);
        }

        [TestMethod]
        public async Task CompleteAsync_CodePastItsLifetime_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(browser);
            host.Clock.Advance(host.Options.CompletionLifetime);

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, code);

            Assert.AreEqual(SamlAuthFailureReason.UnknownCompletionCode, result.FailureReason);
        }

        [TestMethod]
        [DataRow("")]
        [DataRow("never-issued")]
        public async Task CompleteAsync_CodeNeverIssued_IsRefused(string code)
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(new BrowserCookieJar(), code);

            Assert.AreEqual(SamlAuthFailureReason.UnknownCompletionCode, result.FailureReason);
        }

        [TestMethod]
        public async Task CompleteAsync_StoredRecordNotSealedByThisKey_IsRefused()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            await host.Store.StoreCompletionAsync(new SamlCompletionRecord(
                System.Buffers.Text.Base64Url.EncodeToString(System.Security.Cryptography.SHA256.HashData("planted"u8)),
                "bm90LXNlYWxlZA",
                host.Clock.GetUtcNow().UtcDateTime.AddMinutes(1)));

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(new BrowserCookieJar(), "planted");

            Assert.AreEqual(SamlAuthFailureReason.UnknownCompletionCode, result.FailureReason);
        }

        [TestMethod]
        public async Task CompletionResult_ToString_LeavesOutTheUser()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            SamlAuthCompletionResult result = await host.SignInThroughServiceAsync(
                customizeResponse: response => response.Attributes["email"] = ["person@example.test"]);

            string text = result.ToString();
            StringAssert.Contains(text, "Success = true");
            Assert.IsFalse(text.Contains("person@example.test", StringComparison.Ordinal));
        }

        [TestMethod]
        public async Task CompleteAsync_CodeBegunWithAnotherCallerBinding_IsRefusedAndTheCodeIsSpent()
        {
            // A re-check endpoint handed the code of a plain sign-in, which carried none of the re-check's rules.
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(browser, request => request.CallerBinding = "plain-sign-in");

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, code, "re-check:user-7:record-9");
            SamlAuthCompletionResult retry = await host.ServiceCompleteAsync(browser, code, "plain-sign-in");

            Assert.AreEqual(SamlAuthFailureReason.CallerBindingMismatch, result.FailureReason);
            Assert.AreEqual(SamlFlowHost.DefaultIdentity, result.AttemptedIdentity);
            Assert.IsNull(result.UserInfo);
            Assert.AreEqual(SamlAuthFailureReason.UnknownCompletionCode, retry.FailureReason);
        }

        [TestMethod]
        public async Task CompleteAsync_RefusedResponseBegunWithAnotherCallerBinding_IsRefusedAsCallerBindingMismatch()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();
            BrowserCookieJar browser = new BrowserCookieJar();
            string code = await host.BeginAndAnswerAsync(
                browser,
                request =>
                {
                    request.CallerBinding = "plain-sign-in";
                    request.AcceptedAuthnContextClassRefs = ["urn:example:mfa"];
                });

            SamlAuthCompletionResult result = await host.ServiceCompleteAsync(browser, code, "re-check:user-7:record-9");

            Assert.AreEqual(SamlAuthFailureReason.CallerBindingMismatch, result.FailureReason);
            Assert.AreEqual(SamlFlowHost.DefaultIdentity, result.AttemptedIdentity);
        }

        [TestMethod]
        public async Task CompleteAsync_NullExpectedCallerBinding_Throws()
        {
            await using SamlFlowHost host = await SamlFlowHost.StartAsync();

            ArgumentNullException exception = await Assert.ThrowsExceptionAsync<ArgumentNullException>(() => host.ServiceCompleteAsync(new BrowserCookieJar(), "any-code", null!));

            Assert.AreEqual("expectedCallerBinding", exception.ParamName);
        }
    }
}
