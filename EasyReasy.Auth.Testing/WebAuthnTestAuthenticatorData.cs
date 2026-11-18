using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Writes the authenticator data an honest authenticator signs over (WebAuthn Level 3 §6.1): the
    /// relying-party id hash, the flags, the sign counter and, at registration, the attested credential
    /// data.
    /// </summary>
    internal static class WebAuthnTestAuthenticatorData
    {
        private const byte UserPresentFlag = 0x01;
        private const byte UserVerifiedFlag = 0x04;
        private const byte BackupEligibleFlag = 0x08;
        private const byte BackedUpFlag = 0x10;
        private const byte AttestedCredentialDataFlag = 0x40;

        /// <summary>
        /// Writes authenticator data with no attested credential data, as an assertion carries.
        /// </summary>
        /// <param name="relyingPartyId">The relying-party id the ceremony is scoped to.</param>
        /// <param name="flags">The flags to report.</param>
        /// <param name="signCount">The sign counter to report.</param>
        public static byte[] ForAssertion(string relyingPartyId, WebAuthnTestAuthenticatorFlags flags, uint signCount)
        {
            List<byte> data = WriteHeader(relyingPartyId, flags, signCount, hasAttestedCredentialData: false);

            return data.ToArray();
        }

        /// <summary>
        /// Writes authenticator data carrying the attested credential data a registration enrolls.
        /// </summary>
        /// <param name="relyingPartyId">The relying-party id the ceremony is scoped to.</param>
        /// <param name="flags">The flags to report.</param>
        /// <param name="signCount">The sign counter to report.</param>
        /// <param name="aaguid">The authenticator model identifier.</param>
        /// <param name="credentialId">The credential id.</param>
        /// <param name="cosePublicKey">The CBOR-encoded COSE public key.</param>
        public static byte[] ForRegistration(
            string relyingPartyId,
            WebAuthnTestAuthenticatorFlags flags,
            uint signCount,
            Guid aaguid,
            byte[] credentialId,
            byte[] cosePublicKey)
        {
            List<byte> data = WriteHeader(relyingPartyId, flags, signCount, hasAttestedCredentialData: true);

            byte[] aaguidBytes = new byte[16];
            aaguid.TryWriteBytes(aaguidBytes, bigEndian: true, out int _);
            data.AddRange(aaguidBytes);

            byte[] credentialIdLength = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(credentialIdLength, checked((ushort)credentialId.Length));
            data.AddRange(credentialIdLength);
            data.AddRange(credentialId);
            data.AddRange(cosePublicKey);

            return data.ToArray();
        }

        private static List<byte> WriteHeader(string relyingPartyId, WebAuthnTestAuthenticatorFlags flags, uint signCount, bool hasAttestedCredentialData)
        {
            List<byte> data = new List<byte>();
            data.AddRange(SHA256.HashData(Encoding.UTF8.GetBytes(relyingPartyId)));
            data.Add(EncodeFlags(flags, hasAttestedCredentialData));

            byte[] signCountBytes = new byte[4];
            BinaryPrimitives.WriteUInt32BigEndian(signCountBytes, signCount);
            data.AddRange(signCountBytes);

            return data;
        }

        /// <summary>
        /// Encodes the flags byte. User presence is always set: every ceremony this authenticator answers
        /// is one a user was present for, and WebAuthn has no ceremony without it.
        /// </summary>
        private static byte EncodeFlags(WebAuthnTestAuthenticatorFlags flags, bool hasAttestedCredentialData)
        {
            byte encoded = UserPresentFlag;

            if (flags.VerifiesUser)
            {
                encoded |= UserVerifiedFlag;
            }

            if (flags.BackupEligible)
            {
                encoded |= BackupEligibleFlag;
            }

            if (flags.BackedUp)
            {
                encoded |= BackedUpFlag;
            }

            if (hasAttestedCredentialData)
            {
                encoded |= AttestedCredentialDataFlag;
            }

            return encoded;
        }
    }
}
