namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// What a flow asks of Microsoft, and what its Complete will check: the input to
    /// <see cref="IMicrosoftAuthService.Begin"/>.
    /// </summary>
    /// <remarks>
    /// Every value here except <see cref="LoginHint"/> decides what the resulting sign-in proves, so build it from
    /// the application's own rules for the flow, never from what the browser sends.
    /// </remarks>
    public sealed class MicrosoftAuthBeginRequest
    {
        /// <summary>
        /// The redirect URI Microsoft sends the browser back to. Must be one of
        /// <see cref="MicrosoftAuthOptions.RedirectUris"/>; the code is later redeemed with this same URI.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// An opaque value the application chooses, returned by Complete untouched so the application can check the
        /// completed flow is the one it began (for example "user X, signing record Y"). The package only carries it;
        /// comparing it is the application's job. It travels inside the encrypted <c>state</c>, so keep it short.
        /// </summary>
        public required string CallerBinding { get; init; }

        /// <summary>
        /// The tenant the account must belong to, used as the authority segment, or null for any work or school
        /// tenant (the <c>organizations</c> segment). Complete refuses a token from another tenant. It must not be the
        /// personal accounts tenant, whose accounts are always refused.
        /// </summary>
        public Guid? TenantId { get; init; }

        /// <summary>
        /// The <c>prompt</c> to send, or null to send none.
        /// </summary>
        public MicrosoftAuthPrompt? Prompt { get; init; }

        /// <summary>
        /// The nonce to send, or null to have Begin generate a random one. Supply one to tie the sign-in to
        /// something of the application's (for example a hash of the record being signed); it comes back as
        /// <see cref="MicrosoftUserInfo.Nonce"/>.
        /// </summary>
        public string? Nonce { get; init; }

        /// <summary>
        /// The <c>login_hint</c> to send (the account's sign-in name), or null. A convenience that fills in the
        /// account picker; it proves nothing.
        /// </summary>
        public string? LoginHint { get; init; }

        /// <summary>
        /// A Conditional Access authentication context id (for example <c>c1</c>) the sign-in must satisfy, or null.
        /// Begin asks for it through the <c>claims</c> parameter, and Complete refuses a token whose <c>acrs</c> does
        /// not contain it. This is how a flow proves a second factor ran at this sign-in; see the README for the
        /// tenant setup it relies on.
        /// </summary>
        public string? RequiredAuthenticationContextId { get; init; }

        /// <summary>
        /// The longest time since the user last entered credentials that the flow accepts, or null for no limit.
        /// Begin sends it as <c>max_age</c> in whole seconds (at least one), and Complete refuses a token without
        /// <c>auth_time</c> or with one older than this.
        /// </summary>
        public TimeSpan? MaxAuthenticationAge { get; init; }
    }
}
