namespace EasyReasy.Auth.OpenIdConnect
{
    /// <summary>
    /// Configuration of the flow itself: the key that protects the OAuth <c>state</c> and how long a begun flow stays
    /// completable. The providers are not configured here: the application's <see cref="IOpenIdConnectProviderResolver"/>
    /// supplies them per request.
    /// </summary>
    public class OpenIdConnectAuthOptions
    {
        /// <summary>
        /// The 32-byte AES-256 key that encrypts and authenticates the OAuth <c>state</c>, so only this server can read
        /// or forge it. Give it its own key: never the key of a cipher the application registers for another purpose,
        /// such as encrypting MFA secrets, nor another package's state key. The key is copied when the flow is built.
        /// </summary>
        public required byte[] StateKey { get; init; }

        /// <summary>
        /// How long a begun flow stays completable: the lifetime of the <c>state</c> and of the browser binding cookie.
        /// Defaults to 10 minutes.
        /// </summary>
        public TimeSpan StateLifetime { get; init; } = TimeSpan.FromMinutes(10);

        /// <summary>
        /// Every problem with these options, for a registration to report in one exception with its own.
        /// </summary>
        /// <returns>One message per problem; empty when the options are valid.</returns>
        public IReadOnlyList<string> FindProblems()
        {
            List<string> problems = new List<string>();

            if (StateKey == null || StateKey.Length != 32)
            {
                problems.Add($"{nameof(StateKey)} must be exactly 32 bytes for AES-256; got {StateKey?.Length.ToString() ?? "null"}.");
            }

            if (StateLifetime <= TimeSpan.Zero)
            {
                problems.Add($"{nameof(StateLifetime)} must be positive; got {StateLifetime}.");
            }

            return problems;
        }
    }
}
