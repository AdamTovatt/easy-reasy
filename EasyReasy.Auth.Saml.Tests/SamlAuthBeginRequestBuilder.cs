namespace EasyReasy.Auth.Saml.Tests
{
    /// <summary>
    /// The fields of a <see cref="SamlAuthBeginRequest"/> a test changes, with the values an application's own flow would
    /// send by default.
    /// </summary>
    internal sealed class SamlAuthBeginRequestBuilder
    {
        public const string DefaultCallerBinding = "link-account:user-7";

        public string IdentityProviderKey { get; set; } = SamlFlowHost.IdentityProviderKey;

        public string CallerBinding { get; set; } = DefaultCallerBinding;

        public string CompletionPath { get; set; } = "/settings/linked";

        public bool ForceAuthentication { get; set; }

        public TimeSpan? MaxAuthenticationAge { get; set; }

        public IReadOnlyList<string>? AcceptedAuthnContextClassRefs { get; set; }

        public SamlIdentity? ExpectedIdentity { get; set; }

        public SamlAuthBeginRequest Build()
        {
            return new SamlAuthBeginRequest
            {
                IdentityProviderKey = IdentityProviderKey,
                CallerBinding = CallerBinding,
                CompletionPath = CompletionPath,
                ForceAuthentication = ForceAuthentication,
                MaxAuthenticationAge = MaxAuthenticationAge,
                AcceptedAuthnContextClassRefs = AcceptedAuthnContextClassRefs,
                ExpectedIdentity = ExpectedIdentity,
            };
        }
    }
}
