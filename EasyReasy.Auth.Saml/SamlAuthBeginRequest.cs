namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What a sign-in asks of the identity provider, and what its completion will check: the input to
    /// <see cref="ISamlAuthService.BeginAsync"/>.
    /// </summary>
    /// <remarks>
    /// Every value here except <see cref="IdentityProviderKey"/> decides what the resulting sign-in proves, so build it
    /// from the application's own rules for the flow, never from what the browser sends.
    /// <para>
    /// A re-check at a sensitive moment is <see cref="ForceAuthentication"/> with a <see cref="MaxAuthenticationAge"/>,
    /// and optionally <see cref="AcceptedAuthnContextClassRefs"/>. These are the identity provider's assertions: whether it
    /// honours <c>ForceAuthn</c>, and whether a class it reports proves a second factor ran for this act, is the identity
    /// provider's, and the package checks neither for any provider.
    /// </para>
    /// </remarks>
    public sealed class SamlAuthBeginRequest
    {
        /// <summary>
        /// The key of the identity provider to sign in with, as <see cref="ISamlIdentityProviderResolver"/> knows it.
        /// Sealed with the sign-in, so the response must come from the same provider.
        /// </summary>
        public required string IdentityProviderKey { get; init; }

        /// <summary>
        /// An opaque value the application chooses, naming what the sign-in is for (for example "user X, signing record
        /// Y"). It is sealed with the sign-in, never sent to the identity provider, and completion refuses the code unless
        /// the completing endpoint passes the same value to <see cref="ISamlAuthService.CompleteAsync"/>, so one endpoint's
        /// code never satisfies another's rules.
        /// </summary>
        public required string CallerBinding { get; init; }

        /// <summary>
        /// The application page the assertion consumer service redirects the browser to with the completion code, as a
        /// path on this site: it starts with one <c>/</c>, never <c>//</c> or a backslash, so the redirect cannot leave the
        /// site, and holds no <c>#</c>, since the code is appended as a query parameter that a fragment would swallow. It
        /// may carry a query string of its own, which the code joins. The page posts the code to the completing endpoint, same-site, which is what sends the browser binding
        /// cookie a cross-site post from the identity provider never carries.
        /// </summary>
        public required string CompletionPath { get; init; }

        /// <summary>
        /// Whether to send <c>ForceAuthn</c>, asking the identity provider to authenticate the person again rather than
        /// reuse its session. Pair it with <see cref="MaxAuthenticationAge"/>, which checks the answer.
        /// </summary>
        public bool ForceAuthentication { get; init; }

        /// <summary>
        /// The longest time since the person authenticated at the identity provider (the assertion's
        /// <c>AuthnInstant</c>) that the sign-in accepts, or null for no limit. Completion refuses an older one as
        /// <see cref="SamlAuthFailureReason.StaleAuthentication"/>. Measured on the registered
        /// <see cref="TimeProvider"/> with no clock skew allowance; an <c>AuthnInstant</c> ahead of the server clock
        /// counts as no age.
        /// </summary>
        public TimeSpan? MaxAuthenticationAge { get; init; }

        /// <summary>
        /// The authentication context classes the sign-in accepts, any one of them, or null to accept any. Begin sends them
        /// as an exact <c>RequestedAuthnContext</c>; completion refuses an assertion without an
        /// <c>AuthnContextClassRef</c> (<see cref="SamlAuthFailureReason.MissingAuthnContextClass"/>) or with another
        /// (<see cref="SamlAuthFailureReason.AuthnContextClassNotAccepted"/>). Matching is exact.
        /// </summary>
        public IReadOnlyList<string>? AcceptedAuthnContextClassRefs { get; init; }

        /// <summary>
        /// The account the person must sign in as, or null for anyone: for a re-check of a signed-in user, their linked
        /// identity. Its key must be <see cref="IdentityProviderKey"/> and its entity id the resolved identity provider's;
        /// Begin throws otherwise, since no sign-in could pass. Completion refuses any other account as
        /// <see cref="SamlAuthFailureReason.IdentityMismatch"/>.
        /// </summary>
        public SamlIdentity? ExpectedIdentity { get; init; }

        /// <summary>
        /// Every problem with the request that is knowable without the identity provider.
        /// </summary>
        internal IReadOnlyList<string> FindProblems()
        {
            List<string> problems = new List<string>();

            if (string.IsNullOrWhiteSpace(IdentityProviderKey))
            {
                problems.Add($"{nameof(IdentityProviderKey)} is required.");
            }

            if (CallerBinding == null)
            {
                problems.Add($"{nameof(CallerBinding)} must not be null.");
            }

            if (!IsSitePath(CompletionPath))
            {
                problems.Add($"{nameof(CompletionPath)} must be a path on this site, starting with one '/', with no '#'; got '{CompletionPath}'.");
            }

            if (ExpectedIdentity is SamlIdentity expected && !string.Equals(expected.IdentityProviderKey, IdentityProviderKey, StringComparison.Ordinal))
            {
                problems.Add($"{nameof(ExpectedIdentity)} is through the identity provider key '{expected.IdentityProviderKey}', not '{IdentityProviderKey}', so no sign-in could pass.");
            }

            if (MaxAuthenticationAge is TimeSpan maxAge && maxAge <= TimeSpan.Zero)
            {
                problems.Add($"{nameof(MaxAuthenticationAge)} must be positive; got {maxAge}.");
            }

            if (AcceptedAuthnContextClassRefs != null && (AcceptedAuthnContextClassRefs.Count == 0 || AcceptedAuthnContextClassRefs.Any(string.IsNullOrWhiteSpace)))
            {
                problems.Add($"{nameof(AcceptedAuthnContextClassRefs)} must be null or hold only non-blank values.");
            }

            return problems;
        }

        private static bool IsSitePath(string? path)
        {
            return !string.IsNullOrEmpty(path)
                && path[0] == '/'
                && (path.Length == 1 || (path[1] != '/' && path[1] != '\\'))
                && !path.Contains('\\')
                && !path.Contains('#')
                && !path.Any(char.IsControl);
        }
    }
}
