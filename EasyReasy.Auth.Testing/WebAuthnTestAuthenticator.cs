using System.Buffers.Text;
using System.Collections.ObjectModel;
using System.Security.Cryptography;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// A WebAuthn authenticator in software, for testing passkey and security-key endpoints against the
    /// real <see cref="WebAuthnVerifier"/>. It reads the options an endpoint returned, holds a real key
    /// pair, and answers with the body the browser's <c>PublicKeyCredential.toJSON()</c> posts.
    /// </summary>
    /// <remarks>
    /// <para>
    /// For test projects only. It signs ceremonies with a private key it holds in memory, which is exactly
    /// what a production deployment must never carry.
    /// </para>
    /// <para>
    /// One instance holds exactly one credential: <see cref="Register(string, string)"/> creates it, and
    /// <see cref="Assert(string, string)"/> signs in with it. It refuses the options listed under
    /// Refusals in the package README, the checks a real authenticator or browser makes that a test is
    /// likely to trip, and lets a test set what a dishonest or unusual authenticator would report
    /// (<see cref="VerifiesUser"/>, <see cref="UserHandle"/>, <see cref="SignCount"/> and the backup flags),
    /// so a test can show the server rejects it.
    /// </para>
    /// </remarks>
    public sealed class WebAuthnTestAuthenticator : IDisposable
    {
        /// <summary>
        /// The model identifier this authenticator reports. Spelled so that it reads as fake at a glance: an
        /// AAGUID that looked like a real one would read as a claim about some vendor's hardware.
        /// </summary>
        public static readonly Guid TestAaguid = new Guid("fa4efa4e-fa4e-4fa4-bfa4-efa4efa4efa4");

        /// <summary>The length of a credential id made when none is given, in bytes.</summary>
        public const int DefaultCredentialIdLength = 16;

        /// <summary>The longest credential id WebAuthn allows (Level 3 §4, Credential ID), in bytes.</summary>
        public const int MaximumCredentialIdLength = 1023;

        private readonly WebAuthnTestCredentialKey _key;
        private readonly byte[] _credentialId;
        private byte[]? _userHandle;
        private ReadOnlyCollection<string> _transports = Array.AsReadOnly(new[] { "internal" });
        private string? _registeredRelyingPartyId;
        private bool _disposed;

        private WebAuthnTestAuthenticator(WebAuthnTestCredentialKey key, byte[] credentialId)
        {
            _key = key;
            _credentialId = credentialId;
        }

        /// <summary>The credential id, as a copy the caller may change.</summary>
        public byte[] CredentialId => (byte[])_credentialId.Clone();

        /// <summary>The algorithm this authenticator signs with.</summary>
        public CoseAlgorithm Algorithm => _key.Algorithm;

        /// <summary>
        /// The CBOR-encoded COSE public key this authenticator enrolls at registration, as a copy the caller
        /// may change. A test that needs the raw key parameters reads them out of this.
        /// </summary>
        public byte[] CosePublicKey => (byte[])_key.CosePublicKey.Clone();

        /// <summary>The model identifier this authenticator reports at registration: <see cref="TestAaguid"/>.</summary>
        public Guid Aaguid => TestAaguid;

        /// <summary>
        /// The signature counter. 0, the default, is an authenticator that keeps no counter, like most
        /// platform passkeys, and every ceremony reports 0. Once a test sets it above 0, each
        /// <see cref="Assert(string, string)"/> raises it by one before signing, as a counting authenticator
        /// does; setting it lower than the server stored is how a test makes a regression.
        /// </summary>
        public uint SignCount { get; set; }

        /// <summary>
        /// Whether ceremonies report the user-verified (UV) flag. True by default. Set to false, the
        /// authenticator answers without it even when the options require verification, which is the
        /// dishonest client a test of the server's enforcement needs; it never refuses for this.
        /// </summary>
        public bool VerifiesUser { get; set; } = true;

        /// <summary>Whether ceremonies report the backup-eligible (BE) flag. False by default.</summary>
        public bool BackupEligible { get; set; }

        /// <summary>Whether ceremonies report the backed-up (BS) flag. False by default.</summary>
        public bool BackedUp { get; set; }

        /// <summary>
        /// The user handle the next assertion answers with, or null to answer with none. Registration
        /// records the options' <c>user.id</c> here, as a discoverable credential stores it; set it to
        /// another user's handle, or to null, to show the server checks it. Read and written as copies.
        /// </summary>
        public byte[]? UserHandle
        {
            get => (byte[]?)_userHandle?.Clone();
            set => _userHandle = (byte[]?)value?.Clone();
        }

        /// <summary>
        /// The transports registration reports. <c>internal</c> by default, as a platform authenticator
        /// reports; an empty list reports none. Set as a copy and read as a read-only list.
        /// </summary>
        /// <exception cref="ArgumentNullException">The value is null.</exception>
        /// <exception cref="ArgumentException">The value contains a null entry.</exception>
        public IReadOnlyList<string> Transports
        {
            get => _transports;
            set
            {
                ArgumentNullException.ThrowIfNull(value);

                string[] transports = value.ToArray();
                if (transports.Any(transport => transport == null))
                {
                    throw new ArgumentException("A transport list must not contain null entries.", nameof(value));
                }

                _transports = Array.AsReadOnly(transports);
            }
        }

        /// <summary>
        /// Creates an authenticator with a freshly generated key pair and no credential yet.
        /// </summary>
        /// <param name="algorithm">The algorithm to sign with: <see cref="CoseAlgorithm.Es256"/> (the default) or <see cref="CoseAlgorithm.Rs256"/>.</param>
        /// <param name="credentialId">
        /// The credential id to register, 1 to <see cref="MaximumCredentialIdLength"/> bytes, or null for
        /// <see cref="DefaultCredentialIdLength"/> random bytes. Copied, so changing the array afterwards
        /// does not change the credential.
        /// </param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="algorithm"/> is neither ES256 nor RS256.</exception>
        /// <exception cref="ArgumentException"><paramref name="credentialId"/> is empty or too long.</exception>
        public static WebAuthnTestAuthenticator Create(CoseAlgorithm algorithm = CoseAlgorithm.Es256, byte[]? credentialId = null)
        {
            if (credentialId != null && (credentialId.Length == 0 || credentialId.Length > MaximumCredentialIdLength))
            {
                throw new ArgumentException(
                    $"A credential id must be between 1 and {MaximumCredentialIdLength} bytes; got {credentialId.Length}.",
                    nameof(credentialId));
            }

            byte[] effectiveCredentialId = credentialId == null
                ? RandomNumberGenerator.GetBytes(DefaultCredentialIdLength)
                : (byte[])credentialId.Clone();

            return new WebAuthnTestAuthenticator(WebAuthnTestCredentialKey.Create(algorithm), effectiveCredentialId);
        }

        /// <summary>
        /// Creates this authenticator's credential from typed creation options.
        /// </summary>
        /// <param name="options">The options the server generated.</param>
        /// <param name="origin">The origin the page runs at, such as <c>https://example.com</c>.</param>
        /// <returns>The registration, as the posted JSON body and as a typed response.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="origin"/> is not an http or https origin.</exception>
        /// <exception cref="InvalidOperationException">
        /// This authenticator already holds a credential, or the options are ones it refuses; the message
        /// says which.
        /// </exception>
        /// <exception cref="ObjectDisposedException">This authenticator has been disposed.</exception>
        public WebAuthnTestRegistration Register(PublicKeyCredentialCreationOptions options, string origin)
        {
            ArgumentNullException.ThrowIfNull(options);

            return Register(options.ToJson(), origin);
        }

        /// <summary>
        /// Creates this authenticator's credential from the creation options JSON an endpoint returned.
        /// </summary>
        /// <param name="optionsJson">The <c>PublicKeyCredentialCreationOptionsJSON</c> the endpoint returned.</param>
        /// <param name="origin">The origin the page runs at, such as <c>https://example.com</c>.</param>
        /// <returns>The registration, as the posted JSON body and as a typed response.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// The JSON lacks a field registration needs, or <paramref name="origin"/> is not an http or https
        /// origin.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This authenticator already holds a credential, the options' relying-party id does not cover the
        /// origin, <c>excludeCredentials</c> names its credential, or <c>pubKeyCredParams</c> does not offer
        /// its algorithm.
        /// </exception>
        /// <exception cref="ObjectDisposedException">This authenticator has been disposed.</exception>
        public WebAuthnTestRegistration Register(string optionsJson, string origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(optionsJson);
            WebAuthnTestOrigin pageOrigin = WebAuthnTestOrigin.Parse(origin);

            if (_registeredRelyingPartyId != null)
            {
                throw new InvalidOperationException(
                    $"This authenticator already holds a credential for \"{_registeredRelyingPartyId}\"; one instance holds exactly one. Create another {nameof(WebAuthnTestAuthenticator)} for a second credential.");
            }

            WebAuthnTestCreationOptions options = WebAuthnTestCreationOptions.FromJson(optionsJson, pageOrigin);
            pageOrigin.ThrowIfNotCoveredBy(options.RelyingPartyId);

            if (options.ExcludedCredentialIds.Any(excluded => excluded.AsSpan().SequenceEqual(_credentialId)))
            {
                throw new InvalidOperationException(
                    "The options' excludeCredentials names this authenticator's credential, so a real authenticator refuses to register it again (WebAuthn Level 3 §6.3.2 step 3).");
            }

            if (!options.Algorithms.Contains((int)Algorithm))
            {
                throw new InvalidOperationException(
                    $"The options' pubKeyCredParams do not offer {Algorithm} ({(int)Algorithm}), the algorithm this authenticator signs with, so a real authenticator refuses to create a credential (WebAuthn Level 3 §6.3.2 step 2).");
            }

            byte[] authenticatorData = WebAuthnTestAuthenticatorData.ForRegistration(
                options.RelyingPartyId,
                CurrentFlags(),
                SignCount,
                Aaguid,
                _credentialId,
                _key.CosePublicKey);

            byte[] clientDataJson = WebAuthnTestClientData.Write(WebAuthnWireValues.RegistrationCeremonyType, options.Challenge, pageOrigin.Serialized);
            byte[] attestationObject = WebAuthnTestCbor.EncodeNoneAttestationObject(authenticatorData);
            string credentialId = Base64Url.EncodeToString(_credentialId);

            WebAuthnRegistrationResponse response = new WebAuthnRegistrationResponse(
                credentialId,
                credentialId,
                WebAuthnWireValues.PublicKeyCredentialType,
                new WebAuthnAttestationResponse(
                    Base64Url.EncodeToString(clientDataJson),
                    Base64Url.EncodeToString(attestationObject),
                    _transports));

            string json = WebAuthnTestBrowserJson.ForRegistration(response, authenticatorData, _key.SubjectPublicKeyInfo, Algorithm);

            _userHandle = options.UserHandle;
            _registeredRelyingPartyId = options.RelyingPartyId;

            return new WebAuthnTestRegistration(json, response);
        }

        /// <summary>
        /// Signs in with this authenticator's credential, answering typed request options.
        /// </summary>
        /// <param name="options">The options the server generated.</param>
        /// <param name="origin">The origin the page runs at, such as <c>https://example.com</c>.</param>
        /// <returns>The assertion, as the posted JSON body and as a typed response.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException"><paramref name="origin"/> is not an http or https origin.</exception>
        /// <exception cref="InvalidOperationException">
        /// This authenticator holds no credential yet, or the options are ones it refuses; the message says
        /// which.
        /// </exception>
        /// <exception cref="OverflowException"><see cref="SignCount"/> is <see cref="uint.MaxValue"/> and cannot advance.</exception>
        /// <exception cref="ObjectDisposedException">This authenticator has been disposed.</exception>
        public WebAuthnTestAssertion Assert(PublicKeyCredentialRequestOptions options, string origin)
        {
            ArgumentNullException.ThrowIfNull(options);

            return Assert(options.ToJson(), origin);
        }

        /// <summary>
        /// Signs in with this authenticator's credential, answering the request options JSON an endpoint
        /// returned. Raises <see cref="SignCount"/> by one first when it is above 0.
        /// </summary>
        /// <param name="optionsJson">The <c>PublicKeyCredentialRequestOptionsJSON</c> the endpoint returned.</param>
        /// <param name="origin">The origin the page runs at, such as <c>https://example.com</c>.</param>
        /// <returns>The assertion, as the posted JSON body and as a typed response.</returns>
        /// <exception cref="ArgumentNullException">An argument is null.</exception>
        /// <exception cref="ArgumentException">
        /// The JSON lacks a field authentication needs, or <paramref name="origin"/> is not an http or https
        /// origin.
        /// </exception>
        /// <exception cref="InvalidOperationException">
        /// This authenticator holds no credential yet, the options' <c>rpId</c> does not cover the origin or
        /// is not the one it registered with, or a non-empty <c>allowCredentials</c> does not name its
        /// credential.
        /// </exception>
        /// <exception cref="OverflowException"><see cref="SignCount"/> is <see cref="uint.MaxValue"/> and cannot advance.</exception>
        /// <exception cref="ObjectDisposedException">This authenticator has been disposed.</exception>
        public WebAuthnTestAssertion Assert(string optionsJson, string origin)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            ArgumentNullException.ThrowIfNull(optionsJson);
            WebAuthnTestOrigin pageOrigin = WebAuthnTestOrigin.Parse(origin);

            if (_registeredRelyingPartyId == null)
            {
                throw new InvalidOperationException(
                    $"This authenticator holds no credential yet; call {nameof(Register)} before {nameof(Assert)}.");
            }

            WebAuthnTestRequestOptions options = WebAuthnTestRequestOptions.FromJson(optionsJson, pageOrigin);
            pageOrigin.ThrowIfNotCoveredBy(options.RelyingPartyId);

            if (!string.Equals(options.RelyingPartyId, _registeredRelyingPartyId, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"The options' rpId \"{options.RelyingPartyId}\" is not \"{_registeredRelyingPartyId}\", the one this credential was registered with, so a real authenticator has no credential to answer with.");
            }

            if (options.AllowedCredentialIds.Count > 0
                && !options.AllowedCredentialIds.Any(allowed => allowed.AsSpan().SequenceEqual(_credentialId)))
            {
                throw new InvalidOperationException(
                    "The options' allowCredentials does not name this authenticator's credential, so a real browser finds no credential to answer with.");
            }

            if (SignCount > 0)
            {
                SignCount = checked(SignCount + 1);
            }

            byte[] authenticatorData = WebAuthnTestAuthenticatorData.ForAssertion(options.RelyingPartyId, CurrentFlags(), SignCount);
            byte[] clientDataJson = WebAuthnTestClientData.Write(WebAuthnWireValues.AuthenticationCeremonyType, options.Challenge, pageOrigin.Serialized);

            byte[] signedBytes = new byte[authenticatorData.Length + SHA256.HashSizeInBytes];
            authenticatorData.CopyTo(signedBytes, 0);
            SHA256.HashData(clientDataJson, signedBytes.AsSpan(authenticatorData.Length));

            string credentialId = Base64Url.EncodeToString(_credentialId);

            WebAuthnAuthenticationResponse response = new WebAuthnAuthenticationResponse(
                credentialId,
                credentialId,
                WebAuthnWireValues.PublicKeyCredentialType,
                new WebAuthnAssertionResponse(
                    Base64Url.EncodeToString(clientDataJson),
                    Base64Url.EncodeToString(authenticatorData),
                    Base64Url.EncodeToString(_key.Sign(signedBytes)),
                    _userHandle == null ? null : Base64Url.EncodeToString(_userHandle)));

            return new WebAuthnTestAssertion(WebAuthnTestBrowserJson.ForAssertion(response), response);
        }

        /// <summary>
        /// Signs the given bytes with this authenticator's private key, the way its algorithm requires on
        /// the WebAuthn wire: an ASN.1 DER sequence for ES256, PKCS#1 v1.5 for RS256. For a test that
        /// assembles a tampered ceremony of its own.
        /// </summary>
        /// <param name="data">The bytes to sign.</param>
        /// <exception cref="ObjectDisposedException">This authenticator has been disposed.</exception>
        public byte[] Sign(ReadOnlySpan<byte> data)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);

            return _key.Sign(data);
        }

        /// <inheritdoc />
        public void Dispose()
        {
            _disposed = true;
            _key.Dispose();
        }

        private WebAuthnTestAuthenticatorFlags CurrentFlags()
        {
            return new WebAuthnTestAuthenticatorFlags(VerifiesUser, BackupEligible, BackedUp);
        }
    }
}
