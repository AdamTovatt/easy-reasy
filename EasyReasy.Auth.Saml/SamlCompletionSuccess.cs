namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// A response that passed every check: the user it signed in.
    /// </summary>
    internal sealed class SamlCompletionSuccess : SamlCompletionOutcome
    {
        public required SamlIdentity Identity { get; init; }

        public required DateTimeOffset AuthenticationInstant { get; init; }

        public required string? AuthnContextClassRef { get; init; }

        public required Dictionary<string, List<string>> Attributes { get; init; }
    }
}
