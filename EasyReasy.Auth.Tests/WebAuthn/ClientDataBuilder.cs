using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// The collected client data a browser writes, field by field, defaulted to the values that verify.
    /// </summary>
    /// <remarks>
    /// Held by both synthetic ceremonies rather than copied into each, the way
    /// <see cref="AuthenticatorDataBuilder"/> is: the client data checks are the same checks on both paths,
    /// so a test that breaks one of them should be breaking the same field in the same place whichever
    /// ceremony it is about.
    /// </remarks>
    internal sealed class ClientDataBuilder
    {
        /// <summary>The ceremony the client data records.</summary>
        public string CeremonyType { get; set; }

        /// <summary>The challenge the client data answers, base64url-encoded.</summary>
        public string Challenge { get; set; }

        /// <summary>The origin the client data reports the ceremony ran at.</summary>
        public string Origin { get; set; } = WebAuthnTestData.Origin;

        /// <summary>
        /// The <c>crossOrigin</c> the client data reports, or null to leave the field out entirely — which
        /// is the case a browser produces when the page is not framed at all.
        /// </summary>
        public bool? CrossOrigin { get; set; } = false;

        /// <summary>
        /// Client data JSON to send instead of one built from the fields above, for the cases where what is
        /// wrong with it is that it is not client data.
        /// </summary>
        public string? Override { get; set; }

        /// <summary>
        /// Creates client data for one ceremony answering one challenge.
        /// </summary>
        /// <param name="ceremonyType">The ceremony the client data records.</param>
        /// <param name="challenge">The base64url challenge the ceremony answers.</param>
        public ClientDataBuilder(string ceremonyType, string challenge)
        {
            CeremonyType = ceremonyType;
            Challenge = challenge;
        }

        /// <summary>
        /// Builds the collected client data JSON, as the browser would write it.
        /// </summary>
        public string Build()
        {
            if (Override != null)
            {
                return Override;
            }

            Dictionary<string, object> fields = new Dictionary<string, object>
            {
                ["type"] = CeremonyType,
                ["challenge"] = Challenge,
                ["origin"] = Origin,
            };

            if (CrossOrigin != null)
            {
                fields["crossOrigin"] = CrossOrigin.Value;
            }

            return JsonSerializer.Serialize(fields);
        }
    }
}
