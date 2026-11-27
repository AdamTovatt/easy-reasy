using System.Text.Json;
using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// Who a person is at a SAML identity provider: the key the application resolved the identity provider by, its entity
    /// id, and the subject it asserted, either a persistent NameID or the value of the attribute the provider's
    /// <see cref="SamlIdentitySource"/> names. All three together are the account link an application stores. Compared
    /// ordinally, each part exactly as it was resolved or asserted.
    /// </summary>
    /// <remarks>
    /// The key is part of the identity because the key's configuration is what the response was verified against: its
    /// metadata pins the signing certificates. An entity id proves nothing on its own, since it is only text in the
    /// metadata an application stores, so two keys whose metadata claims the same entity id (one customer's administrator
    /// copying another customer's) are still different identity providers, and a subject asserted through one never
    /// matches an account linked through the other. A subject is unique only within its identity provider, and an email
    /// address is never an identity.
    /// </remarks>
    public readonly struct SamlIdentity : IEquatable<SamlIdentity>
    {
        /// <summary>
        /// Initializes an identity.
        /// </summary>
        /// <param name="identityProviderKey">The key the application's <see cref="ISamlIdentityProviderResolver"/> resolved
        /// the identity provider by.</param>
        /// <param name="identityProviderEntityId">The identity provider's entity id.</param>
        /// <param name="subject">The subject the identity provider asserted.</param>
        /// <exception cref="ArgumentException">Any part is null or blank.</exception>
        [JsonConstructor]
        public SamlIdentity(string identityProviderKey, string identityProviderEntityId, string subject)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(identityProviderKey);
            ArgumentException.ThrowIfNullOrWhiteSpace(identityProviderEntityId);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            IdentityProviderKey = identityProviderKey;
            IdentityProviderEntityId = identityProviderEntityId;
            Subject = subject;
        }

        /// <summary>
        /// The key the application resolved the identity provider by, whose configuration the response was verified
        /// against.
        /// </summary>
        public string IdentityProviderKey { get; }

        /// <summary>
        /// The identity provider's entity id, which issued the assertion.
        /// </summary>
        public string IdentityProviderEntityId { get; }

        /// <summary>
        /// The subject the identity provider asserted.
        /// </summary>
        public string Subject { get; }

        /// <summary>
        /// Whether two identities are the same account.
        /// </summary>
        public static bool operator ==(SamlIdentity left, SamlIdentity right) => left.Equals(right);

        /// <summary>
        /// Whether two identities are different accounts.
        /// </summary>
        public static bool operator !=(SamlIdentity left, SamlIdentity right) => !left.Equals(right);

        /// <inheritdoc />
        public bool Equals(SamlIdentity other)
        {
            return string.Equals(IdentityProviderKey, other.IdentityProviderKey, StringComparison.Ordinal)
                && string.Equals(IdentityProviderEntityId, other.IdentityProviderEntityId, StringComparison.Ordinal)
                && string.Equals(Subject, other.Subject, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => obj is SamlIdentity other && Equals(other);

        /// <inheritdoc />
        public override int GetHashCode() => HashCode.Combine(IdentityProviderKey, IdentityProviderEntityId, Subject);

        /// <summary>
        /// The identity as JSON, <c>{"identityProviderKey":"…","identityProvider":"…","subject":"…"}</c>: the form an audit
        /// record's <see cref="ExternalAuthResult.AttemptedSubject"/> carries, unambiguous whatever each part holds.
        /// </summary>
        /// <returns>The JSON form.</returns>
        public override string ToString()
        {
            return JsonSerializer.Serialize(new Dictionary<string, string?>
            {
                ["identityProviderKey"] = IdentityProviderKey,
                ["identityProvider"] = IdentityProviderEntityId,
                ["subject"] = Subject,
            });
        }
    }
}
