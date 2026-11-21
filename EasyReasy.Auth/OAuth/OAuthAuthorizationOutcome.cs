namespace EasyReasy.Auth
{
    /// <summary>
    /// How a person's decision on a pending authorization request ended.
    /// </summary>
    public enum OAuthAuthorizationOutcome
    {
        /// <summary>
        /// The handler approved and recorded the grant, and a code was issued.
        /// </summary>
        Approved,

        /// <summary>
        /// The handler refused this approval; the request stays pending.
        /// </summary>
        Refused,

        /// <summary>
        /// The person denied the request, and the client is told <c>access_denied</c>.
        /// </summary>
        Denied,
    }
}
