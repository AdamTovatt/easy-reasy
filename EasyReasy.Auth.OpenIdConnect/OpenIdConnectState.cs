using System.Text.Json.Serialization;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Everything a flow's completion needs from its Begin, carried encrypted in the OAuth <c>state</c> rather than
    /// stored server-side. Property names on the wire are short because the state travels in a URL.
    /// </summary>
    internal sealed class OpenIdConnectState
    {
        /// <summary>
        /// The random id naming this flow's binding cookie, so two flows in one browser each have their own.
        /// </summary>
        [JsonPropertyName("f")]
        public required string FlowId { get; init; }

        /// <summary>
        /// The hash of the binding cookie's value, as <see cref="FlowBindingCookie.Issue"/> returned it.
        /// </summary>
        [JsonPropertyName("b")]
        public required string BindingCookieHash { get; init; }

        /// <summary>
        /// The PKCE code verifier the code is redeemed with.
        /// </summary>
        [JsonPropertyName("v")]
        public required string CodeVerifier { get; init; }

        /// <summary>
        /// The nonce the ID token must carry.
        /// </summary>
        [JsonPropertyName("n")]
        public required string Nonce { get; init; }

        /// <summary>
        /// The provider's key, which completion resolves again for the client secret.
        /// </summary>
        [JsonPropertyName("p")]
        public required string ProviderKey { get; init; }

        /// <summary>
        /// The provider's issuer when the flow began; completion refuses a provider that resolves to another.
        /// </summary>
        [JsonPropertyName("i")]
        public required string Issuer { get; init; }

        /// <summary>
        /// The provider's client id when the flow began; completion refuses a provider that resolves to another.
        /// </summary>
        [JsonPropertyName("k")]
        public required string ClientId { get; init; }

        /// <summary>
        /// The redirect URI the flow named, which the response must arrive at and the code is redeemed with.
        /// </summary>
        [JsonPropertyName("r")]
        public required string RedirectUri { get; init; }

        /// <summary>
        /// The caller binding from Begin.
        /// </summary>
        [JsonPropertyName("c")]
        public required string CallerBinding { get; init; }

        /// <summary>
        /// When the flow stops being completable, in Unix seconds.
        /// </summary>
        [JsonPropertyName("e")]
        public required long ExpiresAtUnixSeconds { get; init; }

        /// <summary>
        /// The max authentication age in seconds, or null.
        /// </summary>
        [JsonPropertyName("m")]
        public long? MaxAuthenticationAgeSeconds { get; init; }

        /// <summary>
        /// The accepted <c>acr</c> values, or null.
        /// </summary>
        [JsonPropertyName("a")]
        public IReadOnlyList<string>? AcceptedAcrValues { get; init; }

        /// <summary>
        /// The accepted <c>amr</c> values, or null.
        /// </summary>
        [JsonPropertyName("w")]
        public IReadOnlyList<string>? AcceptedAmrValues { get; init; }

        /// <summary>
        /// The required claim values, or null.
        /// </summary>
        [JsonPropertyName("q")]
        public IReadOnlyDictionary<string, string>? RequiredClaimValues { get; init; }

        /// <summary>
        /// The expected identity's issuer, or null. Begin checked it can be the provider's.
        /// </summary>
        [JsonPropertyName("x")]
        public string? ExpectedIssuer { get; init; }

        /// <summary>
        /// The expected identity's subject, or null; set exactly when <see cref="ExpectedIssuer"/> is.
        /// </summary>
        [JsonPropertyName("s")]
        public string? ExpectedSubject { get; init; }
    }
}
