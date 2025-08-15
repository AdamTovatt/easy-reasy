namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    public class SqliteKnowledgeStore : IKnowledgeStore, IExplicitPersistence
    {
        public IFileStore Files { get; }
        public IChunkStore Chunks { get; }
        public ISectionStore Sections { get; }

        public SqliteKnowledgeStore(string connectionString)
        {
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new ArgumentException("Connection string cannot be null or empty.", nameof(connectionString));

            Files = new SqliteFileStore(connectionString);
            Chunks = new SqliteChunkStore(connectionString);
            Sections = new SqliteSectionStore(connectionString);
        }

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
