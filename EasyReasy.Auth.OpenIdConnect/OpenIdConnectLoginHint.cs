namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The rule for a <c>login_hint</c> a sign-in endpoint takes from the browser, shared by every sign-in endpoint on the
    /// flow, a provider package's included. A hint only fills in the provider's account picker and proves nothing, so one
    /// that is unusable is dropped rather than refused.
    /// </summary>
    public static class OpenIdConnectLoginHint
    {
        /// <summary>
        /// The longest hint that is sent. A sign-in name is far shorter, so a longer one is not a hint.
        /// </summary>
        public const int MaxLength = 256;

        /// <summary>
        /// The hint to send for one the browser gave.
        /// </summary>
        /// <param name="loginHint">The hint from the browser, or null.</param>
        /// <returns>The hint, or null when it is null, blank or longer than <see cref="MaxLength"/>.</returns>
        public static string? FromBrowser(string? loginHint)
        {
            return string.IsNullOrWhiteSpace(loginHint) || loginHint.Length > MaxLength ? null : loginHint;
        }
    }
}
