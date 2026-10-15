namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Stored transports handed back through <see cref="WebAuthnCredentialReference"/>: written onto the
    /// credential's descriptor in either ceremony, left out when there are none, and bounded exactly as
    /// registration bounded them.
    /// </summary>
    [TestClass]
    public class WebAuthnOptionsTransportTests
    {
        [TestMethod]
        public void CreateAuthenticationOptions_CredentialWithTransports_CarriesThemOnItsEntry()
        {
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new[] { "internal", "hybrid" }),
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.SecondCredentialId, new[] { "usb" }));

            CollectionAssert.AreEqual(new[] { "internal", "hybrid" }, options.AllowCredentials[0].Transports!.ToArray());
            CollectionAssert.AreEqual(new[] { "usb" }, options.AllowCredentials[1].Transports!.ToArray());
        }

        [TestMethod]
        public void CreateAuthenticationOptions_CredentialWithoutTransports_LeavesTheFieldOutOfTheJson()
        {
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId));

            Assert.IsNull(options.AllowCredentials[0].Transports);
            Assert.IsFalse(options.ToJson().Contains("transports", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateAuthenticationOptions_EmptyTransports_AreLeftOutLikeNull()
        {
            // "transports":[] gives the browser nothing to go on, so it is written as no field at all.
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, Array.Empty<string>()));

            Assert.IsNull(options.AllowCredentials[0].Transports);
            Assert.IsFalse(options.ToJson().Contains("transports", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateAuthenticationOptions_CredentialWithoutTransports_WritesTheSameEntryAsTheIdOverload()
        {
            WebAuthnOptionsGenerator generator = WebAuthnTestData.NewGenerator();

            PublicKeyCredentialRequestOptions byReference = generator.CreateAuthenticationOptions(
                new[] { new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId) },
                WebAuthnUserVerificationRequirement.Required);
            PublicKeyCredentialRequestOptions byId = generator.CreateAuthenticationOptions(
                new[] { WebAuthnOptionsTestData.FirstCredentialId },
                WebAuthnUserVerificationRequirement.Required);

            // The challenges differ, so the whole documents cannot be compared; with it swapped out they match.
            Assert.AreEqual(
                byId.ToJson().Replace(byId.Challenge, "challenge", StringComparison.Ordinal),
                byReference.ToJson().Replace(byReference.Challenge, "challenge", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateAuthenticationOptions_UnrecognisedTransport_IsHandedBackUnchanged()
        {
            // Registration keeps a name it does not know, so handing it back must not be where it is lost.
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new[] { "something-added-later" }));

            Assert.AreEqual("something-added-later", options.AllowCredentials[0].Transports!.Single());
        }

        [TestMethod]
        public void CreateAuthenticationOptions_TransportsChangedAfterwards_DoNotChangeTheOptions()
        {
            // The list is checked once; one the caller can still edit afterwards would be written unchecked.
            string[] transports = new[] { "internal" };
            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, transports));

            transports[0] = new string('t', WebAuthnAttestationResponse.MaximumTransportLength + 1);

            Assert.AreEqual("internal", options.AllowCredentials[0].Transports!.Single());
        }

        [TestMethod]
        public void CreateAuthenticationOptions_WhatRegistrationAccepted_IsAccepted()
        {
            // The round trip the bound is shared for: whatever a registration reported and the application
            // stored goes back without the application having to filter it. Taken at both limits, it is also
            // the accepting side of the bounds, without which the rejecting tests below would pass against
            // limits of zero.
            string[] atTheLimits = Enumerable.Repeat(
                new string('t', WebAuthnAttestationResponse.MaximumTransportLength),
                WebAuthnAttestationResponse.MaximumTransportCount).ToArray();
            WebAuthnAttestationResponse registration = new WebAuthnAttestationResponse(
                WebAuthnResponseTestData.ClientDataJson,
                WebAuthnResponseTestData.AttestationObject,
                atTheLimits);

            PublicKeyCredentialRequestOptions options = NewAuthenticationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, registration.Transports));

            CollectionAssert.AreEqual(registration.Transports!.ToArray(), options.AllowCredentials[0].Transports!.ToArray());
        }

        [TestMethod]
        public void CreateAuthenticationOptions_MoreTransportsThanRegistrationAccepts_NamesTheArgument()
        {
            string[] tooMany = Enumerable.Repeat("usb", WebAuthnAttestationResponse.MaximumTransportCount + 1).ToArray();

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAuthenticationOptions(new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, tooMany)));

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_TransportLongerThanRegistrationAccepts_NamesTheArgument()
        {
            string[] oneLongOne = new[] { "usb", new string('t', WebAuthnAttestationResponse.MaximumTransportLength + 1) };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAuthenticationOptions(new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, oneLongOne)));

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_TransportsOverTheBound_NameTheCredentialTheyBelongTo()
        {
            // With several stored credentials in one argument, the argument alone does not say which row to fix.
            string[] oneLongOne = new[] { new string('t', WebAuthnAttestationResponse.MaximumTransportLength + 1) };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAuthenticationOptions(
                    new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new[] { "usb" }),
                    new WebAuthnCredentialReference(WebAuthnOptionsTestData.SecondCredentialId, oneLongOne)));

            StringAssert.Contains(exception.Message, WebAuthnOptionsTestData.SecondCredentialId);
            Assert.IsFalse(exception.Message.Contains(WebAuthnOptionsTestData.FirstCredentialId, StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NullTransport_NamesTheArgument()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAuthenticationOptions(new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new string[] { "usb", null! })));

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [DataTestMethod]
        [DataRow("")]
        [DataRow("not base64url!")]
        [DataRow("Y3JlZGVudGlhbC1vbmU=")]   // padded; the wire form is unpadded
        public void CreateAuthenticationOptions_BadCredentialId_NamesTheArgument(string credentialId)
        {
            // The same id rules the credential-id overload applies, reported against this overload's argument.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAuthenticationOptions(new WebAuthnCredentialReference(credentialId, new[] { "usb" })));

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NoCredentials_Throws()
        {
            // An empty allow-list is passwordless login whichever overload spells it.
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewAuthenticationOptions());

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NullCredentials_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewGenerator().CreateAuthenticationOptions(
                    (IEnumerable<WebAuthnCredentialReference>)null!,
                    WebAuthnUserVerificationRequirement.Required));

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_NullCredential_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => NewAuthenticationOptions(new WebAuthnCredentialReference[] { null! }));

            Assert.AreEqual("allowCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_ByReference_StillChecksTheSharedArguments()
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnTestData.NewGenerator().CreateAuthenticationOptions(
                    new[] { new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId) },
                    WebAuthnUserVerificationRequirement.Required,
                    timeoutMilliseconds: 0));

            Assert.AreEqual("timeoutMilliseconds", exception.ParamName);
        }

        [TestMethod]
        public void CreateAuthenticationOptions_ByReference_CarriesTheTimeoutAndUserVerificationAskedFor()
        {
            PublicKeyCredentialRequestOptions options = WebAuthnTestData.NewGenerator().CreateAuthenticationOptions(
                new[] { new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId) },
                WebAuthnUserVerificationRequirement.Discouraged,
                timeoutMilliseconds: 12_345);

            Assert.AreEqual(12_345, options.Timeout);
            Assert.AreEqual(WebAuthnUserVerificationRequirement.Discouraged, options.UserVerification);
        }

        [TestMethod]
        public void CreateRegistrationOptions_ExcludedCredentialWithTransports_CarriesThemOnItsEntry()
        {
            PublicKeyCredentialCreationOptions options = NewRegistrationOptions(
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, new[] { "internal" }),
                new WebAuthnCredentialReference(WebAuthnOptionsTestData.SecondCredentialId));

            CollectionAssert.AreEqual(
                new[] { WebAuthnOptionsTestData.FirstCredentialId, WebAuthnOptionsTestData.SecondCredentialId },
                options.ExcludeCredentials!.Select(descriptor => descriptor.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "internal" }, options.ExcludeCredentials![0].Transports!.ToArray());
            Assert.IsNull(options.ExcludeCredentials![1].Transports);
        }

        [TestMethod]
        public void CreateRegistrationOptions_CredentialWithoutTransports_WritesTheSameEntryAsTheIdOverload()
        {
            WebAuthnOptionsGenerator generator = WebAuthnTestData.NewGenerator();

            PublicKeyCredentialCreationOptions byReference = generator.CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentials: new[] { new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId) });
            PublicKeyCredentialCreationOptions byId = generator.CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentialIds: new[] { WebAuthnOptionsTestData.FirstCredentialId });

            // The challenges differ, so the whole documents cannot be compared; with it swapped out they match.
            Assert.AreEqual(
                byId.ToJson().Replace(byId.Challenge, "challenge", StringComparison.Ordinal),
                byReference.ToJson().Replace(byReference.Challenge, "challenge", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateRegistrationOptions_ByReference_CarriesTheTimeoutAndAttachmentAskedFor()
        {
            PublicKeyCredentialCreationOptions options = WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentials: new[] { new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId) },
                authenticatorAttachment: WebAuthnAuthenticatorAttachment.Platform,
                timeoutMilliseconds: 12_345);

            Assert.AreEqual(12_345, options.Timeout);
            Assert.AreEqual(WebAuthnAuthenticatorAttachment.Platform, options.AuthenticatorSelection.AuthenticatorAttachment);
        }

        [TestMethod]
        public void CreateRegistrationOptions_NoExcludedCredentials_LeavesTheFieldOutOfTheJson()
        {
            PublicKeyCredentialCreationOptions options = NewRegistrationOptions();

            Assert.IsNull(options.ExcludeCredentials);
            Assert.IsFalse(options.ToJson().Contains("excludeCredentials", StringComparison.Ordinal));
        }

        [TestMethod]
        public void CreateRegistrationOptions_TransportLongerThanRegistrationAccepts_NamesTheArgument()
        {
            string[] oneLongOne = new[] { new string('t', WebAuthnAttestationResponse.MaximumTransportLength + 1) };

            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewRegistrationOptions(new WebAuthnCredentialReference(WebAuthnOptionsTestData.FirstCredentialId, oneLongOne)));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_BadExcludedCredentialId_NamesTheArgument()
        {
            ArgumentException exception = Assert.ThrowsException<ArgumentException>(
                () => NewRegistrationOptions(new WebAuthnCredentialReference("Y3JlZGVudGlhbC1vbmU=")));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_NullExcludedCredentials_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    excludeCredentials: null!));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_NullExcludedCredential_Throws()
        {
            ArgumentNullException exception = Assert.ThrowsException<ArgumentNullException>(
                () => NewRegistrationOptions(new WebAuthnCredentialReference[] { null! }));

            Assert.AreEqual("excludeCredentials", exception.ParamName);
        }

        [TestMethod]
        public void CreateRegistrationOptions_ByReference_StillChecksTheSharedArguments()
        {
            ArgumentOutOfRangeException exception = Assert.ThrowsException<ArgumentOutOfRangeException>(
                () => WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                    WebAuthnOptionsTestData.NewUser(),
                    WebAuthnUserVerificationRequirement.Required,
                    excludeCredentials: Array.Empty<WebAuthnCredentialReference>(),
                    authenticatorAttachment: (WebAuthnAuthenticatorAttachment)99));

            Assert.AreEqual("authenticatorAttachment", exception.ParamName);
        }

        private static PublicKeyCredentialRequestOptions NewAuthenticationOptions(params WebAuthnCredentialReference[] credentials)
        {
            return WebAuthnTestData.NewGenerator().CreateAuthenticationOptions(credentials, WebAuthnUserVerificationRequirement.Required);
        }

        private static PublicKeyCredentialCreationOptions NewRegistrationOptions(params WebAuthnCredentialReference[] credentials)
        {
            return WebAuthnTestData.NewGenerator().CreateRegistrationOptions(
                WebAuthnOptionsTestData.NewUser(),
                WebAuthnUserVerificationRequirement.Required,
                excludeCredentials: credentials);
        }
    }
}
