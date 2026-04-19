using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// In-memory <see cref="IAuthAuditLogger"/> that records every hook invocation so tests can
    /// assert which events fired and with what data. Used across multiple test classes.
    /// </summary>
    internal sealed class RecordingAuditLogger : IAuthAuditLogger
    {
        public List<(HttpContext HttpContext, LoginResult Result)> LoginCalls { get; } = new List<(HttpContext, LoginResult)>();
        public List<(HttpContext HttpContext, ApiKeyAuthResult Result)> ApiKeyCalls { get; } = new List<(HttpContext, ApiKeyAuthResult)>();
        public List<(HttpContext? HttpContext, RefreshResult Result)> RefreshCalls { get; } = new List<(HttpContext?, RefreshResult)>();
        public List<(HttpContext? HttpContext, LogoutResult Result)> LogoutCalls { get; } = new List<(HttpContext?, LogoutResult)>();
        public List<SessionRevocationResult> SessionsInvalidatedCalls { get; } = new List<SessionRevocationResult>();

        public Task OnLoginAsync(HttpContext httpContext, LoginResult result)
        {
            LoginCalls.Add((httpContext, result));
            return Task.CompletedTask;
        }

        public Task OnApiKeyAuthAsync(HttpContext httpContext, ApiKeyAuthResult result)
        {
            ApiKeyCalls.Add((httpContext, result));
            return Task.CompletedTask;
        }

        public Task OnRefreshAsync(HttpContext? httpContext, RefreshResult result)
        {
            RefreshCalls.Add((httpContext, result));
            return Task.CompletedTask;
        }

        public Task OnLogoutAsync(HttpContext? httpContext, LogoutResult result)
        {
            LogoutCalls.Add((httpContext, result));
            return Task.CompletedTask;
        }

        public Task OnSessionsInvalidatedAsync(SessionRevocationResult result)
        {
            SessionsInvalidatedCalls.Add(result);
            return Task.CompletedTask;
        }
    }
}
