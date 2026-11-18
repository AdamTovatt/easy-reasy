namespace EasyReasy.Auth
{
    /// <summary>
    /// The result of starting a session through <see cref="IRefreshTokenService.StartSessionAsync"/>: the token pair to
    /// return to the client and the identifier of the refresh token family that was created.
    /// </summary>
    /// <remarks>
    /// <see cref="FamilyId"/> lets the caller retire the session it just started through
    /// <see cref="IRefreshTokenService.RetireFamilyAsync"/>, for example when a check it makes after the session starts
    /// fails.
    /// </remarks>
    public sealed class SessionStartResult
    {
        /// <summary>
        /// The first access token, its expiry and the refresh token, to return to the client.
        /// </summary>
        public required AuthResponse AuthResponse { get; init; }

        /// <summary>
        /// The identifier of the refresh token family that was created. The first access token carries it as the
        /// <see cref="EasyReasyClaimTypes.RefreshFamilyId"/> claim, and it is stable across the family's rotations.
        /// </summary>
        public required string FamilyId { get; init; }
    }
}
