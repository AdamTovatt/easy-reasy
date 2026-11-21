namespace EasyReasy.Auth
{
    /// <summary>
    /// Lets an OAuth connection's access token reach an endpoint. At every other endpoint the bearer rejects one as
    /// invalid, so a connection reaches only what the application opened to it, whatever the token's claims would
    /// satisfy. Put it on a
    /// controller or action, or add it to a minimal API endpoint or route group with
    /// <see cref="OAuthClientEndpointConventionBuilderExtensions.AllowOAuthClients{TBuilder}(TBuilder)"/>.
    /// </summary>
    /// <remarks>
    /// It opens the endpoint to connections only; the endpoint's own authorization still applies to them as to any
    /// caller. A first-party token is unaffected by it.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = false, Inherited = true)]
    public sealed class AllowOAuthClientsAttribute : Attribute
    {
    }
}
