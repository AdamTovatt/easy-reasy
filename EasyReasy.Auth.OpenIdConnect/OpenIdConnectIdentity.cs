using System.Text.Json;

namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Who a person is at an OpenID Connect provider: the issuer and the <c>sub</c> it assigned. Together they are the
    /// account link an application stores; a <c>sub</c> is unique only within its issuer, and an email address is never an
    /// identity. Compared ordinally, both parts exactly as the ID token carried them.
    /// </summary>
    public readonly struct OpenIdConnectIdentity : IEquatable<OpenIdConnectIdentity>
    {
        /// <summary>
        /// Initializes an identity.
        /// </summary>
        /// <param name="issuer">The issuer (the ID token's <c>iss</c>).</param>
        /// <param name="subject">The subject (the ID token's <c>sub</c>).</param>
        /// <exception cref="ArgumentException">Either part is null or blank.</exception>
        public OpenIdConnectIdentity(string issuer, string subject)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(issuer);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            Issuer = issuer;
            Subject = subject;
        }

        /// <summary>
        /// The issuer (the ID token's <c>iss</c>).
        /// </summary>
        public string Issuer { get; }

        /// <summary>
        /// The subject the issuer assigned (the ID token's <c>sub</c>).
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// Whether two identities are the same account.
        /// </summary>
        public static bool operator ==(OpenIdConnectIdentity left, OpenIdConnectIdentity right) => left.Equals(right);

        /// <summary>
        /// Whether two identities are different accounts.
        /// </summary>
        public static bool operator !=(OpenIdConnectIdentity left, OpenIdConnectIdentity right) => !left.Equals(right);

        /// <inheritdoc />
        public bool Equals(OpenIdConnectIdentity other)
        {
            return string.Equals(Issuer, other.Issuer, StringComparison.Ordinal)
                && string.Equals(Subject, other.Subject, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is OpenIdConnectIdentity other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(Issuer, Subject);

        /// <summary>
        /// The identity as JSON, <c>{"issuer":"…","subject":"…"}</c>: the form an audit record's
        /// <see cref="ExternalAuthResult.AttemptedSubject"/> carries, unambiguous whatever either part holds.
        /// </summary>
        /// <returns>The JSON form.</returns>
        public override string ToString()
        {
            return JsonSerializer.Serialize(new Dictionary<string, string?> { ["issuer"] = Issuer, ["subject"] = Subject });
        }
    }
}
