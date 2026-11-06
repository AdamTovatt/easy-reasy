using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Microsoft
{
    /// <summary>
    /// Everything a flow's Complete needs from its Begin, carried encrypted in the OAuth <c>state</c> rather than
    /// stored server-side. Property names on the wire are short because the state travels in a URL.
    /// </summary>
    internal sealed class MicrosoftAuthState
    {
        /// <summary>
        /// The random id naming this flow's binding cookie, so two flows in one browser each have their own.
        /// </summary>
        [JsonPropertyName("f")]
        public required string FlowId { get; init; }

        /// <summary>
        /// The base64url SHA-256 hash of the binding cookie's value.
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
        /// The tenant the flow requires, which is also its authority segment, or null for the
        /// <c>organizations</c> segment.
        /// </summary>
        [JsonPropertyName("t")]
        public Guid? RequiredTenantId { get; init; }

        /// <summary>
        /// The authentication context id the token's <c>acrs</c> must contain, or null.
        /// </summary>
        [JsonPropertyName("a")]
        public string? RequiredAuthenticationContextId { get; init; }

        /// <summary>
        /// The max authentication age in seconds, or null.
        /// </summary>
        [JsonPropertyName("m")]
        public long? MaxAuthenticationAgeSeconds { get; init; }

        /// <summary>
        /// The redirect URI the flow named, which the code is redeemed with.
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
    }
}
