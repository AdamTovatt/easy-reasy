using Microsoft.IdentityModel.Tokens;
using System.Globalization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A <see cref="TokenValidationParameters.LifetimeValidator"/> that checks a token's lifetime against a
    /// <see cref="TimeProvider"/> instead of the wall clock.
    /// </summary>
    /// <remarks>
    /// JwtBearer copies the DI <see cref="TimeProvider"/> onto its options but never hands it to IdentityModel, whose
    /// lifetime check reads a clock that <see cref="TokenValidationParameters"/> keeps non-public. A lifetime validator
    /// is the only public hook, and it replaces the built-in check entirely, so this mirrors IdentityModel's rules,
    /// exception types, properties and messages (including their IDX codes, which consumers match on). A differential
    /// test runs a grid of cases through both and requires identical outcomes.
    /// </remarks>
    internal static class TimeProviderLifetimeValidator
    {
        internal static LifetimeValidator Create(TimeProvider timeProvider)
        {
            return (notBefore, expires, securityToken, validationParameters) =>
            {
                Validate(timeProvider.GetUtcNow().UtcDateTime, notBefore, expires, securityToken, validationParameters);
                return true;
            };
        }

        /// <summary>
        /// Throws the exception IdentityModel would throw for these lifetime values at <paramref name="utcNow"/>, with
        /// its message and properties, or returns when the lifetime is valid.
        /// </summary>
        internal static void Validate(DateTime utcNow, DateTime? notBefore, DateTime? expires, SecurityToken? securityToken, TokenValidationParameters validationParameters)
        {
            if (!validationParameters.ValidateLifetime)
            {
                return;
            }

            if (!expires.HasValue && validationParameters.RequireExpirationTime)
            {
                throw new SecurityTokenNoExpirationException(
                    $"IDX10225: Lifetime validation failed. The token is missing an Expiration Time. Tokentype: '{(securityToken == null ? "null" : securityToken.GetType().ToString())}'.");
            }

            if (notBefore.HasValue && expires.HasValue && notBefore.Value > expires.Value)
            {
                throw new SecurityTokenInvalidLifetimeException(
                    $"IDX10224: Lifetime validation failed. The NotBefore (UTC): '{Format(notBefore.Value)}' is after Expires (UTC): '{Format(expires.Value)}'.")
                {
                    NotBefore = notBefore,
                    Expires = expires,
                };
            }

            if (notBefore.HasValue && notBefore.Value > AddClamped(utcNow, validationParameters.ClockSkew))
            {
                throw new SecurityTokenNotYetValidException(
                    $"IDX10222: Lifetime validation failed. The token is not yet valid. ValidFrom (UTC): '{Format(notBefore.Value)}', Current time (UTC): '{Format(utcNow)}'.")
                {
                    NotBefore = notBefore.Value,
                };
            }

            if (expires.HasValue && expires.Value < AddClamped(utcNow, validationParameters.ClockSkew.Negate()))
            {
                throw new SecurityTokenExpiredException(
                    $"IDX10223: Lifetime validation failed. The token is expired. ValidTo (UTC): '{Format(expires.Value)}', Current time (UTC): '{Format(utcNow)}'.")
                {
                    Expires = expires.Value,
                };
            }
        }

        private static string Format(DateTime time) => time.ToString(CultureInfo.InvariantCulture);

        // IdentityModel's DateTimeUtil.Add saturates at DateTime.MinValue and MaxValue instead of throwing.
        private static DateTime AddClamped(DateTime time, TimeSpan offset)
        {
            if (offset > TimeSpan.Zero && DateTime.MaxValue - time < offset)
            {
                return DateTime.MaxValue;
            }

            if (offset < TimeSpan.Zero && time - DateTime.MinValue < offset.Negate())
            {
                return DateTime.MinValue;
            }

            return time + offset;
        }
    }
}
