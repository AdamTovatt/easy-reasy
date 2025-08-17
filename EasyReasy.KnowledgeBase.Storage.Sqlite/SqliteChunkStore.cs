using EasyReasy.KnowledgeBase.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    public class SqliteChunkStore : IChunkStore, IExplicitPersistence
    {
        private readonly string _connectionString;
        private bool _isInitialized = false;

        public SqliteChunkStore(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            await InitializeDatabaseAsync();
            _isInitialized = true;
        }

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
