namespace EasyReasy.Auth.Testing.Tests
{
    /// <summary>
    /// The relying party every test here runs its ceremonies against, with the real EasyReasy.Auth options
    /// generator and verifier for it, and the ceremonies themselves run end to end through them.
    /// </summary>
    internal static class TestRelyingParty
    {
        /// <summary>The relying-party id.</summary>
        public const string Id = "example.com";

        /// <summary>The origin the ceremonies run at.</summary>
        public const string Origin = "https://example.com";

        /// <summary>
        /// The bytes of a GUID: the stable, opaque user handle the library tells applications to use. The
        /// same value as EasyReasy.Auth.Tests' own, but deliberately not shared with it: the two test
        /// projects stay independent, and nothing compares one project's handle with the other's.
        /// </summary>
        public static byte[] UserHandle => new Guid("05e2a7d1-0b1e-4c7a-9d3e-5a5a5a5a5a5a").ToByteArray();

        /// <summary>A different user's handle, of the same length.</summary>
        public static byte[] OtherUserHandle => new Guid("0a4e0a4e-0a4e-4a4e-8a4e-0a4e0a4e0a4e").ToByteArray();

        /// <summary>An options generator for <see cref="Id"/>, or for another relying party served at its own host.</summary>
        public static WebAuthnOptionsGenerator NewGenerator(string relyingPartyId = Id)
        {
            return new WebAuthnOptionsGenerator(new WebAuthnRelyingParty(relyingPartyId, "Contoso", new[] { $"https://{relyingPartyId}" }));
        }

        /// <summary>A verifier for <see cref="Id"/>.</summary>
        public static WebAuthnVerifier NewVerifier()
        {
            return new WebAuthnVerifier(new WebAuthnRelyingParty(Id, "Contoso", new[] { Origin }));
        }

        /// <summary>The user entity a registration names, with <see cref="UserHandle"/> as its id.</summary>
        public static PublicKeyCredentialUserEntity NewUser()
        {
            return new PublicKeyCredentialUserEntity(UserHandle, "ada@example.com", "Ada");
        }

        /// <summary>
        /// Registers the authenticator's credential as a second factor and returns what the server stores,
        /// failing the test when the verifier declines it.
        /// </summary>
        public static WebAuthnRegisteredCredential RegisterSecondFactor(
            WebAuthnTestAuthenticator authenticator,
            WebAuthnUserVerificationRequirement userVerification = WebAuthnUserVerificationRequirement.Preferred)
        {
            PublicKeyCredentialCreationOptions options = NewGenerator().CreateRegistrationOptions(NewUser(), userVerification);
            WebAuthnTestRegistration registration = authenticator.Register(options, Origin);

            return Enrolled(NewVerifier().VerifyRegistration(registration.Response, options.Challenge, userVerification));
        }

        /// <summary>
        /// Registers the authenticator's credential as a passkey and returns what the server stores,
        /// failing the test when the verifier declines it.
        /// </summary>
        public static WebAuthnRegisteredCredential RegisterPasskey(WebAuthnTestAuthenticator authenticator)
        {
            PublicKeyCredentialCreationOptions options = NewGenerator().CreateDiscoverableRegistrationOptions(
                NewUser(),
                Array.Empty<WebAuthnCredentialReference>());
            WebAuthnTestRegistration registration = authenticator.Register(options, Origin);

            return Enrolled(NewVerifier().VerifyDiscoverableRegistration(registration.Response, options.Challenge));
        }

        /// <summary>
        /// Signs in with the authenticator as a second factor, the stored credential named in the allow
        /// list, and returns the verifier's outcome.
        /// </summary>
        public static WebAuthnAuthenticationResult SignInSecondFactor(
            WebAuthnTestAuthenticator authenticator,
            WebAuthnStoredCredential storedCredential,
            WebAuthnUserVerificationRequirement userVerification = WebAuthnUserVerificationRequirement.Preferred)
        {
            PublicKeyCredentialRequestOptions options = NewGenerator().CreateAuthenticationOptions(
                new[] { storedCredential.CredentialId },
                userVerification);
            WebAuthnTestAssertion assertion = authenticator.Assert(options, Origin);

            return NewVerifier().VerifyAuthentication(assertion.Response, options.Challenge, storedCredential, userVerification);
        }

        /// <summary>
        /// Signs in with the authenticator as a usernameless passkey and returns the verifier's outcome
        /// against the owner's <see cref="UserHandle"/>.
        /// </summary>
        public static WebAuthnAuthenticationResult SignInWithPasskey(WebAuthnTestAuthenticator authenticator, WebAuthnStoredCredential storedCredential)
        {
            PublicKeyCredentialRequestOptions options = NewGenerator().CreateDiscoverableAuthenticationOptions();
            WebAuthnTestAssertion assertion = authenticator.Assert(options, Origin);

            return NewVerifier().VerifyDiscoverableAuthentication(assertion.Response, options.Challenge, storedCredential, UserHandle);
        }

        /// <summary>The credential as the server stores it after registration.</summary>
        public static WebAuthnStoredCredential Stored(WebAuthnRegisteredCredential credential)
        {
            return new WebAuthnStoredCredential(credential.CredentialId, credential.PublicKey, credential.SignCount);
        }

        private static WebAuthnRegisteredCredential Enrolled(WebAuthnRegistrationResult result)
        {
            return result.Credential
                ?? throw new AssertFailedException($"Registration was declined: {result.FailureReason} {result.FailureMessage}");
        }
    }
}
