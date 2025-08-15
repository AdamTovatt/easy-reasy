namespace EasyReasy.KnowledgeBase.Storage
{
    /// <summary>
    /// Defines an interface for components that require explicit persistence operations.
    /// This interface is useful for storage implementations that need manual control
    /// over when data is loaded from or saved to persistent storage.
    /// </summary>
    public interface IExplicitPersistence
    {
        /// <summary>
        /// Loads data from persistent storage into memory.
        /// This method should be called during startup to initialize the component with data.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous load operation.</returns>
        Task LoadAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Saves data from memory to persistent storage.
        /// This method should be called during shutdown or when explicit persistence is required.
        /// </summary>
        /// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        Task SaveAsync(CancellationToken cancellationToken = default);
    }
}
