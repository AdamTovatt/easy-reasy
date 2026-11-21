using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Records the OAuth server's audit hooks, and the first-party refresh hook, which a client's refresh must not reach.
    /// </summary>
    internal sealed class RecordingOAuthAuditLogger : IAuthAuditLogger
    {
        public List<OAuthClientRegistrationResult> Registrations { get; } = new List<OAuthClientRegistrationResult>();

        public List<OAuthAuthorizationResult> Authorizations { get; } = new List<OAuthAuthorizationResult>();

        public List<OAuthTokenResult> Tokens { get; } = new List<OAuthTokenResult>();

        public List<OAuthRevocationResult> Revocations { get; } = new List<OAuthRevocationResult>();

        public List<RefreshResult> FirstPartyRefreshes { get; } = new List<RefreshResult>();

        public Task OnOAuthClientRegistrationAsync(HttpContext httpContext, OAuthClientRegistrationResult result)
        {
            Registrations.Add(result);
            return Task.CompletedTask;
        }

        public Task OnOAuthAuthorizationAsync(HttpContext httpContext, OAuthAuthorizationResult result)
        {
            Authorizations.Add(result);
            return Task.CompletedTask;
        }

        public Task OnOAuthTokenAsync(HttpContext httpContext, OAuthTokenResult result)
        {
            Tokens.Add(result);
            return Task.CompletedTask;
        }

        public Task OnOAuthRevocationAsync(HttpContext httpContext, OAuthRevocationResult result)
        {
            Revocations.Add(result);
            return Task.CompletedTask;
        }

        public Task OnRefreshAsync(HttpContext? httpContext, RefreshResult result)
        {
            FirstPartyRefreshes.Add(result);
            return Task.CompletedTask;
        }
    }
}
