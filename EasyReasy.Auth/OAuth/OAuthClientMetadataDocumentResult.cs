using System.Diagnostics.CodeAnalysis;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The outcome of fetching a client ID metadata document.
    /// </summary>
    internal sealed class OAuthClientMetadataDocumentResult
    {
        private OAuthClientMetadataDocumentResult(OAuthClientMetadata? metadata, string? failure)
        {
            Metadata = metadata;
            Failure = failure;
        }

        /// <summary>
        /// Whether the document could be used.
        /// </summary>
        [MemberNotNullWhen(true, nameof(Metadata))]
        [MemberNotNullWhen(false, nameof(Failure))]
        public bool IsUsable => Metadata != null;

        /// <summary>
        /// The document's metadata, when it could be used.
        /// </summary>
        public OAuthClientMetadata? Metadata { get; }

        /// <summary>
        /// Why it could not be used, when it could not.
        /// </summary>
        public string? Failure { get; }

        /// <summary>
        /// A usable document.
        /// </summary>
        /// <param name="metadata">Its metadata.</param>
        /// <returns>The result.</returns>
        public static OAuthClientMetadataDocumentResult Succeeded(OAuthClientMetadata metadata)
        {
            return new OAuthClientMetadataDocumentResult(metadata, null);
        }

        /// <summary>
        /// A document that could not be fetched or used.
        /// </summary>
        /// <param name="failure">Why.</param>
        /// <returns>The result.</returns>
        public static OAuthClientMetadataDocumentResult Failed(string failure)
        {
            return new OAuthClientMetadataDocumentResult(null, failure);
        }
    }
}
