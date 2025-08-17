using EasyReasy.KnowledgeBase.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    /// <summary>
    /// SQLite-based implementation of the section store for managing knowledge file sections.
    /// </summary>
    public class SqliteSectionStore : ISectionStore, IExplicitPersistence
    {
        private readonly string _connectionString;
        private bool _isInitialized = false;

        /// <summary>
        /// Initializes a new instance of the <see cref="SqliteSectionStore"/> class with the specified connection string.
        /// </summary>
        /// <param name="connectionString">The SQLite connection string to use for database operations.</param>
        /// <exception cref="ArgumentNullException">Thrown when the connection string is null.</exception>
        public SqliteSectionStore(string connectionString)
        {
            _connectionString = connectionString ?? throw new ArgumentNullException(nameof(connectionString));
        }

        /// <summary>
        /// Loads and initializes the section store database schema.
        /// </summary>
        /// <param name="cancellationToken">Optional cancellation token to cancel the operation.</param>
        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (_isInitialized) return;

            await InitializeDatabaseAsync();
            _isInitialized = true;
        }

        /// <summary>
        /// Saves the section store data. For SQLite, this is a no-op as data is saved transactionally.
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

        /// <summary>
        /// Adds a new knowledge file section to the store.
        /// </summary>
        /// <param name="section">The section to add.</param>
        /// <exception cref="ArgumentNullException">Thrown when the section is null.</exception>
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

        /// <summary>
        /// Retrieves a knowledge file section by its unique identifier.
        /// </summary>
        /// <param name="sectionId">The unique identifier of the section to retrieve.</param>
        /// <returns>The section if found; otherwise, null.</returns>
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

        /// <summary>
        /// Retrieves a knowledge file section by its file identifier and section index.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file.</param>
        /// <param name="sectionIndex">The index of the section within the file.</param>
        /// <returns>The section if found; otherwise, null.</returns>
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

        /// <summary>
        /// Deletes all sections associated with a specific file.
        /// </summary>
        /// <param name="fileId">The unique identifier of the file whose sections should be deleted.</param>
        /// <returns>True if any sections were deleted; otherwise, false.</returns>
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
