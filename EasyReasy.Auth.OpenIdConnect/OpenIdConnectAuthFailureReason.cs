namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Why an OpenID Connect sign-in was refused. Each member names one check, so an application or an audit record can
    /// tell refusals apart without parsing a message. Intended for logs and audit records, not for the browser.
    /// </summary>
    /// <remarks>
    /// The sign-in endpoint records the member's name as <see cref="ExternalAuthResult.FailureDetail"/>: a failure of the
    /// state, the binding cookie, the response, the provider, the code or the token as
    /// <see cref="ExternalAuthFailureReason.InvalidToken"/>, and a genuine identity refused by the flow's rules as
    /// <see cref="ExternalAuthFailureReason.Rejected"/>.
    /// <para>
    /// The member names are a stable contract: they are written into audit records that outlive any release, so a member
    /// is never renamed, and a new check gets a new member.
    /// </para>
    /// </remarks>
    public enum OpenIdConnectAuthFailureReason
    {
        /// <summary>
        /// The <c>state</c> is missing, not this server's, tampered with or otherwise unreadable.
        /// </summary>
        InvalidState,

        /// <summary>
        /// The <c>state</c> is genuine but older than <see cref="OpenIdConnectAuthOptions.StateLifetime"/>.
        /// </summary>
        ExpiredState,

        /// <summary>
        /// The browser completing the flow did not send the binding cookie the flow's Begin set: it is not the browser
        /// that began the flow, or the cookie has expired or been cleared (by its completion, or by the browser beginning
        /// more flows than it may have pending at once).
        /// </summary>
        BindingCookieMissing,

        /// <summary>
        /// The browser sent a binding cookie for the flow, but not the value the flow's Begin set.
        /// </summary>
        BindingCookieMismatch,

        /// <summary>
        /// The response arrived at another redirect URI than the flow named: a provider passing off a response to a
        /// flow begun with another provider (a mix-up), or a page posting to the wrong flow.
        /// </summary>
        RedirectUriMismatch,

        /// <summary>
        /// The response's <c>iss</c> parameter is not the issuer of the flow's provider (RFC 9207).
        /// </summary>
        IssuerParameterMismatch,

        /// <summary>
        /// The provider's discovery document says it sends the <c>iss</c> parameter, and the response has none
        /// (RFC 9207).
        /// </summary>
        IssuerParameterMissing,

        /// <summary>
        /// The flow's provider key no longer resolves to a provider.
        /// </summary>
        UnknownProvider,

        /// <summary>
        /// The flow's provider resolves to another issuer or client id than when the flow began.
        /// </summary>
        ProviderChanged,

        /// <summary>
        /// The code is missing, or the provider's token endpoint refused to redeem it with <c>invalid_grant</c> or
        /// <c>invalid_request</c> (already used, expired, or issued for another redirect URI or PKCE verifier), or
        /// answered without an ID token. A refusal of the client itself, such as <c>invalid_client</c>, is no refusal of
        /// the person: completion throws instead.
        /// </summary>
        CodeRedemptionFailed,

        /// <summary>
        /// The ID token is malformed, not yet valid, has no <c>exp</c> or no <c>sub</c>, or has an <c>auth_time</c> that is
        /// not a number.
        /// </summary>
        InvalidToken,

        /// <summary>
        /// The ID token's signature does not verify against the provider's published keys (including a key id in neither
        /// the cached nor a freshly fetched key set), or it is signed with an algorithm outside
        /// <see cref="OpenIdConnectProvider.SigningAlgorithms"/>.
        /// </summary>
        InvalidSignature,

        /// <summary>
        /// The ID token's audience does not hold the client id, or its <c>azp</c> is another client.
        /// </summary>
        InvalidAudience,

        /// <summary>
        /// The ID token's issuer is not the provider's.
        /// </summary>
        InvalidIssuer,

        /// <summary>
        /// The ID token has expired.
        /// </summary>
        TokenExpired,

        /// <summary>
        /// The ID token's <c>nonce</c> is missing or is not the one the flow's Begin sent.
        /// </summary>
        NonceMismatch,

        /// <summary>
        /// The flow named an expected identity, and the token is for another account.
        /// </summary>
        IdentityMismatch,

        /// <summary>
        /// The flow set a max authentication age, and the token carries no <c>auth_time</c>.
        /// </summary>
        MissingAuthTime,

        /// <summary>
        /// The flow set a max authentication age, and the token's <c>auth_time</c> is older than it.
        /// </summary>
        StaleAuthTime,

        /// <summary>
        /// The flow named accepted <c>acr</c> values, and the token carries no <c>acr</c>.
        /// </summary>
        MissingAcr,

        /// <summary>
        /// The flow named accepted <c>acr</c> values, and the token's <c>acr</c> is none of them.
        /// </summary>
        AcrNotAccepted,

        /// <summary>
        /// The flow named accepted <c>amr</c> values, and the token carries no <c>amr</c>.
        /// </summary>
        MissingAmr,

        /// <summary>
        /// The flow named accepted <c>amr</c> values, and the token's <c>amr</c> holds none of them.
        /// </summary>
        AmrNotAccepted,

        /// <summary>
        /// The flow named required claim values, and the token lacks one of those claims.
        /// </summary>
        MissingRequiredClaim,

        /// <summary>
        /// The flow named required claim values, and one of the token's claims neither equals its value nor, as an array,
        /// holds it.
        /// </summary>
        RequiredClaimMismatch,

        /// <summary>
        /// The <c>state</c> was begun with another caller binding than the completing endpoint expects: by another flow of
        /// the application (linking an account, a re-check at signing) with other rules, and posted to this one. Refused
        /// before the code is redeemed.
        /// </summary>
        CallerBindingMismatch,
    }
}
