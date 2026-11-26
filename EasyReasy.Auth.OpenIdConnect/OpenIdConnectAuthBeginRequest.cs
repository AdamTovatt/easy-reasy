namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// What a flow asks of the provider, and what its completion will check: the input to
    /// <see cref="IOpenIdConnectAuthService.BeginAsync"/>.
    /// </summary>
    /// <remarks>
    /// Every value here except <see cref="ProviderKey"/> and <see cref="LoginHint"/> decides what the resulting sign-in
    /// proves, so build it from the application's own rules for the flow, never from what the browser sends.
    /// <para>
    /// A re-check at a sensitive moment is <see cref="Prompt"/> <see cref="OpenIdConnectPrompt.Login"/> with a
    /// <see cref="MaxAuthenticationAge"/>, and optionally <see cref="AcceptedAcrValues"/> or
    /// <see cref="AcceptedAmrValues"/>. A matching <c>acr</c> or <c>amr</c> proves what the provider asserts, not that a
    /// factor ran for this act: a provider may report a factor from earlier in its session.
    /// </para>
    /// </remarks>
    public sealed class OpenIdConnectAuthBeginRequest
    {
        /// <summary>
        /// The key of the provider to sign in with, as <see cref="IOpenIdConnectProviderResolver"/> knows it. Sealed in
        /// the <c>state</c>, so completion uses the same provider.
        /// </summary>
        public required string ProviderKey { get; init; }

        /// <summary>
        /// The redirect URI the provider sends the browser back to. Must be one of the provider's
        /// <see cref="OpenIdConnectProvider.RedirectUris"/>; the code is redeemed with this same URI, and completion
        /// refuses a response that arrived anywhere else.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// An opaque value the application chooses, returned by completion untouched so the application can check the
        /// completed flow is the one it began (for example "user X, signing record Y"). The package only carries it;
        /// comparing it is the application's job. It travels inside the encrypted <c>state</c>, so keep it short.
        /// </summary>
        public required string CallerBinding { get; init; }

        /// <summary>
        /// The <c>prompt</c> to send, or null to send none.
        /// </summary>
        public OpenIdConnectPrompt? Prompt { get; init; }

        /// <summary>
        /// The nonce to send, or null to have Begin generate a random one. Supply one to tie the sign-in to something of
        /// the application's (for example a hash of the record being signed); it comes back as
        /// <see cref="OpenIdConnectUserInfo.Nonce"/>.
        /// </summary>
        public string? Nonce { get; init; }

        /// <summary>
        /// The <c>login_hint</c> to send (the account's sign-in name), or null. A convenience that fills in the sign-in
        /// form; it proves nothing.
        /// </summary>
        public string? LoginHint { get; init; }

        /// <summary>
        /// The longest time since the person last authenticated at the provider that the flow accepts, or null for no
        /// limit. Begin sends it as <c>max_age</c> in whole seconds (at least one) and asks for <c>auth_time</c> as an
        /// essential claim; completion refuses a token without <c>auth_time</c>
        /// (<see cref="OpenIdConnectAuthFailureReason.MissingAuthTime"/>) or with one older than this
        /// (<see cref="OpenIdConnectAuthFailureReason.StaleAuthTime"/>).
        /// </summary>
        /// <remarks>
        /// The age is the registered <see cref="TimeProvider"/>'s now minus <c>auth_time</c>, with no clock skew
        /// allowance: a server clock ahead of the provider's makes every sign-in look that much older, so keep the clock
        /// synchronized, or allow for its drift in the age chosen. An <c>auth_time</c> ahead of the server clock counts
        /// as no age.
        /// </remarks>
        public TimeSpan? MaxAuthenticationAge { get; init; }

        /// <summary>
        /// The <c>acr</c> values the flow accepts, any one of them, or null to accept any. Begin asks for <c>acr</c> as an
        /// essential claim with these values; completion refuses a token without <c>acr</c>
        /// (<see cref="OpenIdConnectAuthFailureReason.MissingAcr"/>) or with another value
        /// (<see cref="OpenIdConnectAuthFailureReason.AcrNotAccepted"/>). Matching is exact.
        /// </summary>
        public IReadOnlyList<string>? AcceptedAcrValues { get; init; }

        /// <summary>
        /// The <c>amr</c> values the flow accepts, any one of them, or null to accept any. Begin asks for <c>amr</c> as an
        /// essential claim; completion refuses a token without <c>amr</c>
        /// (<see cref="OpenIdConnectAuthFailureReason.MissingAmr"/>) or whose <c>amr</c> holds none of these
        /// (<see cref="OpenIdConnectAuthFailureReason.AmrNotAccepted"/>). Matching is exact. When both this and
        /// <see cref="AcceptedAcrValues"/> are set, the token must meet both.
        /// </summary>
        public IReadOnlyList<string>? AcceptedAmrValues { get; init; }

        /// <summary>
        /// The account the person must sign in as, or null for anyone: for a re-check of a signed-in user, their linked
        /// identity. Its issuer must be one the provider can issue as (the provider's issuer, or with a
        /// <see cref="OpenIdConnectProvider.TenantClaim"/> the issuer of one tenant); Begin throws otherwise, since no
        /// sign-in could pass. Completion refuses any other account, issuer and subject compared together, as
        /// <see cref="OpenIdConnectAuthFailureReason.IdentityMismatch"/>.
        /// </summary>
        public OpenIdConnectIdentity? ExpectedIdentity { get; init; }
    }
}
