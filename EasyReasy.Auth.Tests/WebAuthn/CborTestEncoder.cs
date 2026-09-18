using System.Formats.Cbor;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Writes the CBOR structures a WebAuthn authenticator produces, with every field a parameter so a
    /// test can encode one that is wrong in exactly one place. Written in <see cref="CborConformanceMode.Lax"/>
    /// so a deliberately malformed structure can be produced at all; the field order used is the canonical
    /// one a real authenticator emits.
    /// </summary>
    /// <remarks>
    /// The <c>Encode…</c> methods build a whole structure; the single-value writers below produce the
    /// fragments <see cref="CborMapBuilder"/> assembles when a test needs a map no honest authenticator
    /// would write — a duplicate key, a key that is not a text string, a value of the wrong kind.
    /// </remarks>
    internal static class CborTestEncoder
    {
        /// <summary>
        /// Encodes an attestation object. Each argument is written when it is non-null and left out of the
        /// map entirely when it is null, so every combination of present and absent entries is reachable.
        /// </summary>
        /// <param name="format">The value of <c>fmt</c>.</param>
        /// <param name="authenticatorData">The value of <c>authData</c>.</param>
        /// <param name="attestationStatement">The pre-encoded CBOR value of <c>attStmt</c>; <see cref="EncodeEmptyMap"/> is what the <c>none</c> format requires.</param>
        public static byte[] EncodeAttestationObject(string? format, byte[]? authenticatorData, byte[]? attestationStatement)
        {
            CborMapBuilder builder = new CborMapBuilder();

            if (format != null)
            {
                builder.With(EncodeTextString("fmt"), EncodeTextString(format));
            }

            if (attestationStatement != null)
            {
                builder.With(EncodeTextString("attStmt"), attestationStatement);
            }

            if (authenticatorData != null)
            {
                builder.With(EncodeTextString("authData"), EncodeByteString(authenticatorData));
            }

            return builder.Build();
        }

        /// <summary>
        /// Encodes a COSE EC2 public key. Every COSE label is a parameter so a test can encode a key whose
        /// key type, algorithm or curve disagrees with the rest of it.
        /// </summary>
        public static byte[] EncodeCoseEc2Key(byte[] x, byte[] y, int keyType = 2, long algorithm = -7, int curve = 1)
        {
            return new CborMapBuilder()
                .With(EncodeInteger(1), EncodeInteger(keyType))
                .With(EncodeInteger(3), EncodeInteger(algorithm))
                .With(EncodeInteger(-1), EncodeInteger(curve))
                .With(EncodeInteger(-2), EncodeByteString(x))
                .With(EncodeInteger(-3), EncodeByteString(y))
                .Build();
        }

        /// <summary>
        /// Encodes a COSE RSA public key, with the key type and algorithm as parameters for the same reason
        /// as <see cref="EncodeCoseEc2Key"/>.
        /// </summary>
        public static byte[] EncodeCoseRsaKey(byte[] modulus, byte[] exponent, int keyType = 3, long algorithm = -257)
        {
            return new CborMapBuilder()
                .With(EncodeInteger(1), EncodeInteger(keyType))
                .With(EncodeInteger(3), EncodeInteger(algorithm))
                .With(EncodeInteger(-1), EncodeByteString(modulus))
                .With(EncodeInteger(-2), EncodeByteString(exponent))
                .Build();
        }

        /// <summary>
        /// Encodes an empty CBOR map — the shape a WebAuthn extensions block takes when no extension
        /// returned anything, and the shape the <c>none</c> attestation statement is defined as.
        /// </summary>
        public static byte[] EncodeEmptyMap()
        {
            return new CborMapBuilder().Build();
        }

        /// <summary>Encodes a single CBOR text string, minimally.</summary>
        public static byte[] EncodeTextString(string value)
        {
            CborWriter writer = new CborWriter(CborConformanceMode.Lax);
            writer.WriteTextString(value);
            return writer.Encode();
        }

        /// <summary>
        /// Encodes a single CBOR text string with a one-byte length header even when the length would fit
        /// in the initial byte. Well-formed CBOR that is not the *minimal* encoding, which is how a test
        /// produces two keys that are the same string without being the same bytes.
        /// </summary>
        public static byte[] EncodeTextStringNonMinimally(string value)
        {
            byte[] utf8 = System.Text.Encoding.UTF8.GetBytes(value);
            byte[] encoded = new byte[utf8.Length + 2];

            encoded[0] = 0x78; // major type 3 (text string), one-byte length follows
            encoded[1] = (byte)utf8.Length;
            utf8.CopyTo(encoded, 2);

            return encoded;
        }

        /// <summary>Encodes a single CBOR byte string.</summary>
        public static byte[] EncodeByteString(byte[] value)
        {
            CborWriter writer = new CborWriter(CborConformanceMode.Lax);
            writer.WriteByteString(value);
            return writer.Encode();
        }

        /// <summary>
        /// Encodes a small non-negative CBOR integer with a one-byte length header even though it would fit
        /// in the initial byte — the integer counterpart of <see cref="EncodeTextStringNonMinimally"/>.
        /// </summary>
        public static byte[] EncodeIntegerNonMinimally(int value)
        {
            return new byte[] { 0x18, (byte)value }; // major type 0 (unsigned integer), one-byte value follows
        }

        /// <summary>Encodes a single CBOR integer, which is how COSE labels are written.</summary>
        public static byte[] EncodeInteger(long value)
        {
            CborWriter writer = new CborWriter(CborConformanceMode.Lax);
            writer.WriteInt64(value);
            return writer.Encode();
        }
    }
}
