namespace EasyReasy.Auth
{
    /// <summary>
    /// The result of creating a new refresh token via
    /// <see cref="IRefreshTokenService.CreateRefreshTokenWithFamilyAsync"/>, exposing both the raw token
    /// string to return to the client and the generated family identifier.
    /// </summary>
    /// <remarks>
    /// The <see cref="FamilyId"/> lets a caller that re-mints a token pair for an already-authenticated
    /// subject (for example an active-organization switch endpoint) later retire that exact family via
    /// <see cref="IRefreshTokenService.RetireFamilyAsync"/>, or seed the <c>family_id</c> claim onto the
    /// very first (login) access token itself.
    /// </remarks>
    public sealed class RefreshTokenCreationResult
    {
        /// <summary>
        /// The raw refresh token string to return to the client.
        /// </summary>
        public required string RawToken { get; init; }

        /// <summary>
        /// The identifier of the refresh token family that was created. Stable across the family's rotations.
        /// </summary>
        public required string FamilyId { get; init; }
    }
}
