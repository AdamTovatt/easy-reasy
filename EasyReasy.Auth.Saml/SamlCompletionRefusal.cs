namespace EasyReasy.Auth.Saml
{
    /// <summary>
    /// A response that was refused: why, and the account it named when the response was verified far enough to know it.
    /// </summary>
    internal sealed class SamlCompletionRefusal : SamlCompletionOutcome
    {
        public required SamlAuthFailureReason Reason { get; init; }

        public required SamlIdentity? AttemptedIdentity { get; init; }
    }
}
