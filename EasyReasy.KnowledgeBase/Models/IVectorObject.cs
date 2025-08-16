namespace EasyReasy.KnowledgeBase.Models
{
    /// <summary>
    /// Represents an object that can provide a vector for similarity or embedding purposes.
    /// </summary>
    public interface IVectorObject
    {
        /// <summary>
        /// Gets the vector representation of the object.
        /// </summary>
        /// <returns>The vector representation of the object.</returns>
        float[] Vector();

        /// <summary>
        /// Gets a value indicating whether or not the object contains a valid vector.
        /// </summary>
        /// <returns>True if the object contains a valid vector; otherwise, false.</returns>
        bool ContainsVector();
    }
}