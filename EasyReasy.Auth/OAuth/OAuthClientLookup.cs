using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of <see cref="OAuthClientDirectory.FindAsync"/>.
    /// </summary>
    internal sealed class OAuthClientLookup
    {
        private OAuthClientLookup(OAuthResolvedClient? client, string? failure)
        {
            Client = client;
            Failure = failure;
        }

        /// <summary>
        /// Whether the client was found.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Client))]
        [MemberNotNullWhen(false, nameof(Failure))]
        public bool IsFound => Client != null;

        /// <summary>
        /// The client, when found.
        /// </summary>
        public OAuthResolvedClient? Client { get; }

        /// <summary>
        /// Why there is no client, when there is none.
        /// </summary>
        public string? Failure { get; }

        /// <summary>
        /// A found client.
        /// </summary>
        /// <param name="client">The client.</param>
        /// <returns>The lookup.</returns>
        public static OAuthClientLookup Found(OAuthResolvedClient client)
        {
            return new OAuthClientLookup(client, null);
        }

        /// <summary>
        /// No client.
        /// </summary>
        /// <param name="failure">Why.</param>
        /// <returns>The lookup.</returns>
        public static OAuthClientLookup Failed(string failure)
        {
            return new OAuthClientLookup(null, failure);
        }
    }
}
