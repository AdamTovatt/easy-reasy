using System.Formats.Cbor;

namespace EasyReasy.Auth
{
    /// <summary>
    /// A COSE_Key (RFC 8152 §7) parsed into a .NET verification key. Internal: a consumer never handles a
    /// key, it hands back the encoded bytes this library told it to store.
    /// </summary>
    /// <remarks>
    /// The stated algorithm selects the subclass, and each subclass insists on the key type its algorithm
    /// implies — an EC2 key claiming RS256 is rejected rather than reinterpreted, so a registration cannot
    /// smuggle in a key whose stated algorithm and material disagree.
    /// </remarks>
    internal abstract class CoseKey : IDisposable
    {
        /// <summary>COSE label for the key type (RFC 8152 §7.1).</summary>
        protected const int KeyTypeLabel = 1;

        /// <summary>COSE label for the algorithm (RFC 8152 §7.1).</summary>
        protected const int AlgorithmLabel = 3;

        /// <summary>
        /// COSE label -1: the curve for an EC2 key, the modulus for an RSA key. Which it is depends on the
        /// key type, which is why reading it belongs to the subclass rather than here.
        /// </summary>
        protected const int FirstKeyTypeSpecificLabel = -1;

        /// <summary>COSE label -2: the x coordinate for an EC2 key, the exponent for an RSA key.</summary>
        protected const int SecondKeyTypeSpecificLabel = -2;

        /// <summary>COSE label -3: the y coordinate for an EC2 key; unused for RSA.</summary>
        protected const int ThirdKeyTypeSpecificLabel = -3;

        /// <summary>The signature algorithm the key states, always one this library supports.</summary>
        public abstract CoseAlgorithm Algorithm { get; }

        /// <summary>
        /// Verifies a signature over the given data with this key.
        /// </summary>
        /// <param name="data">The signed bytes.</param>
        /// <param name="signature">The signature, in the encoding the algorithm uses on the wire.</param>
        /// <returns><c>true</c> when the signature verifies.</returns>
        public abstract bool VerifySignature(ReadOnlySpan<byte> data, ReadOnlySpan<byte> signature);

        /// <summary>Releases the underlying .NET key.</summary>
        public abstract void Dispose();

        /// <summary>
        /// Parses a CBOR-encoded COSE_Key.
        /// </summary>
        /// <param name="encoded">The encoded key.</param>
        /// <returns>The parsed key, which the caller owns and must dispose.</returns>
        /// <exception cref="WebAuthnParseException">The bytes are not a supported, well-formed COSE_Key.</exception>
        public static CoseKey Parse(ReadOnlyMemory<byte> encoded)
        {
            Dictionary<int, long> integerValues = new Dictionary<int, long>();
            Dictionary<int, byte[]> byteStringValues = new Dictionary<int, byte[]>();

            ReadLabels(encoded, integerValues, byteStringValues);

            if (!integerValues.TryGetValue(KeyTypeLabel, out long keyType))
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE key has no key type (label {KeyTypeLabel}).");
            }

            if (!integerValues.TryGetValue(AlgorithmLabel, out long algorithm))
            {
                throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE key has no algorithm (label {AlgorithmLabel}).");
            }

            if (algorithm == (long)CoseAlgorithm.Es256)
            {
                return Es256CoseKey.Create(keyType, integerValues, byteStringValues);
            }

            if (algorithm == (long)CoseAlgorithm.Rs256)
            {
                return Rs256CoseKey.Create(keyType, byteStringValues);
            }

            throw new WebAuthnParseException(
                WebAuthnParseError.UnsupportedAlgorithm,
                $"COSE algorithm {algorithm} is not supported; only ES256 ({(long)CoseAlgorithm.Es256}) and RS256 ({(long)CoseAlgorithm.Rs256}) are.");
        }

        /// <summary>
        /// Reads the COSE_Key map into the integer-valued and byte-string-valued labels it carries. Labels
        /// whose value is neither, and labels that are not integers, are skipped: COSE is an extensible
        /// format and none of the ones this library reads is of those shapes.
        /// </summary>
        private static void ReadLabels(ReadOnlyMemory<byte> encoded, Dictionary<int, long> integerValues, Dictionary<int, byte[]> byteStringValues)
        {
            try
            {
                CborReader reader = new CborReader(encoded, CborConformanceMode.Strict);
                reader.ReadStartMap();

                while (reader.PeekState() != CborReaderState.EndMap)
                {
                    CborReaderState labelState = reader.PeekState();
                    if (labelState != CborReaderState.UnsignedInteger && labelState != CborReaderState.NegativeInteger)
                    {
                        // A COSE label may also be a text string; none this library reads is, so skip the pair.
                        reader.SkipValue();
                        reader.SkipValue();
                        continue;
                    }

                    int label = reader.ReadInt32();
                    CborReaderState valueState = reader.PeekState();

                    if (valueState == CborReaderState.UnsignedInteger || valueState == CborReaderState.NegativeInteger)
                    {
                        // Strict mode rejects a repeated key only when the two encodings are byte-identical,
                        // so the same label written minimally and non-minimally reaches here twice. A key
                        // carrying two algorithms is rejected rather than resolved to whichever came last.
                        if (!integerValues.TryAdd(label, reader.ReadInt64()))
                        {
                            throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE key repeats label {label}.");
                        }
                    }
                    else if (valueState == CborReaderState.ByteString)
                    {
                        if (!byteStringValues.TryAdd(label, reader.ReadByteString()))
                        {
                            throw new WebAuthnParseException(WebAuthnParseError.MalformedPublicKey, $"COSE key repeats label {label}.");
                        }
                    }
                    else
                    {
                        reader.SkipValue();
                    }
                }

                reader.ReadEndMap();

                if (reader.BytesRemaining != 0)
                {
                    throw new WebAuthnParseException(
                        WebAuthnParseError.MalformedPublicKey,
                        $"COSE key is followed by {reader.BytesRemaining} bytes that are not part of it.");
                }
            }
            catch (Exception exception) when (exception is CborContentException or InvalidOperationException or OverflowException)
            {
                throw WebAuthnParseException.FromCborFailure(WebAuthnParseError.MalformedPublicKey, "COSE key", exception);
            }
        }
    }
}
