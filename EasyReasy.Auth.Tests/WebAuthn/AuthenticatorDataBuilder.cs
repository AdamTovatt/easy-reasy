using System.Buffers.Binary;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Assembles the authenticator data byte string field by field, so a test can produce one that is
    /// correct everywhere except the single field the check under test reads.
    /// </summary>
    internal sealed class AuthenticatorDataBuilder
    {
        /// <summary>The relying-party id hash to place in the first 32 bytes.</summary>
        public required byte[] RelyingPartyIdHash { get; set; }

        /// <summary>Whether to set the user-present flag.</summary>
        public bool UserPresent { get; set; } = true;

        /// <summary>Whether to set the user-verified flag.</summary>
        public bool UserVerified { get; set; } = true;

        /// <summary>Whether to set the backup-eligible flag.</summary>
        public bool BackupEligible { get; set; }

        /// <summary>Whether to set the backed-up flag.</summary>
        public bool BackedUp { get; set; }

        /// <summary>The signature counter to report.</summary>
        public uint SignCount { get; set; }

        /// <summary>The model identifier; set together with the credential fields to emit attested credential data.</summary>
        public Guid? Aaguid { get; set; }

        /// <summary>The credential id to emit as part of the attested credential data.</summary>
        public byte[]? CredentialId { get; set; }

        /// <summary>The CBOR-encoded COSE public key to emit as part of the attested credential data.</summary>
        public byte[]? CredentialPublicKey { get; set; }

        /// <summary>
        /// A credential id length to write instead of the true length of <see cref="CredentialId"/>, for
        /// tests that need a structure whose declared and actual lengths disagree.
        /// </summary>
        public ushort? DeclaredCredentialIdLength { get; set; }

        /// <summary>The CBOR-encoded extension outputs to append, and the extension-data flag with them.</summary>
        public byte[]? Extensions { get; set; }

        /// <summary>Bytes to append after everything the flags account for.</summary>
        public byte[]? TrailingBytes { get; set; }

        /// <summary>
        /// Builds the authenticator data.
        /// </summary>
        public byte[] Build()
        {
            bool hasAttestedCredentialData = CredentialId != null || CredentialPublicKey != null || Aaguid != null;

            byte flags = 0;
            if (UserPresent)
            {
                flags |= 0x01;
            }
            if (UserVerified)
            {
                flags |= 0x04;
            }
            if (BackupEligible)
            {
                flags |= 0x08;
            }
            if (BackedUp)
            {
                flags |= 0x10;
            }
            if (hasAttestedCredentialData)
            {
                flags |= 0x40;
            }
            if (Extensions != null)
            {
                flags |= 0x80;
            }

            List<byte> data = new List<byte>();
            data.AddRange(RelyingPartyIdHash);
            data.Add(flags);

            byte[] signCount = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(signCount, SignCount);
            data.AddRange(signCount);

            if (hasAttestedCredentialData)
            {
                byte[] aaguid = new byte[16];
                (Aaguid ?? Guid.Empty).TryWriteBytes(aaguid, bigEndian: true, out int _);
                data.AddRange(aaguid);

                byte[] credentialId = CredentialId ?? Array.Empty<byte>();
                byte[] credentialIdLength = new byte[2];
                BinaryPrimitives.WriteUInt16BigEndian(credentialIdLength, DeclaredCredentialIdLength ?? (ushort)credentialId.Length);
                data.AddRange(credentialIdLength);
                data.AddRange(credentialId);

                if (CredentialPublicKey != null)
                {
                    data.AddRange(CredentialPublicKey);
                }
            }

            if (Extensions != null)
            {
                data.AddRange(Extensions);
            }

            if (TrailingBytes != null)
            {
                data.AddRange(TrailingBytes);
            }

            return data.ToArray();
        }
    }
}
