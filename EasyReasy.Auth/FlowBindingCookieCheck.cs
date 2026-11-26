namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of <see cref="FlowBindingCookie.Check"/>.
    /// </summary>
    public enum FlowBindingCookieCheck
    {
        /// <summary>
        /// The browser sent the flow's cookie with the value its state names.
        /// </summary>
        Matches,

        /// <summary>
        /// The browser sent no cookie for the flow: another browser is completing it, the cookie expired or was cleared by
        /// an earlier completion, or too many flows were begun after it.
        /// </summary>
        Missing,

        /// <summary>
        /// The browser sent a cookie for the flow with another value.
        /// </summary>
        Mismatch,
    }
}
