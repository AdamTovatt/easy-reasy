namespace EasyReasy.KnowledgeBase.Searching
{
    /// <summary>
    /// A factory that creates a knowledge base of the type <see cref="T"/>
    /// </summary>
    /// <typeparam name="T">The type of knowledge base that this factory creates.</typeparam>
    public interface IKnowledgeBaseFactory<T>
    {
        /// <summary>
        /// Creates and returns a knowledge base from this factory.
        /// </summary>
        /// <returns>A knowledgebase of type <see cref="T"/>.</returns>
        Task<T> CreateKnowledgebaseAsync();
    }
}
