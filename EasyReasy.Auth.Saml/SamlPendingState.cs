namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What Begin seals with a pending request: the browser binding, the identity provider the response must come from,
    /// and the rules its assertion must meet.
    /// </summary>
    internal sealed class SamlPendingState
    {
        public required string FlowId { get; init; }

        public required string BindingCookieHash { get; init; }

        public required string IdentityProviderKey { get; init; }

        public required string IdentityProviderEntityId { get; init; }

        public required string CallerBinding { get; init; }

        public required string CompletionPath { get; init; }

        public required TimeSpan? MaxAuthenticationAge { get; init; }

        public required List<string>? AcceptedAuthnContextClassRefs { get; init; }

        public required SamlIdentity? ExpectedIdentity { get; init; }
    }
}
