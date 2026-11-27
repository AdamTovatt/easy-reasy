namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Why a SAML sign-in was refused.
    /// </summary>
    public enum SamlAuthFailureReason
    {
        /// <summary>
        /// The completion code is unknown, already redeemed or expired.
        /// </summary>
        UnknownCompletionCode,

        /// <summary>
        /// The browser completing the sign-in sent no binding cookie for it: it is not the browser that began it, or the
        /// cookie expired.
        /// </summary>
        BindingCookieMissing,

        /// <summary>
        /// The browser's binding cookie for the sign-in does not match the one Begin set.
        /// </summary>
        BindingCookieMismatch,

        /// <summary>
        /// The identity provider the sign-in was begun with resolves to none at the assertion consumer service.
        /// </summary>
        UnknownIdentityProvider,

        /// <summary>
        /// The identity provider's entity id changed between Begin and the assertion consumer service.
        /// </summary>
        IdentityProviderChanged,

        /// <summary>
        /// The response failed a check: its XML, signature, algorithms, encryption, issuer, audience, destination,
        /// recipient, the request it answers, or its times.
        /// </summary>
        InvalidResponse,

        /// <summary>
        /// The completed sign-in was begun with another caller binding than the one the completing endpoint passed to
        /// <see cref="ISamlAuthService.CompleteAsync"/>, so its rules were another endpoint's.
        /// </summary>
        CallerBindingMismatch,

        /// <summary>
        /// The verified assertion carries no identity the identity provider's <see cref="SamlIdentitySource"/> accepts: no
        /// persistent NameID or one qualified for another party, or a missing or multi-valued identity attribute.
        /// </summary>
        MissingIdentity,

        /// <summary>
        /// The verified assertion names another account than <see cref="SamlAuthBeginRequest.ExpectedIdentity"/>.
        /// </summary>
        IdentityMismatch,

        /// <summary>
        /// The assertion's <c>AuthnInstant</c> is older than <see cref="SamlAuthBeginRequest.MaxAuthenticationAge"/>.
        /// </summary>
        StaleAuthentication,

        /// <summary>
        /// <see cref="SamlAuthBeginRequest.AcceptedAuthnContextClassRefs"/> was set and the assertion names no
        /// <c>AuthnContextClassRef</c>.
        /// </summary>
        MissingAuthnContextClass,

        /// <summary>
        /// The assertion's <c>AuthnContextClassRef</c> is not one of
        /// <see cref="SamlAuthBeginRequest.AcceptedAuthnContextClassRefs"/>.
        /// </summary>
        AuthnContextClassNotAccepted,
    }
}
