using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.Microsoft.Tests
{
    /// <summary>
    /// The ID token <see cref="FakeMicrosoftIdentityProvider"/> will issue for one code: its claims and the key it is
    /// signed with. A test changes either to produce the token a refusal needs.
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
