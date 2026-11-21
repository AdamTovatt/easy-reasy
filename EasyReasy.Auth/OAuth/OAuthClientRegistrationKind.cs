namespace EasyReasy.Auth
{
    /// <summary>
    /// How the authorization server knows a client.
    /// </summary>
    public enum OAuthClientRegistrationKind
    {
        /// <summary>
        /// Recorded in <see cref="IOAuthServerStore"/>, by dynamic client registration or by the application itself.
        /// Its name and URI are whatever the registration said: self-asserted, never an identity.
        /// </summary>
        Registered,

        /// <summary>
        /// Identified by an https URL whose client ID metadata document the server fetched. The URL's host is the one
        /// part of it the client cannot assert falsely; its name and URI are still self-asserted.
        /// </summary>
        MetadataDocument,
    }
}
