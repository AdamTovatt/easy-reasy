namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The security-relevant inputs of a sign-in begun through <c>POST /api/auth/microsoft/begin</c>, decided per
    /// request by <see cref="IMicrosoftAuthHandler.GetSignInSettingsAsync"/>. The browser's body may carry a login hint
    /// and nothing else, so none of these can be chosen by the caller.
    /// </summary>
    public sealed class MicrosoftSignInSettings
    {
        /// <summary>
        /// The sign-in page's redirect URI. Must be one of <see cref="MicrosoftAuthOptions.RedirectUris"/>.
        /// </summary>
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The tenant the account must belong to, or null for any work or school tenant. See
        /// <see cref="MicrosoftAuthBeginRequest.TenantId"/>.
        /// </summary>
        public Guid? TenantId { get; init; }

        /// <summary>
        /// The <c>prompt</c> to send, or null to send none.
        /// </summary>
        public MicrosoftAuthPrompt? Prompt { get; init; }

        /// <summary>
        /// A Conditional Access authentication context id the sign-in must satisfy, or null. See
        /// <see cref="MicrosoftAuthBeginRequest.RequiredAuthenticationContextId"/>.
        /// </summary>
        public string? RequiredAuthenticationContextId { get; init; }

        /// <summary>
        /// The longest time since the user last entered credentials that the sign-in accepts, or null for no limit.
        /// See <see cref="MicrosoftAuthBeginRequest.MaxAuthenticationAge"/>.
        /// </summary>
        public TimeSpan? MaxAuthenticationAge { get; init; }
    }
}
