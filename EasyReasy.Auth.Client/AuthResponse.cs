using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Response model for successful JWT token authentication.
    /// </summary>
    public class AuthResponse
    {
        /// <summary>
        /// The wire (JSON) name of <see cref="Token"/>. Pinned by the <see cref="JsonPropertyNameAttribute"/> on the
        /// property, so renaming the property cannot silently change the field this type reads and writes.
        /// </summary>
        /// <remarks>
        /// Exposed the way the request models expose theirs, and a constant rather than a static field because a
        /// wire name is part of the protocol: it cannot change without the protocol changing, so there is nothing
        /// for an assembly that baked it in to be out of date about. A cap like
        /// <see cref="InvalidAuthResponseException.BodySnippetLength"/> is the opposite case, and is not a constant
        /// for exactly that reason.
        /// </remarks>
        public const string TokenFieldName = "token";

        /// <summary>
        /// The wire (JSON) name of <see cref="ExpiresAt"/>. See <see cref="TokenFieldName"/>.
        /// </summary>
        public const string ExpiresAtFieldName = "expiresAt";

        /// <summary>
        /// The wire (JSON) name of <see cref="RefreshToken"/>. See <see cref="TokenFieldName"/>.
        /// </summary>
        public const string RefreshTokenFieldName = "refreshToken";

        /// <summary>
        /// The options <see cref="FromJson"/> reads with.
        /// </summary>
        /// <remarks>
        /// Deliberately not <see cref="JsonSerializerSettings.CurrentOptions"/>, which the request models use: that
        /// is a settable public static, and these options carry the check that rejects a body which is not an auth
        /// response, so assigning different options must not be able to switch it off.
        /// <para>
        /// Reading only. <see cref="JsonSerializerOptions.RespectNullableAnnotations"/> refuses to <em>write</em> a
        /// null non-nullable property too, and <see cref="ToString"/> is called from exactly the catch blocks and
        /// log lines that must not throw.
        /// </para>
        /// </remarks>
        private static readonly JsonSerializerOptions ReadOptions = new JsonSerializerOptions
        {
            RespectNullableAnnotations = true,
        };

        /// <summary>
        /// The JWT token for authentication.
        /// </summary>
        [JsonRequired]
        [JsonPropertyName(TokenFieldName)]
        public string Token { get; set; }

        /// <summary>
        /// The expiration date/time of the token in ISO 8601 format (UTC).
        /// </summary>
        /// <remarks>
        /// Read as an ISO 8601 date and time: a full calendar date, then <c>T</c> or a space, then the time, with
        /// or without an offset. A value carrying no offset is read as UTC, which is what this field is documented
        /// to be. <see cref="FromJson"/> rejects anything else, so a value that is a time of day alone or a date in
        /// some local convention is not silently turned into an expiry that means nothing.
        /// </remarks>
        [JsonRequired]
        [JsonPropertyName(ExpiresAtFieldName)]
        public string ExpiresAt { get; set; }

        /// <summary>
        /// The refresh token for obtaining a new access token. Null if refresh tokens are not enabled.
        /// </summary>
        [JsonPropertyName(RefreshTokenFieldName)]
        public string? RefreshToken { get; set; }

        /// <summary>
        /// <see cref="ExpiresAt"/> as a point in time, in UTC. Read from the string rather than stored beside it,
        /// so the two cannot disagree about when the token expires.
        /// </summary>
        /// <remarks>
        /// Always <see cref="DateTimeKind.Utc"/>, whatever offset the wire value carried: an expiry that came back
        /// as a local time would be compared against a UTC clock, and on a machine that is not on UTC the token
        /// would be treated as valid for hours after it expired, or expired while it was still good.
        /// </remarks>
        /// <exception cref="FormatException">
        /// Thrown when <see cref="ExpiresAt"/> is not a date and time. An instance that came from
        /// <see cref="FromJson"/> is never in that state — reading it is the check that rejects a body whose
        /// expiry is unreadable — so this can only be reached on an instance built in code from a bad value.
        /// </exception>
        [JsonIgnore]
        public DateTime ExpirationTime => ParseExpiresAt(ExpiresAt);

        /// <summary>
        /// Initializes a new instance of the <see cref="AuthResponse"/> class.
        /// </summary>
        /// <param name="token">The JWT token for authentication.</param>
        /// <param name="expiresAt">The expiration date/time of the token in ISO 8601 format (UTC).</param>
        /// <param name="refreshToken">The refresh token for obtaining a new access token, or null if refresh tokens are not enabled.</param>
        public AuthResponse(string token, string expiresAt, string? refreshToken = null)
        {
            Token = token;
            ExpiresAt = expiresAt;
            RefreshToken = refreshToken;
        }

        /// <summary>
        /// Serializes this <see cref="AuthResponse"/> instance to a JSON string.
        /// </summary>
        /// <remarks>
        /// Writes with the serializer's defaults rather than <see cref="ReadOptions"/> or
        /// <see cref="JsonSerializerSettings.CurrentOptions"/>; the field names are pinned by the attributes on the
        /// properties either way. See the remarks on <see cref="ReadOptions"/> for why reading is the side that
        /// carries options of its own.
        /// </remarks>
        /// <returns>A JSON string representation of this <see cref="AuthResponse"/> instance.</returns>
        public string ToJson()
        {
            return JsonSerializer.Serialize(this);
        }

        /// <summary>
        /// Returns a string representation of this <see cref="AuthResponse"/> instance
        /// with the token and refresh token redacted to prevent accidental secret leakage in logs.
        /// </summary>
        /// <remarks>
        /// A value is blanked when its field name contains <c>token</c>, <c>secret</c> or <c>password</c> — the
        /// same rule <see cref="InvalidAuthResponseException.BodySnippet"/> is blanked by, so the two cannot end up
        /// disagreeing about what a secret is.
        /// </remarks>
        /// <returns>A string representation with sensitive fields replaced by "[REDACTED]".</returns>
        public override string ToString()
        {
            return SecretRedaction.Redact(ToJson());
        }

        /// <summary>
        /// Creates an <see cref="AuthResponse"/> instance from a JSON string.
        /// </summary>
        /// <param name="json">The JSON string to deserialize.</param>
        /// <returns>An <see cref="AuthResponse"/> instance.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown when the JSON cannot be read as an <see cref="AuthResponse"/>: it is not JSON, it is the literal
        /// <c>null</c>, it leaves out or nulls a field this type declares as non-nullable, or its
        /// <c>expiresAt</c> is not a date and time. The parser's own account of the failure, when there was one, is
        /// both appended to the message and carried as the inner exception — a caller diagnosing why needs what the
        /// parser saw, and a log that shows only the message would otherwise show none of it.
        /// </exception>
        public static AuthResponse FromJson(string json)
        {
            AuthResponse? result;

            try
            {
                result = JsonSerializer.Deserialize<AuthResponse>(json, ReadOptions);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException(
                    $"Failed to deserialize {nameof(AuthResponse)} from the provided JSON. {exception.Message}",
                    exception);
            }

            if (result == null)
            {
                throw new ArgumentException($"The provided JSON is the literal null, so it is not an {nameof(AuthResponse)}.");
            }

            // The token and the expiry are what the client is here for, and both are checked before the instance
            // escapes: a value that only fails later fails somewhere that can no longer say what was wrong with it.
            // Presence and nullability are the parser's job above; whether the expiry is a date and time is this one.
            if (!TryParseExpiresAt(result.ExpiresAt, out _))
            {
                throw new ArgumentException(
                    $"The provided JSON carries \"{ExpiresAtFieldName}\": \"{ResponseBodySnippet.Create(result.ExpiresAt)}\", " +
                    $"which is not a date and time, so it is not an {nameof(AuthResponse)}.");
            }

            return result;
        }

        /// <summary>
        /// Reads an expiry string as a point in time.
        /// </summary>
        /// <param name="expiresAt">The expiry string to read.</param>
        /// <returns>The expiry as a point in time.</returns>
        /// <exception cref="FormatException">Thrown when the string is not a date and time.</exception>
        private static DateTime ParseExpiresAt(string expiresAt)
        {
            if (!TryParseExpiresAt(expiresAt, out DateTime expirationTime))
            {
                throw new FormatException(
                    $"\"{ResponseBodySnippet.Create(expiresAt)}\" is not a date and time, so it is not an " +
                    $"{nameof(AuthResponse)}.{nameof(ExpiresAt)}.");
            }

            return expirationTime;
        }

        /// <summary>
        /// Tries to read an expiry string as a point in time in UTC. The one place that says how an expiry is read,
        /// so what <see cref="FromJson"/> accepts and what <see cref="ExpirationTime"/> returns cannot diverge.
        /// </summary>
        /// <remarks>
        /// The leading calendar date is required before the general parse runs, because the general parse is
        /// happy to read <c>12:30</c> as today at half past twelve and <c>1/2/2026</c> as a date in whichever
        /// convention it guesses — both of which would pass a check that only asks "is this a date and time" and
        /// then stand in for an expiry. Everything past the date is left to the general parse, which reads the
        /// offset forms an ISO 8601 time can carry; a value with no offset is taken as UTC, as the field is
        /// documented to be, and one with an offset is converted rather than kept as a local time.
        /// </remarks>
        /// <param name="expiresAt">The expiry string to read.</param>
        /// <param name="expirationTime">The expiry as a point in time in UTC, when the string is one.</param>
        /// <returns>True when the string is an ISO 8601 date and time; otherwise, false.</returns>
        private static bool TryParseExpiresAt(string expiresAt, out DateTime expirationTime)
        {
            expirationTime = default;

            if (!StartsWithCalendarDate(expiresAt))
            {
                return false;
            }

            if (!DateTimeOffset.TryParse(
                expiresAt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out DateTimeOffset expiration))
            {
                return false;
            }

            expirationTime = expiration.UtcDateTime;
            return true;
        }

        /// <summary>
        /// Whether a string opens with an ISO 8601 calendar date followed by the date/time separator.
        /// </summary>
        /// <param name="value">The string to judge.</param>
        /// <returns>True when the string opens <c>yyyy-MM-dd</c> followed by <c>T</c> or a space; otherwise, false.</returns>
        private static bool StartsWithCalendarDate(string value)
        {
            const int dateLength = 10;

            if (value.Length <= dateLength)
            {
                return false;
            }

            if (value[dateLength] != 'T' && value[dateLength] != 't' && value[dateLength] != ' ')
            {
                return false;
            }

            return DateOnly.TryParseExact(
                value.Substring(0, dateLength),
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out _);
        }
    }
}
