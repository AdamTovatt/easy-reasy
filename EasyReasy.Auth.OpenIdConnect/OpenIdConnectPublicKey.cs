using Microsoft.IdentityModel.Tokens;
using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Writes the key an ID token was verified with as a public JSON Web Key, so the token can be checked again after the
    /// provider has rotated the key out of its key set.
    /// </summary>
    internal static class OpenIdConnectPublicKey
    {
        /// <summary>
        /// The key's public members as JWK JSON: <c>kty</c>, <c>kid</c>, <c>alg</c> and <c>use</c> when known, <c>n</c> and
        /// <c>e</c> for RSA, <c>crv</c>, <c>x</c> and <c>y</c> for EC, and <c>x5c</c> when the key set gave certificates.
        /// Never a private member, whatever the key holds.
        /// </summary>
        /// <param name="key">The key the token handler verified the signature with.</param>
        /// <returns>The JWK JSON.</returns>
        /// <exception cref="NotSupportedException">The key is of a kind no JWK describes; not one a key set yields.</exception>
        public static string ToJson(SecurityKey key)
        {
            JsonWebKey jwk = key as JsonWebKey ?? JsonWebKeyConverter.ConvertFromSecurityKey(key);
            Dictionary<string, object> members = new Dictionary<string, object>();

            AddIfPresent(members, "kty", jwk.Kty);
            AddIfPresent(members, "kid", jwk.Kid ?? key.KeyId);
            AddIfPresent(members, "alg", jwk.Alg);
            AddIfPresent(members, "use", jwk.Use);
            AddIfPresent(members, "n", jwk.N);
            AddIfPresent(members, "e", jwk.E);
            AddIfPresent(members, "crv", jwk.Crv);
            AddIfPresent(members, "x", jwk.X);
            AddIfPresent(members, "y", jwk.Y);

            if (jwk.X5c.Count > 0)
            {
                members["x5c"] = jwk.X5c.ToArray();
            }

            return JsonSerializer.Serialize(members);
        }

        private static void AddIfPresent(Dictionary<string, object> members, string name, string? value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                members[name] = value;
            }
        }
    }
}
