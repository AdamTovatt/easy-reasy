using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The consent endpoints' policy on its own, apart from the bearer's confinement of connection tokens that stops a
    /// client's token before it: a person signed in to the app passes, a client's token and an API key's do not.
    /// </summary>
    [TestClass]
    public class OAuthConsentPolicyTests
    {
        [TestMethod]
        [DataRow(EasyReasyAuthTypes.User, false, true, DisplayName = "a person")]
        [DataRow(EasyReasyAuthTypes.User, true, false, DisplayName = "a connection acting for a person")]
        [DataRow(EasyReasyAuthTypes.ApiKey, false, false, DisplayName = "an API key")]
        public async Task Build_EvaluatedForACaller_AllowsOnlyAPerson(string authType, bool hasClientId, bool allowed)
        {
            ServiceCollection services = new ServiceCollection();
            services.AddLogging();
            services.AddAuthorization();
            using ServiceProvider provider = services.BuildServiceProvider();
            List<Claim> claims = [new Claim("sub", "person-1"), new Claim(EasyReasyClaimTypes.AuthType, authType)];
            if (hasClientId)
            {
                claims.Add(new Claim(EasyReasyClaimTypes.ClientId, "client-1"));
            }

            ClaimsPrincipal caller = new ClaimsPrincipal(new ClaimsIdentity(claims, "Bearer"));

            AuthorizationResult result = await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(caller, null, OAuthConsentPolicy.Build());

            Assert.AreEqual(allowed, result.Succeeded);
        }
    }
}
