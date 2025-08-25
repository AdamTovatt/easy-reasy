namespace EasyReasy.KnowledgeBase.Models
{
    /// <summary>
    /// Represents the current status of a knowledge file during processing.
    /// </summary>
    public enum KnowledgeFileStatus
    {
        /// <summary>
        /// The file is waiting to be processed.
        /// </summary>
        Pending,

        /// <summary>
        /// The file has been successfully indexed and is available for search.
        /// </summary>
        Indexed,

        /// <summary>
        /// An error occurred while processing the file.
        /// </summary>
        Error,

        /// <summary>
        /// The file content type is not supported for indexing.
        /// </summary>
        UnsupportedContentType,
    }
}
