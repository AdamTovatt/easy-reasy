using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// One algorithm a relying party is willing to accept a credential for (WebAuthn Level 3 §5.3).
    /// </summary>
    public sealed class PublicKeyCredentialParameters
    {
        /// <summary>The credential type, always <c>public-key</c>.</summary>
        [JsonPropertyName("type")]
        public string Type => PublicKeyCredentialType.PublicKey;

        /// <summary>
        /// The COSE algorithm identifier, as a number — this is the one place in the contract where an
        /// algorithm is named by its IANA value rather than a string.
        /// </summary>
        [JsonPropertyName("alg")]
        public int Algorithm { get; }

        /// <summary>
        /// Initializes the parameters for one algorithm. Internal: the set of algorithms this library
        /// accepts is fixed by what verification implements, so it is not a caller's to assemble.
        /// </summary>
        /// <param name="algorithm">The algorithm to offer.</param>
        internal PublicKeyCredentialParameters(CoseAlgorithm algorithm)
        {
            Algorithm = (int)algorithm;
        }
    }
}
