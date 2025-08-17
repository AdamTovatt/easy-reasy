using EasyReasy.KnowledgeBase.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    /// <summary>
    /// SQLite-based implementation of the chunk store for managing knowledge file chunks.
    /// </summary>
    public class SqliteChunkStore : IChunkStore, IExplicitPersistence
    {
        private readonly string _connectionString;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteChunkStore"/> class with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to use for database operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when the connection string is null.</exception>
        public SqliteChunkStore(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Loads and initializes the chunk store database schema.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            await InitializeDatabaseAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// Saves the chunk store data. For SQLite, this is a no-op as data is saved transactionally.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        /// <returns>A completed task.</returns>
        public Task SaveAsync(CancellationToken cancellationToken = default)
        {
            // No explicit save needed for SQLite as it's transactional
            return Task.CompletedTask;
        }

        private async Task InitializeDatabaseAsync()
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string createTableSql = @"
                CREATE TABLE IF NOT EXISTS knowledge_chunks (
                    id TEXT PRIMARY KEY,
                    section_id TEXT NOT NULL,
                    chunk_index INTEGER NOT NULL,
                    content TEXT NOT NULL,
                    embedding BLOB,
                    file_id TEXT NOT NULL,
                    FOREIGN KEY (section_id) REFERENCES knowledge_sections (id) ON DELETE CASCADE
                )";

            using SqliteCommand command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync();

            // Create index for better performance
            const string createIndexSql = @"
                CREATE INDEX IF NOT EXISTS idx_chunks_section_id ON knowledge_chunks (section_id);
                CREATE INDEX IF NOT EXISTS idx_chunks_file_id ON knowledge_chunks (file_id);
                CREATE INDEX IF NOT EXISTS idx_chunks_section_index ON knowledge_chunks (section_id, chunk_index)";

            using SqliteCommand indexCommand = new SqliteCommand(createIndexSql, connection);
            await indexCommand.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Adds a new knowledge file chunk to the store.
        /// </summary>
        /// <param name="chunk">The chunk to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when the chunk is null.</exception>
        public async Task AddAsync(KnowledgeFileChunk chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string insertSql = @"
                INSERT INTO knowledge_chunks (id, section_id, chunk_index, content, embedding, file_id) 
                VALUES (@Id, @SectionId, @ChunkIndex, @Content, @Embedding, @FileId)";

            using SqliteCommand command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@Id", chunk.Id.ToString());
            command.Parameters.AddWithValue("@SectionId", chunk.SectionId.ToString());
            command.Parameters.AddWithValue("@ChunkIndex", chunk.ChunkIndex);
            command.Parameters.AddWithValue("@Content", chunk.Content);
            command.Parameters.AddWithValue("@Embedding", chunk.Embedding != null ? ConvertEmbeddingToBytes(chunk.Embedding) : (object)DBNull.Value);
            command.Parameters.AddWithValue("@FileId", await GetFileIdFromSectionAsync(chunk.SectionId));

            await command.ExecuteNonQueryAsync();
        }

        /// <summary>
        /// Retrieves a knowledge file chunk by its unique identifier.
        /// </summary>
        /// <param name="chunkId">The unique identifier of the chunk to retrieve.</param>
        /// <returns>The chunk if found; otherwise, null.</returns>
        public async Task<KnowledgeFileChunk?> GetAsync(Guid chunkId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, section_id, chunk_index, content, embedding 
                FROM knowledge_chunks 
                WHERE id = @Id";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@Id", chunkId.ToString());

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new KnowledgeFileChunk(
                    Guid.Parse(reader.GetString("id")),
                    Guid.Parse(reader.GetString("section_id")),
                    reader.GetInt32("chunk_index"),
                    reader.GetString("content"),
                    reader.IsDBNull("embedding") ? null : ConvertBytesToEmbedding((byte[])reader.GetValue("embedding"))
                );
            }

            return null;
        }

        /// <summary>
        /// Retrieves multiple knowledge file chunks by their unique identifiers.
        /// </summary>
        /// <param name="chunkIds">The collection of unique identifiers of the chunks to retrieve.</param>
        /// <returns>A collection of chunks that were found.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the chunkIds collection is null.</exception>
        public async Task<IEnumerable<KnowledgeFileChunk>> GetAsync(IEnumerable<Guid> chunkIds)
        {
            if (chunkIds == null)
                throw new ArgumentNullException(nameof(chunkIds));

            if (!chunkIds.Any())
                return Enumerable.Empty<KnowledgeFileChunk>();

            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            List<Guid> chunkIdList = chunkIds.ToList();

            // Build the SQL query with placeholders for all IDs
            string placeholders = string.Join(",", chunkIdList.Select((_, index) => $"@Id{index}"));
            string selectSql = $@"
                SELECT id, section_id, chunk_index, content, embedding 
                FROM knowledge_chunks 
                WHERE id IN ({placeholders})";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);

            // Add parameters for each ID
            for (int i = 0; i < chunkIdList.Count; i++)
            {
                command.Parameters.AddWithValue($"@Id{i}", chunkIdList[i].ToString());
            }

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                chunks.Add(new KnowledgeFileChunk(
                    Guid.Parse(reader.GetString("id")),
                    Guid.Parse(reader.GetString("section_id")),
                    reader.GetInt32("chunk_index"),
                    reader.GetString("content"),
                    reader.IsDBNull("embedding") ? null : ConvertBytesToEmbedding((byte[])reader.GetValue("embedding"))
                ));
            }

            return chunks;
        }

        /// <summary>
        /// Deletes all chunks associated with a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file whose chunks should be deleted.</param>
        /// <returns>True if any chunks were deleted; otherwise, false.</returns>
        public async Task<bool> DeleteByFileAsync(Guid fileId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string deleteSql = @"
                DELETE FROM knowledge_chunks 
                WHERE file_id = @FileId";

            using SqliteCommand command = new SqliteCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@FileId", fileId.ToString());

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }

        /// <summary>
        /// Retrieves a knowledge file chunk by its section identifier and chunk index.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section.</param>
        /// <param name="chunkIndex">The index of the chunk within the section.</param>
        /// <returns>The chunk if found; otherwise, null.</returns>
        public async Task<KnowledgeFileChunk?> GetByIndexAsync(Guid sectionId, int chunkIndex)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, section_id, chunk_index, content, embedding 
                FROM knowledge_chunks 
                WHERE section_id = @SectionId AND chunk_index = @ChunkIndex";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@SectionId", sectionId.ToString());
            command.Parameters.AddWithValue("@ChunkIndex", chunkIndex);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new KnowledgeFileChunk(
                    Guid.Parse(reader.GetString("id")),
                    Guid.Parse(reader.GetString("section_id")),
                    reader.GetInt32("chunk_index"),
                    reader.GetString("content"),
                    reader.IsDBNull("embedding") ? null : ConvertBytesToEmbedding((byte[])reader.GetValue("embedding"))
                );
            }

            return null;
        }

        /// <summary>
        /// Retrieves all knowledge file chunks belonging to a specific section.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section.</param>
        /// <returns>A collection of chunks that belong to the section.</returns>
        public async Task<IEnumerable<KnowledgeFileChunk>> GetBySectionAsync(Guid sectionId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, section_id, chunk_index, content, embedding 
                FROM knowledge_chunks 
                WHERE section_id = @SectionId 
                ORDER BY chunk_index";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@SectionId", sectionId.ToString());

            List<KnowledgeFileChunk> chunks = new List<KnowledgeFileChunk>();
            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                chunks.Add(new KnowledgeFileChunk(
                    Guid.Parse(reader.GetString("id")),
                    Guid.Parse(reader.GetString("section_id")),
                    reader.GetInt32("chunk_index"),
                    reader.GetString("content"),
                    reader.IsDBNull("embedding") ? null : ConvertBytesToEmbedding((byte[])reader.GetValue("embedding"))
                ));
            }

            return chunks;
        }

        private async Task<string> GetFileIdFromSectionAsync(Guid sectionId)
        {
            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT file_id 
                FROM knowledge_sections 
                WHERE id = @SectionId";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@SectionId", sectionId.ToString());

            object? result = await command.ExecuteScalarAsync();
            return result?.ToString() ?? string.Empty;
        }

        private static byte[] ConvertEmbeddingToBytes(float[] embedding)
        {
            byte[] bytes = new byte[embedding.Length * sizeof(float)];
            Buffer.BlockCopy(embedding, 0, bytes, 0, bytes.Length);
            return bytes;
        }

        private static float[] ConvertBytesToEmbedding(byte[] bytes)
        {
            float[] embedding = new float[bytes.Length / sizeof(float)];
            Buffer.BlockCopy(bytes, 0, embedding, 0, bytes.Length);
            return embedding;
        }
    }
}
