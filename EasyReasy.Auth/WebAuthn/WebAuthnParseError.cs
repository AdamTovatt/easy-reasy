namespace EasyReasy.Auth
{
    /// <summary>
    /// What a WebAuthn structure failed to be while being parsed. Internal: the parsers report in these
    /// terms and each verifier translates them into the failure reason of the ceremony it is running, so
    /// the same malformed COSE key reads as an attestation problem during registration and as a stored-
    /// credential problem during authentication.
    /// </summary>
    internal enum WebAuthnParseError
    {
        /// <summary>The collected client data was not a JSON object carrying <c>type</c>, <c>challenge</c> and <c>origin</c>.</summary>
        MalformedClientData,

        /// <summary>The attestation object was not CBOR of the expected shape, or its attestation statement did not match its format.</summary>
        MalformedAttestationObject,

        /// <summary>The attestation statement format is one this library does not verify.</summary>
        UnsupportedAttestationFormat,

        /// <summary>The authenticator data was truncated or carried a length that does not fit inside it.</summary>
        MalformedAuthenticatorData,

        /// <summary>The COSE public key was not a well-formed key of its stated type.</summary>
        MalformedPublicKey,

        /// <summary>The COSE public key states an algorithm outside <see cref="CoseAlgorithm"/>.</summary>
        UnsupportedAlgorithm,
    }
}
