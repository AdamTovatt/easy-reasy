using Microsoft.AspNetCore.Authorization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Requires an authenticated caller, as a bare <see cref="AuthorizeAttribute"/> does unless the endpoint has
    /// authorization of its own, and lets an OAuth connection's access token be that caller: the endpoint authenticates
    /// with exactly the app's JWT bearer and the connection's bearer scheme. No other endpoint runs that scheme, and the
    /// app's bearer cannot validate a connection's token, so a connection reaches only what the application opened to it,
    /// whatever the token's claims would satisfy. Put it on a controller or action, or add it to a minimal API endpoint or
    /// route group with
    /// <see cref="OAuthClientEndpointConventionBuilderExtensions.AllowOAuthClients{TBuilder}(TBuilder)"/>.
    /// </summary>
    /// <remarks>
    /// It is authorization data, as <see cref="AuthorizeAttribute"/> is, naming only the schemes: the endpoint's own
    /// authorization still applies to a connection as to any caller. Another sign-in scheme of the app's (a cookie
    /// scheme, say) is not run there unless the endpoint's own authorization names it too. Without the OAuth server
    /// registered, the connection scheme refuses every token, so only the app's own tokens get through.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AllowOAuthClientsAttribute : Attribute, IAuthorizeData
    {
        string? IAuthorizeData.Policy
        {
            get => null;
            set => throw new NotSupportedException($"{nameof(AllowOAuthClientsAttribute)} names no policy; add one with the endpoint's own authorization.");
        }

        string? IAuthorizeData.Roles
        {
            get => null;
            set => throw new NotSupportedException($"{nameof(AllowOAuthClientsAttribute)} names no roles; add them with the endpoint's own authorization.");
        }

        string? IAuthorizeData.AuthenticationSchemes
        {
            get => OAuthConnectionBearer.OpenedEndpointSchemes;
            set => throw new NotSupportedException($"{nameof(AllowOAuthClientsAttribute)}'s schemes are fixed.");
        }
    }
}
