namespace EasyReasy.Auth.OpenIdConnect.Tests
{
    /// <summary>
    /// One request a <see cref="FakeOpenIdConnectIssuer"/>'s token endpoint received: its form fields and its
    /// <c>Authorization</c> header, if any.
    /// </summary>
    internal sealed record FakeTokenRequest(Dictionary<string, string> Form, string? Authorization);
}
