using System.Text.Json.Serialization;

namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// What the assertion consumer service seals with a completion: the browser binding and caller binding from the
    /// pending request, and the outcome of the response, either a <see cref="SamlCompletionSuccess"/> or a
    /// <see cref="SamlCompletionRefusal"/>.
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "outcome")]
    [JsonDerivedType(typeof(SamlCompletionSuccess), "success")]
    [JsonDerivedType(typeof(SamlCompletionRefusal), "refusal")]
    internal abstract class SamlCompletionOutcome
    {
        public required string FlowId { get; init; }

        public required string BindingCookieHash { get; init; }

        public required string CallerBinding { get; init; }

        /// <summary>
        /// The account the response named, when it was verified far enough to know it: what a refusal of the code at
        /// completion reports as attempted, whichever the outcome.
        /// </summary>
        [JsonIgnore]
        public abstract SamlIdentity? AttemptedIdentity { get; }
    }
}
