using EasyReasy.KnowledgeBase.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    public class SqliteSectionStore : ISectionStore, IExplicitPersistence
    {
        private readonly string _connectionString;
        private bool _isInitialized = false;

        public SqliteSectionStore(string connectionString)
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
                CREATE TABLE IF NOT EXISTS knowledge_sections (
                    id TEXT PRIMARY KEY,
                    file_id TEXT NOT NULL,
                    section_index INTEGER NOT NULL,
                    summary TEXT,
                    additional_context TEXT,
                    embedding BLOB,
                    FOREIGN KEY (file_id) REFERENCES knowledge_files (id) ON DELETE CASCADE
                )";

            using SqliteCommand command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync();

            // Create index for better performance
            const string createIndexSql = @"
                CREATE INDEX IF NOT EXISTS idx_sections_file_id ON knowledge_sections (file_id);
                CREATE INDEX IF NOT EXISTS idx_sections_file_index ON knowledge_sections (file_id, section_index)";

            using SqliteCommand indexCommand = new SqliteCommand(createIndexSql, connection);
            await indexCommand.ExecuteNonQueryAsync();
        }

        public async Task AddAsync(KnowledgeFileSection section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string insertSql = @"
                INSERT INTO knowledge_sections (id, file_id, section_index, summary, additional_context, embedding) 
                VALUES (@Id, @FileId, @SectionIndex, @Summary, @AdditionalContext, @Embedding)";

            using SqliteCommand command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@Id", section.Id.ToString());
            command.Parameters.AddWithValue("@FileId", section.FileId.ToString());
            command.Parameters.AddWithValue("@SectionIndex", section.SectionIndex);
            command.Parameters.AddWithValue("@Summary", section.Summary ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@AdditionalContext", section.AdditionalContext ?? (object)DBNull.Value);
            command.Parameters.AddWithValue("@Embedding", section.Embedding != null ? ConvertEmbeddingToBytes(section.Embedding) : (object)DBNull.Value);

            await command.ExecuteNonQueryAsync();
        }

        public async Task<KnowledgeFileSection?> GetAsync(Guid sectionId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, file_id, section_index, summary, additional_context, embedding 
                FROM knowledge_sections 
                WHERE id = @Id";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@Id", sectionId.ToString());

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new KnowledgeFileSection(
                    Guid.Parse(reader.GetString("id")),
                    Guid.Parse(reader.GetString("file_id")),
                    reader.GetInt32("section_index"),
                    new List<KnowledgeFileChunk>(), // Chunks will be loaded separately if needed
                    reader.IsDBNull("summary") ? null : reader.GetString("summary"),
                    reader.IsDBNull("embedding") ? null : ConvertBytesToEmbedding((byte[])reader.GetValue("embedding"))
                )
                {
                    AdditionalContext = reader.IsDBNull("additional_context") ? null : reader.GetString("additional_context")
                };
            }

            return null;
        }

        public async Task<KnowledgeFileSection?> GetByIndexAsync(Guid fileId, int sectionIndex)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, file_id, section_index, summary, additional_context, embedding 
                FROM knowledge_sections 
                WHERE file_id = @FileId AND section_index = @SectionIndex";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@FileId", fileId.ToString());
            command.Parameters.AddWithValue("@SectionIndex", sectionIndex);

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                return new KnowledgeFileSection(
                    Guid.Parse(reader.GetString("id")),
                    Guid.Parse(reader.GetString("file_id")),
                    reader.GetInt32("section_index"),
                    new List<KnowledgeFileChunk>(), // Chunks will be loaded separately if needed
                    reader.IsDBNull("summary") ? null : reader.GetString("summary"),
                    reader.IsDBNull("embedding") ? null : ConvertBytesToEmbedding((byte[])reader.GetValue("embedding"))
                )
                {
                    AdditionalContext = reader.IsDBNull("additional_context") ? null : reader.GetString("additional_context")
                };
            }

            return null;
        }

        public async Task<bool> DeleteByFileAsync(Guid fileId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string deleteSql = @"
                DELETE FROM knowledge_sections 
                WHERE file_id = @FileId";

            using SqliteCommand command = new SqliteCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@FileId", fileId.ToString());

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
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
