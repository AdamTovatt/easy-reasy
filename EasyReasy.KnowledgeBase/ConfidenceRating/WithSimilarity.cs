using EasyReasy.KnowledgeBase.Models;

namespace EasyReasy.KnowledgeBase.ConfidenceRating
{
    /// <summary>
    /// Represents an item of type <typeparamref name="T"/> with an associated similarity score.
    /// </summary>
    /// <typeparam name="T">The type of the item.</typeparam>
    public class WithSimilarity<T>
        where T : IVectorObject
    {
        /// <summary>
        /// Gets the item.
        /// </summary>
        public T Item { get; }

        /// <summary>
        /// Gets the similarity score.
        /// </summary>
        public double Similarity { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="WithSimilarity{T}"/> class.
        /// </summary>
        /// <param name="item">The item.</param>
        /// <param name="similarity">The similarity score.</param>
        public WithSimilarity(T item, double similarity)
        {
            Item = item;
            Similarity = similarity;
        }

        /// <summary>
        /// Creates a WithSimilarity instance by computing cosine similarity between two vectors.
        /// </summary>
        /// <param name="theItem">The item to associate with the similarity score.</param>
        /// <param name="vectorA">The first vector.</param>
        /// <param name="vectorB">The second vector.</param>
        /// <returns>A new WithSimilarity instance.</returns>
        public static WithSimilarity<T> CreateBetween(T theItem, float[] vectorA, float[] vectorB)
        {
            double similarity = ConfidenceMath.CosineSimilarity(vectorA, vectorB);
            return new WithSimilarity<T>(theItem, similarity);
        }

        /// <summary>
        /// Creates a WithSimilarity instance by computing cosine similarity between an IVectorObject and a vector.
        /// </summary>
        /// <param name="obj">The vector object.</param>
        /// <param name="vector">The vector to compare against.</param>
        /// <returns>A new WithSimilarity instance.</returns>
        public static WithSimilarity<T> CreateBetween(IVectorObject obj, float[] vector)
        {
            double similarity = ConfidenceMath.CosineSimilarity(obj.Vector(), vector);
            return new WithSimilarity<T>((T)obj, similarity);
        }

        /// <summary>
        /// Creates a WithSimilarity instance by computing cosine similarity between two IVectorObjects.
        /// The returned WithSimilarity uses the first object (objA) as the item.
        /// </summary>
        /// <param name="objA">The first vector object.</param>
        /// <param name="objB">The second vector object.</param>
        /// <returns>A new WithSimilarity instance.</returns>
        public static WithSimilarity<T> CreateBetween(IVectorObject objA, IVectorObject objB)
        {
            double similarity = ConfidenceMath.CosineSimilarity(objA.Vector(), objB.Vector());
            return new WithSimilarity<T>((T)objA, similarity);
        }

        /// <summary>
        /// Creates a list of WithSimilarity for each item in the collection, using the provided vector for comparison.
        /// If onlyIncludeItemsWithValidVectors is true (default), only items where ContainsVector() is true are included.
        /// </summary>
        /// <param name="items">The collection of items to process.</param>
        /// <param name="vector">The vector to compare against.</param>
        /// <param name="onlyIncludeItemsWithValidVectors">Whether to only include items with valid vectors.</param>
        /// <returns>A list of WithSimilarity instances.</returns>
        public static List<WithSimilarity<T>> CreateList(IEnumerable<T> items, float[] vector, bool onlyIncludeItemsWithValidVectors = true)
        {
            IEnumerable<T> filtered = onlyIncludeItemsWithValidVectors ? items.Where(i => i.ContainsVector()) : items;
            return filtered.Select(item => CreateBetween(item, vector)).ToList();
        }
    }
}