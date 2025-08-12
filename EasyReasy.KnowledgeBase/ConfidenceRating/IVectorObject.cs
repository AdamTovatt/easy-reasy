namespace EasyReasy.KnowledgeBase.ConfidenceRating
{
    /// <summary>
    /// Represents an object that can provide a vector for similarity or embedding purposes.
    /// </summary>
    public interface IVectorObject
    {
        /// <summary>
        /// Returns the vector representation of the object.
        /// </summary>
        float[] GetVector();

        /// <summary>
        /// Returns true if the object contains a valid vector.
        /// </summary>
        bool ContainsVector();
    }
}