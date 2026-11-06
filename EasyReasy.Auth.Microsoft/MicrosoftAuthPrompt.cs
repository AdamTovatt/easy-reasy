namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// The OpenID Connect <c>prompt</c> sent to Microsoft, which decides what the user is asked to do at sign-in.
    /// </summary>
    public enum MicrosoftAuthPrompt
    {
        /// <summary>
        /// <c>prompt=login</c>: the user enters their credentials even with a session at Microsoft. It does not by
        /// itself prove a second factor ran at this sign-in; require an authentication context for that.
        /// </summary>
        Login,

        /// <summary>
        /// <c>prompt=none</c>: no interaction. Microsoft answers with an error instead of a code when the user
        /// would have to do anything.
        /// </summary>
        None,

        /// <summary>
        /// <c>prompt=consent</c>: the consent dialog is shown after sign-in.
        /// </summary>
        Consent,

        /// <summary>
        /// <c>prompt=select_account</c>: the account picker is shown, even with one session at Microsoft.
        /// </summary>
        SelectAccount,
    }
}
