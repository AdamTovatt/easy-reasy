using Microsoft.IdentityModel.Tokens;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The algorithms <see cref="OpenIdConnectProvider.SigningAlgorithms"/> may name. Only asymmetric ones: an HMAC
    /// signature is made with the client secret, so anyone holding the secret could mint an ID token.
    /// </summary>
    internal static class OpenIdConnectSigningAlgorithms
    {
        /// <summary>
        /// RS, PS and ES at 256, 384 and 512 bits.
        /// </summary>
        public static readonly IReadOnlySet<string> Asymmetric = new HashSet<string>(StringComparer.Ordinal)
        {
            SecurityAlgorithms.RsaSha256,
            SecurityAlgorithms.RsaSha384,
            SecurityAlgorithms.RsaSha512,
            SecurityAlgorithms.RsaSsaPssSha256,
            SecurityAlgorithms.RsaSsaPssSha384,
            SecurityAlgorithms.RsaSsaPssSha512,
            SecurityAlgorithms.EcdsaSha256,
            SecurityAlgorithms.EcdsaSha384,
            SecurityAlgorithms.EcdsaSha512,
        };
    }
}
