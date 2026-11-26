using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace EasyReasy.Auth.Tests
{
    [TestClass]
    public class FlowBindingCookieTests
    {
        private const string Prefix = "__Host-test-flow-";

        private static readonly DateTimeOffset StartTime = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);

        [TestMethod]
        [DataRow("easyreasy-flow-")]
        [DataRow("__Secure-flow-")]
        [DataRow("__Host-")]
        [DataRow("__Host-flow;")]
        [DataRow("__Host-flow =")]
        public void Constructor_PrefixNotHostPrefixedCookieTokenCharacters_Throws(string prefix)
        {
            Assert.ThrowsException<ArgumentException>(() => new FlowBindingCookie(prefix));
        }

        [TestMethod]
        [DataRow("__Host-a")]
        [DataRow("__Host-easyreasy.flow_1-")]
        public void Constructor_PrefixOfCookieTokenCharacters_IsAccepted(string prefix)
        {
            Assert.AreEqual(prefix, new FlowBindingCookie(prefix).NamePrefix);
        }

        [TestMethod]
        public void Issue_NewFlow_SetsHostPrefixedHttpOnlySecureLaxCookieWithTheLifetime()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            DefaultHttpContext context = new DefaultHttpContext();

            FlowBinding binding = cookie.Issue(context, TimeSpan.FromMinutes(10));

            SetCookieHeaderValue setCookie = ReadSetCookies(context.Response).Single();
            Assert.AreEqual(Prefix + binding.FlowId, setCookie.Name.ToString());
            AssertHostCookieAttributes(setCookie);
            Assert.AreEqual(TimeSpan.FromMinutes(10), setCookie.MaxAge);
            Assert.AreEqual(SecureToken.Hash(setCookie.Value.ToString()), binding.ValueHash);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(-1)]
        public void Issue_NonPositiveLifetime_Throws(int lifetimeSeconds)
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);

            Assert.ThrowsException<ArgumentOutOfRangeException>(() => cookie.Issue(new DefaultHttpContext(), TimeSpan.FromSeconds(lifetimeSeconds)));
        }

        [TestMethod]
        public void Issue_TwoFlows_GetDifferentIdsAndValues()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);

            FlowBinding first = cookie.Issue(new DefaultHttpContext(), TimeSpan.FromMinutes(10));
            FlowBinding second = cookie.Issue(new DefaultHttpContext(), TimeSpan.FromMinutes(10));

            Assert.AreNotEqual(first.FlowId, second.FlowId);
            Assert.AreNotEqual(first.ValueHash, second.ValueHash);
        }

        [TestMethod]
        public void Check_TheBrowserThatBeganTheFlow_Matches()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            FlowBinding binding = Issue(cookie, browser);

            Assert.AreEqual(FlowBindingCookieCheck.Matches, cookie.Check(CreateContext(browser), binding));
        }

        [TestMethod]
        public void Check_AnotherBrowser_IsMissing()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeTimeProvider clock = new FakeTimeProvider(StartTime);
            FlowBinding binding = Issue(cookie, new FakeBrowser(clock));

            Assert.AreEqual(FlowBindingCookieCheck.Missing, cookie.Check(CreateContext(new FakeBrowser(clock)), binding));
        }

        [TestMethod]
        public void Check_ACookieForTheFlowWithAnotherValue_IsAMismatch()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            FlowBinding binding = Issue(cookie, browser);
            browser.Set(Prefix + binding.FlowId, "planted-value");

            Assert.AreEqual(FlowBindingCookieCheck.Mismatch, cookie.Check(CreateContext(browser), binding));
        }

        [TestMethod]
        public void Check_ACookieForTheFlowWithAnEmptyValue_IsMissing()
        {
            // Pins the request cookie parser's dropping of an empty value, which Check relies on.
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            FlowBinding binding = Issue(cookie, browser);
            browser.Set(Prefix + binding.FlowId, "");

            Assert.AreEqual(FlowBindingCookieCheck.Missing, cookie.Check(CreateContext(browser), binding));
        }

        [TestMethod]
        public void Check_AfterTheCookiesLifetime_IsMissing()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeTimeProvider clock = new FakeTimeProvider(StartTime);
            FakeBrowser browser = new FakeBrowser(clock);
            FlowBinding binding = Issue(cookie, browser);

            clock.Advance(TimeSpan.FromMinutes(11));

            Assert.AreEqual(FlowBindingCookieCheck.Missing, cookie.Check(CreateContext(browser), binding));
        }

        [TestMethod]
        public void Clear_IssuedFlow_LeavesASecondCheckMissing()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            FlowBinding binding = Issue(cookie, browser);
            DefaultHttpContext completion = CreateContext(browser);

            cookie.Clear(completion, binding);
            browser.Absorb(completion.Response);

            Assert.AreEqual(FlowBindingCookieCheck.Missing, cookie.Check(CreateContext(browser), binding));
        }

        [TestMethod]
        public void Clear_IssuedFlow_DeletesWithTheHostAttributesABrowserRequires()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            FlowBinding binding = Issue(cookie, browser);
            DefaultHttpContext completion = CreateContext(browser);

            cookie.Clear(completion, binding);

            SetCookieHeaderValue deletion = ReadSetCookies(completion.Response).Single();
            Assert.AreEqual(Prefix + binding.FlowId, deletion.Name.ToString());
            AssertHostCookieAttributes(deletion);
            Assert.IsTrue(deletion.Expires < StartTime);
        }

        [TestMethod]
        public void Issue_OverTheLimit_ClearsThisPrefixsPendingCookiesOnlyAndKeepsTheNewOne()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FlowBindingCookie otherKind = new FlowBindingCookie("__Host-other-flow-");
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            FlowBinding other = Issue(otherKind, browser);
            List<FlowBinding> pending = Enumerable.Range(0, FlowBindingCookie.MaxPendingFlows).Select(_ => Issue(cookie, browser)).ToList();

            FlowBinding latest = Issue(cookie, browser);

            Assert.AreEqual(1, browser.Cookies.Keys.Count(name => name.StartsWith(Prefix, StringComparison.Ordinal)));
            Assert.AreEqual(FlowBindingCookieCheck.Matches, cookie.Check(CreateContext(browser), latest));
            Assert.AreEqual(FlowBindingCookieCheck.Missing, cookie.Check(CreateContext(browser), pending[0]));
            Assert.AreEqual(FlowBindingCookieCheck.Matches, otherKind.Check(CreateContext(browser), other));
        }

        [TestMethod]
        public void Issue_OverTheLimit_DeletesWithTheHostAttributesABrowserRequires()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));
            List<FlowBinding> pending = Enumerable.Range(0, FlowBindingCookie.MaxPendingFlows).Select(_ => Issue(cookie, browser)).ToList();
            DefaultHttpContext context = CreateContext(browser);

            cookie.Issue(context, TimeSpan.FromMinutes(10));

            List<SetCookieHeaderValue> deletions = ReadSetCookies(context.Response).Where(setCookie => setCookie.MaxAge == null).ToList();
            CollectionAssert.AreEquivalent(pending.Select(binding => Prefix + binding.FlowId).ToList(), deletions.Select(deletion => deletion.Name.ToString()).ToList());
            foreach (SetCookieHeaderValue deletion in deletions)
            {
                AssertHostCookieAttributes(deletion);
                Assert.IsTrue(deletion.Expires < StartTime);
            }
        }

        [TestMethod]
        public void Issue_UpToTheLimit_KeepsEveryCookie()
        {
            FlowBindingCookie cookie = new FlowBindingCookie(Prefix);
            FakeBrowser browser = new FakeBrowser(new FakeTimeProvider(StartTime));

            List<FlowBinding> pending = Enumerable.Range(0, FlowBindingCookie.MaxPendingFlows).Select(_ => Issue(cookie, browser)).ToList();

            Assert.IsTrue(pending.All(binding => cookie.Check(CreateContext(browser), binding) == FlowBindingCookieCheck.Matches));
        }

        private static List<SetCookieHeaderValue> ReadSetCookies(HttpResponse response)
        {
            return SetCookieHeaderValue.ParseList(response.Headers.SetCookie.Select(value => value ?? "").ToList()).ToList();
        }

        /// <summary>
        /// What a browser insists on for a <c>__Host-</c> cookie, deleting one included, and what the binding adds.
        /// </summary>
        private static void AssertHostCookieAttributes(SetCookieHeaderValue setCookie)
        {
            Assert.IsTrue(setCookie.Secure);
            Assert.AreEqual("/", setCookie.Path.ToString());
            Assert.IsTrue(StringSegment.IsNullOrEmpty(setCookie.Domain));
            Assert.IsTrue(setCookie.HttpOnly);
            Assert.AreEqual(Microsoft.Net.Http.Headers.SameSiteMode.Lax, setCookie.SameSite);
        }

        private static FlowBinding Issue(FlowBindingCookie cookie, FakeBrowser browser)
        {
            DefaultHttpContext context = CreateContext(browser);
            FlowBinding binding = cookie.Issue(context, TimeSpan.FromMinutes(10));
            browser.Absorb(context.Response);
            return binding;
        }

        private static DefaultHttpContext CreateContext(FakeBrowser browser)
        {
            DefaultHttpContext context = new DefaultHttpContext();
            browser.ApplyTo(context.Request);
            return context;
        }
    }
}
