using Microsoft.AspNetCore.Http;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Authenticates the client calling the token or revocation endpoint (RFC 6749 section 2.3) by the method it
    /// registered: a public client by its <c>client_id</c> alone, a confidential one by its secret in a Basic header or the
    /// form body.
    /// </summary>
    internal sealed class OAuthClientAuthenticator
    {
        private readonly OAuthServerSettings _settings;
        private readonly IOAuthServerStore _store;

        /// <summary>
        /// Initializes the authenticator.
        /// </summary>
        /// <param name="settings">The server's settings.</param>
        /// <param name="store">The application's store.</param>
        public OAuthClientAuthenticator(OAuthServerSettings settings, IOAuthServerStore store)
        {
            _settings = settings;
            _store = store;
        }

        /// <summary>
        /// Reads a token or revocation request's form and authenticates its client: the opening both endpoints share.
        /// </summary>
        /// <param name="httpContext">The request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The form and the client's authentication, or the <c>invalid_request</c> for a body that is not one form.</returns>
        public async Task<OAuthClientRequest> ReadRequestAsync(HttpContext httpContext, CancellationToken cancellationToken)
        {
            if (!httpContext.Request.HasFormContentType)
            {
                return OAuthClientRequest.Malformed("The body must be application/x-www-form-urlencoded.");
            }

            OAuthParameters form = new OAuthParameters(await httpContext.Request.ReadFormAsync(cancellationToken));
            string? repeated = form.FirstRepeated;
            if (repeated != null)
            {
                return OAuthClientRequest.Malformed($"The parameter '{repeated}' was sent more than once.");
            }

            return OAuthClientRequest.WellFormed(form, await AuthenticateAsync(httpContext, form, cancellationToken));
        }

        /// <summary>
        /// Authenticates the calling client.
        /// </summary>
        /// <param name="httpContext">The request, for its <c>Authorization</c> header.</param>
        /// <param name="form">The form parameters.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The authenticated client id, or the refusal.</returns>
        private async Task<OAuthClientAuthentication> AuthenticateAsync(HttpContext httpContext, OAuthParameters form, CancellationToken cancellationToken)
        {
            string? formClientId = form.Get("client_id");
            string? formSecret = form.Get("client_secret");
            string? authorization = httpContext.Request.Headers.Authorization.Count == 1 ? httpContext.Request.Headers.Authorization[0] : null;
            bool usedBasic = authorization != null && authorization.StartsWith("Basic ", StringComparison.OrdinalIgnoreCase);

            string? clientId = formClientId;
            string? basicSecret = null;
            if (usedBasic)
            {
                if (formSecret != null)
                {
                    return OAuthClientAuthentication.Failed(formClientId, usedBasic, "The client authenticated with more than one method.");
                }

                if (!TryReadBasic(authorization!, out string? basicClientId, out basicSecret))
                {
                    return OAuthClientAuthentication.Failed(formClientId, usedBasic, "The Basic authorization header is malformed.");
                }

                if (formClientId != null && !string.Equals(formClientId, basicClientId, StringComparison.Ordinal))
                {
                    return OAuthClientAuthentication.Failed(formClientId, usedBasic, "The client_id differs from the Basic authorization header's.");
                }

                clientId = basicClientId;
            }

            if (clientId == null)
            {
                return OAuthClientAuthentication.Failed(null, usedBasic, "The request names no client.");
            }

            // A metadata document client is public by the draft's rules, and was checked at the authorization endpoint;
            // its codes and refresh tokens are bound to its id, so it needs no lookup here.
            if (OAuthClientIdUrl.IsMetadataDocument(clientId))
            {
                if (!_settings.AllowClientIdMetadataDocuments || usedBasic || formSecret != null)
                {
                    return OAuthClientAuthentication.Failed(clientId, usedBasic, "A client ID metadata document client is public and sends no secret.");
                }

                return OAuthClientAuthentication.Succeeded(clientId);
            }

            OAuthClient? client = await _store.GetClientAsync(clientId, cancellationToken);
            if (client == null)
            {
                return OAuthClientAuthentication.Failed(clientId, usedBasic, "The client is unknown.");
            }

            if (client.AuthenticationMethod == OAuthClientAuthenticationMethod.None)
            {
                if (usedBasic || formSecret != null)
                {
                    return OAuthClientAuthentication.Failed(clientId, usedBasic, "The client registered as public and sends no secret.");
                }

                return OAuthClientAuthentication.Succeeded(clientId);
            }

            // Only the method the client registered counts: a secret sent the other way is no authentication.
            string? presentedSecret = client.AuthenticationMethod switch
            {
                OAuthClientAuthenticationMethod.ClientSecretBasic when usedBasic => basicSecret,
                OAuthClientAuthenticationMethod.ClientSecretPost when !usedBasic => formSecret,
                _ => null,
            };

            if (presentedSecret == null || client.ClientSecretHash == null || !SecureToken.MatchesHash(presentedSecret, client.ClientSecretHash))
            {
                return OAuthClientAuthentication.Failed(clientId, usedBasic, "The client's authentication failed.");
            }

            return OAuthClientAuthentication.Succeeded(clientId);
        }

        // RFC 6749 section 2.3.1: the id and secret are form-urlencoded before they are joined and base64-encoded.
        private static bool TryReadBasic(string authorization, out string? clientId, out string? secret)
        {
            clientId = null;
            secret = null;
            try
            {
                string decoded = Encoding.UTF8.GetString(Convert.FromBase64String(authorization.Substring("Basic ".Length).Trim()));
                int separator = decoded.IndexOf(':');
                if (separator <= 0)
                {
                    return false;
                }

                clientId = Uri.UnescapeDataString(decoded.Substring(0, separator).Replace('+', ' '));
                secret = Uri.UnescapeDataString(decoded.Substring(separator + 1).Replace('+', ' '));
                return secret.Length > 0;
            }
            catch (FormatException)
            {
                return false;
            }
        }
    }
}
