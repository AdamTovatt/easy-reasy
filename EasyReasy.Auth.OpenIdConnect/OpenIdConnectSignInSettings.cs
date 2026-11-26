namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The security-relevant inputs of a sign-in begun through <c>POST /api/auth/oidc/begin</c>, decided per request by
    /// <see cref="IOpenIdConnectAuthHandler.GetSignInSettingsAsync"/>. The browser's body may choose the provider and carry
    /// a login hint and nothing else, so none of these can be chosen by the caller.
    /// </summary>
    public sealed class OpenIdConnectSignInSettings
    {
        /// <summary>
        /// The provider's sign-in page redirect URI. Must be one of its <see cref="OpenIdConnectProvider.RedirectUris"/>.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The <c>prompt</c> to send, or null to send none.
        /// </summary>
        public OpenIdConnectPrompt? Prompt { get; init; }

        /// <summary>
        /// The longest time since the person last authenticated that the sign-in accepts, or null for no limit. See
        /// <see cref="OpenIdConnectAuthBeginRequest.MaxAuthenticationAge"/>.
        /// </summary>
        public TimeSpan? MaxAuthenticationAge { get; init; }

        /// <summary>
        /// The <c>acr</c> values the sign-in accepts, or null. See <see cref="OpenIdConnectAuthBeginRequest.AcceptedAcrValues"/>.
        /// </summary>
        public IReadOnlyList<string>? AcceptedAcrValues { get; init; }

        /// <summary>
        /// The <c>amr</c> values the sign-in accepts, or null. See <see cref="OpenIdConnectAuthBeginRequest.AcceptedAmrValues"/>.
        /// </summary>
        public IReadOnlyList<string>? AcceptedAmrValues { get; init; }

        /// <summary>
        /// The claim values the sign-in requires, or null. See <see cref="OpenIdConnectAuthBeginRequest.RequiredClaimValues"/>.
        /// </summary>
        public IReadOnlyDictionary<string, string>? RequiredClaimValues { get; init; }
    }
}
