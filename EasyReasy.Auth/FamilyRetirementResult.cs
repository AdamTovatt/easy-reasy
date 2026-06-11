namespace EasyReasy.Auth
{
    /// <summary>
    /// Represents a request to retire a single refresh token family because a re-issue superseded it.
    /// Surfaced to <see cref="IAuthAuditLogger.OnSessionSupersededAsync"/> so consumers can record the
    /// supersession as its own audit event, distinct from a real logout or bulk revocation.
    /// </summary>
    /// <remarks>
    /// Deliberately distinct from <see cref="SessionRevocationResult"/>, which carries a count of families
    /// revoked by a bulk or single-session operation: this names the one family that was retired. Because
    /// <see cref="IRefreshTokenStore.InvalidateFamilyAsync"/> reports neither whether the family existed nor
    /// which subject it belonged to, the record means "a retire was requested for this family";
    /// <see cref="Subject"/> is supplied by the caller (from the access token) purely for the audit row.
    /// </remarks>
    public sealed class FamilyRetirementResult
    {
        /// <summary>
        /// The identifier of the refresh token family that was retired.
        /// </summary>
        public required string FamilyId { get; init; }

        /// <summary>
        /// The subject (user identifier) the retired family belonged to, when the caller supplied it; otherwise null.
        /// </summary>
        public string? Subject { get; init; }
    }
}
