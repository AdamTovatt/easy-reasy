namespace EasyReasy.Auth
{
    /// <summary>
    /// The rules for a client id written as a client ID metadata document URL
    /// (draft-ietf-oauth-client-id-metadata-document-03): which ids are such URLs, and which of those the draft allows.
    /// </summary>
    internal static class OAuthClientIdUrl
    {
        /// <summary>
        /// Whether a client id is written as a client ID metadata document URL (starts with <c>https://</c>), so it is
        /// looked up by fetching, never in the store. A registered client's id is random base64url and never has a colon.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <returns>Whether it names a metadata document.</returns>
        public static bool IsMetadataDocument(string clientId)
        {
            return clientId.StartsWith("https://", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Whether a client id URL meets the rules the draft sets for one: https, no userinfo, a path other than
        /// <c>/</c>, no dot segments, no fragment.
        /// </summary>
        /// <param name="clientId">The client id.</param>
        /// <returns>Whether it is allowed.</returns>
        public static bool IsValid(string clientId)
        {
            if (clientId.Length > OAuthRedirectUri.MaxLength
                || OAuthUrl.HasFragment(clientId)
                || !Uri.TryCreate(clientId, UriKind.Absolute, out Uri? parsed)
                || parsed.Scheme != Uri.UriSchemeHttps
                || !string.IsNullOrEmpty(parsed.UserInfo)
                || parsed.AbsolutePath == "/")
            {
                return false;
            }

            // Uri removes dot segments while parsing, so they are looked for in what the client sent.
            string afterScheme = clientId.Substring("https://".Length);
            int pathStart = afterScheme.IndexOf('/');
            string path = pathStart < 0 ? "" : afterScheme.Substring(pathStart);
            int queryStart = path.IndexOf('?');
            if (queryStart >= 0)
            {
                path = path.Substring(0, queryStart);
            }

            foreach (string segment in path.Split('/'))
            {
                string decoded = segment.Replace("%2e", ".", StringComparison.OrdinalIgnoreCase);
                if (decoded == "." || decoded == "..")
                {
                    return false;
                }
            }

            return true;
        }
    }
}
