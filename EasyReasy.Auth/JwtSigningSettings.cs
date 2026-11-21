using Microsoft.IdentityModel.Tokens;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The signing key, issuer and clock skew <see cref="AuthServiceCollectionExtensions.AddEasyReasyAuth(Microsoft.Extensions.DependencyInjection.IServiceCollection, string, Action{EasyReasyAuthOptions}?)"/>
    /// registered: the one place a token issuer or a bearer's validation is made from them. The app's own tokens are
    /// signed with the secret. An OAuth connection's tokens are signed with a separate key derived from the secret, so
    /// the app's bearer cannot validate a connection's token, and the connection bearer cannot validate the app's.
    /// </summary>
    internal sealed class JwtSigningSettings
    {
        // HKDF's info: names what the derived key is for, so no other derivation from the same secret yields it. Never
        // change it: a new label derives a new key, and every connection access token already issued stops validating.
        private static readonly byte[] ConnectionKeyPurpose = Encoding.UTF8.GetBytes("EasyReasy.Auth OAuth connection access token signing key");

        private readonly byte[] _firstPartyKey;
        private readonly byte[] _connectionKey;

        /// <summary>
        /// Initializes the settings.
        /// </summary>
        /// <param name="secretKey">The secret's signing key, from <see cref="JwtTokenService.SigningKeyFromSecret"/>.</param>
        /// <param name="issuer">The issuer, or null.</param>
        /// <param name="clockSkew">The clock skew both bearers allow.</param>
        public JwtSigningSettings(byte[] secretKey, string? issuer, TimeSpan clockSkew)
        {
            _firstPartyKey = secretKey;
            _connectionKey = HKDF.DeriveKey(HashAlgorithmName.SHA256, _firstPartyKey, outputLength: 32, salt: [], info: ConnectionKeyPurpose);
            Issuer = issuer;
            ClockSkew = clockSkew;
        }

        /// <summary>
        /// The issuer tokens carry, or null.
        /// </summary>
        public string? Issuer { get; }

        /// <summary>
        /// The clock skew both bearers allow.
        /// </summary>
        public TimeSpan ClockSkew { get; }

        /// <summary>
        /// The app's own token service: it signs with the secret and sets <paramref name="audience"/>.
        /// </summary>
        /// <param name="audience">The audience the tokens carry, or null for none.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <returns>The token service.</returns>
        public JwtTokenService CreateFirstPartyTokenService(string? audience, TimeProvider timeProvider)
        {
            return new JwtTokenService(_firstPartyKey, Issuer, audience, timeProvider);
        }

        /// <summary>
        /// The OAuth connection's token service: it signs with the derived key and sets <paramref name="resource"/> as
        /// the audience.
        /// </summary>
        /// <param name="resource">The protected resource the tokens are for.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <returns>The token service.</returns>
        public JwtTokenService CreateConnectionTokenService(string resource, TimeProvider timeProvider)
        {
            return new JwtTokenService(_connectionKey, Issuer, resource, timeProvider);
        }

        /// <summary>
        /// The app's bearer's validation: the secret's signature, the issuer when one is set, and
        /// <paramref name="audience"/> when one is set.
        /// </summary>
        /// <param name="audience">The app's audience, or null to check none.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <returns>The validation parameters.</returns>
        public TokenValidationParameters CreateFirstPartyValidationParameters(string? audience, TimeProvider timeProvider)
        {
            TokenValidationParameters parameters = CreateValidationParameters(_firstPartyKey, timeProvider);
            parameters.ValidateAudience = audience != null;
            parameters.ValidAudience = audience;
            return parameters;
        }

        /// <summary>
        /// The connection bearer's validation: the derived key's signature, the issuer when one is set, and always the
        /// audience: <paramref name="resource"/>, or, with no OAuth server registered, none, so every token is refused.
        /// </summary>
        /// <param name="resource">The protected resource, or null when no OAuth server is registered.</param>
        /// <param name="timeProvider">The clock.</param>
        /// <returns>The validation parameters.</returns>
        public TokenValidationParameters CreateConnectionValidationParameters(string? resource, TimeProvider timeProvider)
        {
            TokenValidationParameters parameters = CreateValidationParameters(_connectionKey, timeProvider);
            parameters.ValidateAudience = true;
            parameters.ValidAudiences = resource == null ? [] : [resource];
            return parameters;
        }

        private TokenValidationParameters CreateValidationParameters(byte[] key, TimeProvider timeProvider)
        {
            return new TokenValidationParameters
            {
                ValidateIssuer = Issuer != null,
                ValidIssuer = Issuer,
                ValidateLifetime = true,
                LifetimeValidator = TimeProviderLifetimeValidator.Create(timeProvider),
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(key),
                ClockSkew = ClockSkew,
            };
        }
    }
}
