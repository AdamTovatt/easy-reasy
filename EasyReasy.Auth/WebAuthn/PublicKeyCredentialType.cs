namespace EasyReasy.Auth
{
    /// <summary>
    /// The credential type every WebAuthn descriptor and parameter set carries. The spec defines it as an
    /// enumeration with a single value, so it is a constant here rather than a type nothing could vary.
    /// </summary>
    internal static class PublicKeyCredentialType
    {
        /// <summary>The only credential type WebAuthn defines.</summary>
        public const string PublicKey = "public-key";
    }
}
