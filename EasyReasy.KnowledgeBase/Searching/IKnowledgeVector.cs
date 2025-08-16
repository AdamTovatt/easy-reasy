using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// Represents a knowledge vector that extends the basic vector object with an identifier.
    /// </summary>
    public interface IKnowledgeVector : IVectorObject
    {
        /// <summary>
        /// Gets the unique identifier for this knowledge vector.
        /// </summary>
        Guid Id { get; }
    }
}
