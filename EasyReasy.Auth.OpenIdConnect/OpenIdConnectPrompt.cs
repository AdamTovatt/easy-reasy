namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The OpenID Connect <c>prompt</c> sent to the provider, which decides what the person is asked to do at sign-in.
    /// Whether a provider honours each value is the provider's property, so check its documentation.
    /// </summary>
    public enum OpenIdConnectPrompt
    {
        /// <summary>
        /// <c>prompt=login</c>: the person enters their credentials even with a session at the provider. It does not by
        /// itself prove a second factor ran at this sign-in.
        /// </summary>
        Login,

        /// <summary>
        /// <c>prompt=none</c>: no interaction. The provider answers with an error instead of a code when the person would
        /// have to do anything.
        /// </summary>
        None,

        /// <summary>
        /// <c>prompt=consent</c>: the consent dialog is shown after sign-in.
        /// </summary>
        Consent,

        /// <summary>
        /// <c>prompt=select_account</c>: the account picker is shown, even with one session at the provider.
        /// </summary>
        SelectAccount,
    }
}
