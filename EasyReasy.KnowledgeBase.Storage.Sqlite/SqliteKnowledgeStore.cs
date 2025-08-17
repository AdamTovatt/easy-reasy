namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    /// <summary>
    /// SQLite-based implementation of the knowledge store that provides file, section, and chunk storage.
    /// </summary>
    public class SqliteKnowledgeStore : IKnowledgeStore, IExplicitPersistence
    {
        /// <summary>
        /// Gets the file store for managing knowledge files.
        /// </summary>
        public IFileStore Files { get; }

        /// <summary>
        /// Gets the chunk store for managing knowledge file chunks.
        /// </summary>
        public IChunkStore Chunks { get; }

        /// <summary>
        /// Gets the section store for managing knowledge file sections.
        /// </summary>
        public ISectionStore Sections { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteKnowledgeStore"/> class with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to use for database operations.</param>
        /// <exception cref="ArgumentException">Thrown when the connection string is null or empty.</exception>
        public SqliteKnowledgeStore(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

            Files = new SqliteFileStore(connectionString);
            Chunks = new SqliteChunkStore(connectionString);
            Sections = new SqliteSectionStore(connectionString);
        }

        /// <summary>
        /// Creates a new SQLite knowledge store instance and initializes it with the specified database file path.
        /// </summary>
        /// <param name="path">The path to the SQLite database file.</param>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A new initialized <see cref="SqliteKnowledgeStore"/> instance.</returns>
        public static async Task<SqliteKnowledgeStore> CreateAsync(string path, CancellationToken cancellationToken = default)
        {
            SqliteKnowledgeStore result = new SqliteKnowledgeStore($"Data Source={path}");
            await result.LoadAsync(cancellationToken);

            return result;
        }

        /// <summary>
        /// Loads and initializes all underlying stores in dependency order.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            // Initialize in dependency order: Files -> Sections -> Chunks
            if (Files is IExplicitPersistence fileStore)
                await fileStore.LoadAsync(cancellationToken);
            if (Sections is IExplicitPersistence sectionStore)
                await sectionStore.LoadAsync(cancellationToken);
            if (Chunks is IExplicitPersistence chunkStore)
                await chunkStore.LoadAsync(cancellationToken);
        }

        /// <summary>
        /// Saves all underlying stores in dependency order.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            if (Files is IExplicitPersistence fileStore)
                await fileStore.SaveAsync(cancellationToken);
            if (Chunks is IExplicitPersistence chunkStore)
                await chunkStore.SaveAsync(cancellationToken);
            if (Sections is IExplicitPersistence sectionStore)
                await sectionStore.SaveAsync(cancellationToken);
        }
    }
}
