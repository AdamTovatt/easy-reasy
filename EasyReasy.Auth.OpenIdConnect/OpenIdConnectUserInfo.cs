using System.Security.Claims;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// The identity in a validated ID token.
    /// </summary>
    /// <remarks>
    /// Link accounts by <see cref="Identity"/>, the issuer and <c>sub</c> together. Never use <see cref="Email"/> as an
    /// identifier, verified or not: the provider may let it change or be reassigned, and another provider can assert the
    /// same one.
    /// </remarks>
    public sealed class OpenIdConnectUserInfo
    {
        /// <summary>
        /// The key of the provider the person signed in with.
        /// </summary>
        public required string ProviderKey { get; init; }

        /// <summary>
        /// The account: the token's issuer and <c>sub</c>.
        /// </summary>
        public required OpenIdConnectIdentity Identity { get; init; }

        /// <summary>
        /// When the person last authenticated at the provider (the <c>auth_time</c> claim), or null when the token does
        /// not carry it.
        /// </summary>
        public DateTimeOffset? AuthenticationTime { get; init; }

        /// <summary>
        /// The nonce the token was issued for: the caller's own when Begin was given one, otherwise the random one Begin
        /// generated.
        /// </summary>
        public required string Nonce { get; init; }

        /// <summary>
        /// The authentication context class the provider asserts (the <c>acr</c> claim), or null. What the provider
        /// asserts, not proof that a factor ran for this act.
        /// </summary>
        public string? AuthenticationContextClass { get; init; }

        /// <summary>
        /// The authentication methods the provider asserts (the <c>amr</c> claim) as given, possibly empty. What the
        /// provider asserts, not proof that a factor ran for this act.
        /// </summary>
        public required IReadOnlyList<string> AuthenticationMethods { get; init; }

        /// <summary>
        /// The email address (the <c>email</c> claim), for display and contact only, or null.
        /// </summary>
        public string? Email { get; init; }

        /// <summary>
        /// Whether the provider says it verified <see cref="Email"/> (the <c>email_verified</c> claim); false when absent.
        /// Informational: it says the address was the person's when checked, not that it identifies them.
        /// </summary>
        public bool EmailVerified { get; init; }

        /// <summary>
        /// The display name (the <c>name</c> claim), or null.
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// Every claim in the validated token, array claims flattened to one claim per value. Informational, for claims
        /// this type does not name.
        /// </summary>
        public required IReadOnlyList<Claim> Claims { get; init; }

        /// <summary>
        /// Every value of one claim: one for a single value, one per element for an array, none when the token lacks it.
        /// </summary>
        /// <param name="type">The claim's name, such as <c>groups</c>.</param>
        /// <returns>The values, in the token's order.</returns>
        public IReadOnlyList<string> GetClaimValues(string type)
        {
            return Claims.Where(claim => claim.Type == type).Select(claim => claim.Value).ToList();
        }

        /// <summary>
        /// The first value of one claim, or null when the token lacks it.
        /// </summary>
        /// <param name="type">The claim's name, such as <c>preferred_username</c>.</param>
        /// <returns>The value, or null.</returns>
        public string? FindClaimValue(string type)
        {
            return Claims.FirstOrDefault(claim => claim.Type == type)?.Value;
        }
    }
}
