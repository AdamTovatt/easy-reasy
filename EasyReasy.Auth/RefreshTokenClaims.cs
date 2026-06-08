using System.Security.Claims;
using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Serializes and deserializes the claims and roles that <see cref="IRefreshTokenService"/>
    /// persists alongside a refresh token.
    /// </summary>
    /// <remarks>
    /// <see cref="IRefreshTokenService.CreateRefreshTokenAsync"/> accepts the claims and roles as
    /// already-serialized JSON strings rather than as <see cref="Claim"/> collections. This type is
    /// the canonical way to produce those strings: a caller that wants to seed a refresh token with
    /// additional claims (for example a tenant or active-organization claim that must survive a
    /// refresh) serializes them here and passes the result to
    /// <see cref="IRefreshTokenService.CreateRefreshTokenAsync"/>. The refresh flow uses the exact
    /// same format internally, so a claim serialized here round-trips through a refresh unchanged
    /// (subject to any <see cref="IRefreshClaimsResolver"/> that re-evaluates it).
    /// </remarks>
    public static class RefreshTokenClaims
    {
        /// <summary>
        /// Serializes a collection of claims to the JSON string format expected by
        /// <see cref="IRefreshTokenService.CreateRefreshTokenAsync"/>.
        /// </summary>
        /// <param name="claims">The claims to serialize.</param>
        /// <returns>A JSON string representing the claims, or <c>null</c> if the collection is empty.</returns>
        public static string? SerializeClaims(IEnumerable<Claim> claims)
        {
            List<ClaimEntry> entries = claims.Select(claim => new ClaimEntry(claim.Type, claim.Value)).ToList();

            if (entries.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(entries, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Deserializes a JSON string produced by <see cref="SerializeClaims"/> back into claims.
        /// </summary>
        /// <param name="serializedClaims">The serialized claims, or <c>null</c>/empty.</param>
        /// <returns>The deserialized claims; an empty list when the input is <c>null</c> or empty.</returns>
        public static IReadOnlyList<Claim> DeserializeClaims(string? serializedClaims)
        {
            if (string.IsNullOrEmpty(serializedClaims))
            {
                return new List<Claim>();
            }

            List<ClaimEntry>? entries = JsonSerializer.Deserialize<List<ClaimEntry>>(serializedClaims, JsonSerializerSettings.CurrentOptions);

            if (entries == null)
            {
                return new List<Claim>();
            }

            return entries.Select(entry => new Claim(entry.Type, entry.Value)).ToList();
        }

        /// <summary>
        /// Serializes a collection of roles to the JSON string format expected by
        /// <see cref="IRefreshTokenService.CreateRefreshTokenAsync"/>.
        /// </summary>
        /// <param name="roles">The roles to serialize.</param>
        /// <returns>A JSON string representing the roles, or <c>null</c> if the collection is empty.</returns>
        public static string? SerializeRoles(IEnumerable<string> roles)
        {
            List<string> roleList = roles.ToList();

            if (roleList.Count == 0)
            {
                return null;
            }

            return JsonSerializer.Serialize(roleList, JsonSerializerSettings.CurrentOptions);
        }

        /// <summary>
        /// Deserializes a JSON string produced by <see cref="SerializeRoles"/> back into roles.
        /// </summary>
        /// <param name="serializedRoles">The serialized roles, or <c>null</c>/empty.</param>
        /// <returns>The deserialized roles; an empty list when the input is <c>null</c> or empty.</returns>
        public static IReadOnlyList<string> DeserializeRoles(string? serializedRoles)
        {
            if (string.IsNullOrEmpty(serializedRoles))
            {
                return new List<string>();
            }

            List<string>? roles = JsonSerializer.Deserialize<List<string>>(serializedRoles, JsonSerializerSettings.CurrentOptions);
            return roles ?? new List<string>();
        }

        /// <summary>
        /// Record used for JSON serialization of claims as <c>{ type, value }</c> pairs.
        /// </summary>
        private record ClaimEntry(string Type, string Value);
    }
}
