using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Client metadata (RFC 7591 section 2) as a registration request or a client ID metadata document states it, read
    /// and checked by the one set of rules both share.
    /// </summary>
    internal sealed class OAuthClientMetadata
    {
        /// <summary>
        /// The longest client name accepted.
        /// </summary>
        public const int MaxClientNameLength = 200;

        /// <summary>
        /// The most redirect URIs one client may register.
        /// </summary>
        public const int MaxRedirectUris = 10;

        private OAuthClientMetadata(IReadOnlyList<string> redirectUris, string? clientName, string? clientUri, string? authenticationMethodName)
        {
            RedirectUris = redirectUris;
            ClientName = clientName;
            ClientUri = clientUri;
            AuthenticationMethodName = authenticationMethodName;
        }

        /// <summary>
        /// The redirect URIs, each allowed by <see cref="OAuthRedirectUri.IsAllowed"/>.
        /// </summary>
        public IReadOnlyList<string> RedirectUris { get; }

        /// <summary>
        /// The client name, or null.
        /// </summary>
        public string? ClientName { get; }

        /// <summary>
        /// The client's home page, or null.
        /// </summary>
        public string? ClientUri { get; }

        /// <summary>
        /// The <c>token_endpoint_auth_method</c> as given, or null when absent; the caller applies its own default and
        /// its own allowed set.
        /// </summary>
        public string? AuthenticationMethodName { get; }

        /// <summary>
        /// Reads client metadata.
        /// </summary>
        /// <param name="document">The JSON document's root.</param>
        /// <param name="metadata">The metadata, when it is acceptable.</param>
        /// <param name="error">The RFC 7591 error code (<c>invalid_redirect_uri</c> or <c>invalid_client_metadata</c>) and
        /// description, when it is not.</param>
        /// <returns>Whether the metadata is acceptable.</returns>
        public static bool TryRead(JsonElement document, [NotNullWhen(true)] out OAuthClientMetadata? metadata, [NotNullWhen(false)] out OAuthError? error)
        {
            metadata = null;

            if (document.ValueKind != JsonValueKind.Object)
            {
                error = Invalid("The client metadata must be a JSON object.");
                return false;
            }

            if (!document.TryGetProperty("redirect_uris", out JsonElement redirectUrisElement)
                || redirectUrisElement.ValueKind != JsonValueKind.Array
                || redirectUrisElement.GetArrayLength() == 0)
            {
                error = new OAuthError(OAuthError.InvalidRedirectUri, "redirect_uris must be a non-empty array.");
                return false;
            }

            if (redirectUrisElement.GetArrayLength() > MaxRedirectUris)
            {
                error = new OAuthError(OAuthError.InvalidRedirectUri, $"At most {MaxRedirectUris} redirect URIs may be registered.");
                return false;
            }

            List<string> redirectUris = new List<string>();
            foreach (JsonElement redirectUriElement in redirectUrisElement.EnumerateArray())
            {
                string? redirectUri = redirectUriElement.ValueKind == JsonValueKind.String ? redirectUriElement.GetString() : null;
                if (!OAuthRedirectUri.IsAllowed(redirectUri))
                {
                    error = new OAuthError(
                        OAuthError.InvalidRedirectUri,
                        "Each redirect URI must be an absolute https URL, an http URL on a loopback host, or an app's private-use scheme (never javascript, data, file and the like), without a fragment or userinfo.");
                    return false;
                }

                redirectUris.Add(redirectUri!);
            }

            if (!TryReadOptionalString(document, "client_name", out string? clientName, out error))
            {
                return false;
            }

            if (clientName != null && clientName.Length > MaxClientNameLength)
            {
                error = Invalid($"client_name must be at most {MaxClientNameLength} characters.");
                return false;
            }

            if (!TryReadOptionalString(document, "client_uri", out string? clientUri, out error))
            {
                return false;
            }

            if (clientUri != null && (!OAuthUrl.TryParseSecureContextUrl(clientUri, out _) || clientUri.Length > OAuthRedirectUri.MaxLength))
            {
                error = Invalid("client_uri must be an absolute https URL.");
                return false;
            }

            if (!TryReadOptionalString(document, "token_endpoint_auth_method", out string? authenticationMethodName, out error))
            {
                return false;
            }

            if (!RequireIfPresent(document, "grant_types", "authorization_code", out error)
                || !RequireIfPresent(document, "response_types", "code", out error))
            {
                return false;
            }

            metadata = new OAuthClientMetadata(redirectUris, clientName, clientUri, authenticationMethodName);
            error = null;
            return true;
        }

        private static bool TryReadOptionalString(JsonElement document, string propertyName, out string? value, [NotNullWhen(false)] out OAuthError? error)
        {
            value = null;
            error = null;
            if (!document.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (element.ValueKind != JsonValueKind.String)
            {
                error = Invalid($"{propertyName} must be a string.");
                return false;
            }

            value = element.GetString();
            return true;
        }

        // A list the client may send, which must then include what this server needs. Other values are filtered out, as
        // the metadata document draft asks and RFC 7591 allows (the server registers what it supports). Absent means RFC
        // 7591's default, which is what this server supports.
        private static bool RequireIfPresent(JsonElement document, string propertyName, string required, [NotNullWhen(false)] out OAuthError? error)
        {
            error = null;
            if (!document.TryGetProperty(propertyName, out JsonElement element) || element.ValueKind == JsonValueKind.Null)
            {
                return true;
            }

            if (element.ValueKind != JsonValueKind.Array)
            {
                error = Invalid($"{propertyName} must be an array.");
                return false;
            }

            bool hasRequired = false;
            foreach (JsonElement item in element.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    error = Invalid($"{propertyName} must be an array of strings.");
                    return false;
                }

                hasRequired |= item.GetString() == required;
            }

            if (!hasRequired)
            {
                error = Invalid($"{propertyName} must include {required}.");
                return false;
            }

            return true;
        }

        private static OAuthError Invalid(string description)
        {
            return new OAuthError(OAuthError.InvalidClientMetadata, description);
        }
    }
}
