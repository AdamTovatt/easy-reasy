using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// A response that passed every check: the user it signed in.
    /// </summary>
    internal sealed class SamlCompletionSuccess : SamlCompletionOutcome
    {
        public required SamlIdentity Identity { get; init; }

        /// <inheritdoc />
        [JsonIgnore]
        public override SamlIdentity? AttemptedIdentity => Identity;

        public required DateTimeOffset AuthenticationInstant { get; init; }

        public required string? AuthnContextClassRef { get; init; }

        public required IReadOnlyDictionary<string, IReadOnlyList<string>> Attributes { get; init; }
    }
}
