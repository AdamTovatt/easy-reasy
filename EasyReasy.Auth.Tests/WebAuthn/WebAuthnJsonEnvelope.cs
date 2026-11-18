namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// An application's own request or response model with a WebAuthn value nested in it, beside a member of its own
    /// that the host's serializer options do apply to.
    /// </summary>
    /// <typeparam name="T">The WebAuthn type nested in the model.</typeparam>
    internal sealed class WebAuthnJsonEnvelope<T>
        where T : class
    {
        /// <summary>The WebAuthn value.</summary>
        public T? WebAuthnPayload { get; set; }

        /// <summary>A member of the model's own, left null so a host's null handling shows on it.</summary>
        public string? CeremonyNote { get; set; }
    }
}
