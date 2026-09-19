using System.Buffers.Binary;
using System.Formats.Cbor;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The authenticator data structure (WebAuthn Level 3 §6.1): a fixed 37-byte header of relying-party id
    /// hash, flags and signature counter, optionally followed by attested credential data and extensions.
    /// Internal: it is a parsing step, not something a consumer holds.
    /// </summary>
    internal sealed class AuthenticatorData
    {
        /// <summary>Length of the fixed header: 32-byte hash, 1 flags byte, 4 counter bytes.</summary>
        private const int HeaderLength = 37;

        private const int RelyingPartyIdHashLength = 32;
        private const int AaguidLength = 16;

        /// <summary>Longest credential id an authenticator may report (WebAuthn Level 3 §6.5, attested credential data).</summary>
        private const int MaximumCredentialIdLength = 1023;

        private const byte UserPresentFlag = 0x01;
        private const byte UserVerifiedFlag = 0x04;
        private const byte BackupEligibleFlag = 0x08;
        private const byte BackedUpFlag = 0x10;
        private const byte AttestedCredentialDataFlag = 0x40;
        private const byte ExtensionDataFlag = 0x80;

        /// <summary>SHA-256 of the relying-party id the credential is scoped to.</summary>
        public byte[] RelyingPartyIdHash { get; }

        /// <summary>Whether the authenticator reported that someone was physically present.</summary>
        public bool UserPresent { get; }

        /// <summary>Whether the authenticator reported that it verified who was present.</summary>
        public bool UserVerified { get; }

        /// <summary>
        /// Whether the credential may be backed up — synced to the user's other devices rather than bound
        /// to this one. Fixed for the lifetime of a credential.
        /// </summary>
        public bool BackupEligible { get; }

        /// <summary>
        /// Whether the credential currently is backed up. Only ever set on a credential that is also
        /// <see cref="BackupEligible"/>, and unlike it this can change over a credential's lifetime.
        /// </summary>
        public bool BackedUp { get; }

        /// <summary>
        /// The authenticator's signature counter. An authenticator that keeps no counter reports 0 on every
        /// ceremony, which is why a strict monotonic comparison is not the rule the verifier applies.
        /// </summary>
        public uint SignCount { get; }

        /// <summary>
        /// The authenticator model identifier, present only when attested credential data is.
        /// </summary>
        /// <remarks>
        /// <see cref="Guid.Empty"/> on the path this library supports: when a relying party asks for
        /// attestation <c>none</c>, the client replaces the AAGUID with zeroes. A real value reaches here
        /// only from an authenticator that natively emits the <c>none</c> format for a relying party that
        /// asked for a richer attestation conveyance.
        /// </remarks>
        public Guid? Aaguid { get; }

        /// <summary>The credential id, present only when attested credential data is.</summary>
        public byte[]? CredentialId { get; }

        /// <summary>The CBOR-encoded COSE public key, present only when attested credential data is.</summary>
        public byte[]? CredentialPublicKey { get; }

        private AuthenticatorData(
            byte[] relyingPartyIdHash,
            bool userPresent,
            bool userVerified,
            bool backupEligible,
            bool backedUp,
            uint signCount,
            Guid? aaguid,
            byte[]? credentialId,
            byte[]? credentialPublicKey)
        {
            RelyingPartyIdHash = relyingPartyIdHash;
            UserPresent = userPresent;
            UserVerified = userVerified;
            BackupEligible = backupEligible;
            BackedUp = backedUp;
            SignCount = signCount;
            Aaguid = aaguid;
            CredentialId = credentialId;
            CredentialPublicKey = credentialPublicKey;
        }

        /// <summary>
        /// Parses authenticator data.
        /// </summary>
        /// <param name="data">The raw authenticator data bytes.</param>
        /// <returns>The parsed structure.</returns>
        /// <exception cref="WebAuthnParseException">The data is truncated, declares a length that does not fit, or sets a combination of flags that cannot occur.</exception>
        public static AuthenticatorData Parse(ReadOnlySpan<byte> data)
        {
            if (data.Length < HeaderLength)
            {
                throw new WebAuthnParseException(
                    WebAuthnParseError.MalformedAuthenticatorData,
                    $"Authenticator data is {data.Length} bytes; the fixed header alone is {HeaderLength}.");
            }

            byte[] relyingPartyIdHash = data[..RelyingPartyIdHashLength].ToArray();
            byte flags = data[RelyingPartyIdHashLength];
            uint signCount = BinaryPrimitives.ReadUInt32BigEndian(data[(RelyingPartyIdHashLength + 1)..HeaderLength]);

            bool userPresent = (flags & UserPresentFlag) != 0;
            bool userVerified = (flags & UserVerifiedFlag) != 0;
            bool backupEligible = (flags & BackupEligibleFlag) != 0;
            bool backedUp = (flags & BackedUpFlag) != 0;
            bool hasAttestedCredentialData = (flags & AttestedCredentialDataFlag) != 0;
            bool hasExtensionData = (flags & ExtensionDataFlag) != 0;

            if (backedUp && !backupEligible)
            {
                // A credential that is backed up but was never eligible for backup is a state no honest
                // authenticator can be in (WebAuthn Level 3 §6.1).
                throw new WebAuthnParseException(
                    WebAuthnParseError.MalformedAuthenticatorData,
                    "Authenticator data reports the credential as backed up while not eligible for backup.");
            }

            ReadOnlySpan<byte> remaining = data[HeaderLength..];

            Guid? aaguid = null;
            byte[]? credentialId = null;
            byte[]? credentialPublicKey = null;

            if (hasAttestedCredentialData)
            {
                if (remaining.Length < AaguidLength + sizeof(ushort))
                {
                    throw new WebAuthnParseException(
                        WebAuthnParseError.MalformedAuthenticatorData,
                        "Authenticator data sets the attested-credential-data flag but is too short to carry an AAGUID and credential id length.");
                }

                // The AAGUID is 16 bytes in the big-endian byte order RFC 4122 prints, not the
                // little-endian layout Guid uses natively, so it has to be read as such to read back as
                // the identifier the authenticator's vendor publishes.
                aaguid = new Guid(remaining[..AaguidLength], bigEndian: true);

                int credentialIdLength = BinaryPrimitives.ReadUInt16BigEndian(remaining.Slice(AaguidLength, sizeof(ushort)));
                if (credentialIdLength == 0)
                {
                    // A credential id is what the credential is looked up by, so an empty one would store a
                    // credential that can never be found again.
                    throw new WebAuthnParseException(
                        WebAuthnParseError.MalformedAuthenticatorData,
                        "Authenticator data declares an empty credential id.");
                }

                if (credentialIdLength > MaximumCredentialIdLength)
                {
                    throw new WebAuthnParseException(
                        WebAuthnParseError.MalformedAuthenticatorData,
                        $"Authenticator data declares a {credentialIdLength}-byte credential id; {MaximumCredentialIdLength} is the maximum.");
                }

                remaining = remaining[(AaguidLength + sizeof(ushort))..];
                if (remaining.Length < credentialIdLength)
                {
                    throw new WebAuthnParseException(
                        WebAuthnParseError.MalformedAuthenticatorData,
                        $"Authenticator data declares a {credentialIdLength}-byte credential id but carries only {remaining.Length} bytes after the length.");
                }

                credentialId = remaining[..credentialIdLength].ToArray();
                remaining = remaining[credentialIdLength..];

                // The COSE key is self-delimiting CBOR with no preceding length, so how far it runs is
                // only knowable by decoding it. Skipping the value reports exactly that.
                int publicKeyLength = MeasureCborValue(remaining, "credential public key");
                credentialPublicKey = remaining[..publicKeyLength].ToArray();
                remaining = remaining[publicKeyLength..];
            }

            if (hasExtensionData)
            {
                int extensionsLength = MeasureCborValue(remaining, "extensions");
                remaining = remaining[extensionsLength..];
            }

            if (remaining.Length > 0)
            {
                throw new WebAuthnParseException(
                    WebAuthnParseError.MalformedAuthenticatorData,
                    $"Authenticator data carries {remaining.Length} trailing bytes that no flag accounts for.");
            }

            return new AuthenticatorData(
                relyingPartyIdHash,
                userPresent,
                userVerified,
                backupEligible,
                backedUp,
                signCount,
                aaguid,
                credentialId,
                credentialPublicKey);
        }

        /// <summary>
        /// Returns how many bytes the first CBOR value in <paramref name="data"/> occupies.
        /// </summary>
        private static int MeasureCborValue(ReadOnlySpan<byte> data, string what)
        {
            try
            {
                CborReader reader = new CborReader(data.ToArray(), CborConformanceMode.Strict);
                reader.SkipValue();
                return data.Length - reader.BytesRemaining;
            }
            catch (Exception exception) when (exception is CborContentException or InvalidOperationException)
            {
                throw WebAuthnParseException.FromCborFailure(WebAuthnParseError.MalformedAuthenticatorData, $"Authenticator data's {what}", exception);
            }
        }
    }
}
