using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// The ID token a <see cref="FakeOpenIdConnectIssuer"/> will issue for one code: its claims and the key and algorithm
    /// it is signed with. A test changes any of them to produce the token a refusal needs.
    /// </summary>
    internal sealed class FakeIdTokenSettings
    {
        public FakeIdTokenSettings(Dictionary<string, object> claims, SecurityKey signingKey)
        {
            Claims = claims;
            SigningKey = signingKey;
        }

        public Dictionary<string, object> Claims { get; }

        public SecurityKey SigningKey { get; set; }

        public string Algorithm { get; set; } = SecurityAlgorithms.RsaSha256;
    }
}
