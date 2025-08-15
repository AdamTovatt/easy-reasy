using EasyReasy.KnowledgeBase.Models;
using Microsoft.Data.Sqlite;
using System.Data;

namespace EasyReasy.KnowledgeBase.Storage.Sqlite
{
    public class SqliteFileStore : IFileStore, IExplicitPersistence
    {
        private readonly string _connectionString;
        private bool _isInitialized = false;

        public SqliteFileStore(string connectionString)
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
                CREATE TABLE IF NOT EXISTS knowledge_files (
                    id TEXT PRIMARY KEY,
                    name TEXT NOT NULL,
                    hash BLOB NOT NULL
                )";

            using SqliteCommand command = new SqliteCommand(createTableSql, connection);
            await command.ExecuteNonQueryAsync();
        }

        public async Task<Guid> AddAsync(KnowledgeFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string insertSql = @"
                INSERT INTO knowledge_files (id, name, hash) 
                VALUES (@Id, @Name, @Hash)";

            using SqliteCommand command = new SqliteCommand(insertSql, connection);
            command.Parameters.AddWithValue("@Id", file.Id.ToString());
            command.Parameters.AddWithValue("@Name", file.Name);
            command.Parameters.AddWithValue("@Hash", file.Hash);

            await command.ExecuteNonQueryAsync();
            return file.Id;
        }

        public async Task<KnowledgeFile?> GetAsync(Guid fileId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, name, hash 
                FROM knowledge_files 
                WHERE id = @Id";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@Id", fileId.ToString());

            using SqliteDataReader reader = await command.ExecuteReaderAsync();
            if (await reader.ReadAsync())
            {
                object? hashValue = reader.GetValue("hash");
                if (hashValue is not byte[] hashBytes)
                    throw new InvalidOperationException("Hash value must be a byte array in database.");

                return new KnowledgeFile(
                    Guid.Parse(reader.GetString("id")),
                    reader.GetString("name"),
                    hashBytes
                );
            }

            return null;
        }

        public async Task<bool> ExistsAsync(Guid fileId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT COUNT(*) 
                FROM knowledge_files 
                WHERE id = @Id";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            command.Parameters.AddWithValue("@Id", fileId.ToString());

            object? result = await command.ExecuteScalarAsync();
            if (result == null)
                return false;
            long count = (long)result;
            return count > 0;
        }

        public async Task<IEnumerable<KnowledgeFile>> GetAllAsync()
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string selectSql = @"
                SELECT id, name, hash 
                FROM knowledge_files";

            using SqliteCommand command = new SqliteCommand(selectSql, connection);
            using SqliteDataReader reader = await command.ExecuteReaderAsync();

            List<KnowledgeFile> files = new List<KnowledgeFile>();
            while (await reader.ReadAsync())
            {
                object? hashValue = reader.GetValue("hash");
                if (hashValue == null)
                    throw new InvalidOperationException("Hash value cannot be null in database.");
                if (hashValue is not byte[] hashBytes)
                    throw new InvalidOperationException("Hash value must be a byte array in database.");

                files.Add(new KnowledgeFile(
                    Guid.Parse(reader.GetString("id")),
                    reader.GetString("name"),
                    hashBytes
                ));
            }

            return files;
        }

        public async Task UpdateAsync(KnowledgeFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string updateSql = @"
                UPDATE knowledge_files 
                SET name = @Name, hash = @Hash 
                WHERE id = @Id";

            using SqliteCommand command = new SqliteCommand(updateSql, connection);
            command.Parameters.AddWithValue("@Id", file.Id.ToString());
            command.Parameters.AddWithValue("@Name", file.Name);
            command.Parameters.AddWithValue("@Hash", file.Hash);

            int rowsAffected = await command.ExecuteNonQueryAsync();
            if (rowsAffected == 0)
                throw new InvalidOperationException($"File with ID {file.Id} does not exist.");
        }

        public async Task<bool> DeleteAsync(Guid fileId)
        {
            if (!_isInitialized)
                await LoadAsync();

            using SqliteConnection connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string deleteSql = @"
                DELETE FROM knowledge_files 
                WHERE id = @Id";

            using SqliteCommand command = new SqliteCommand(deleteSql, connection);
            command.Parameters.AddWithValue("@Id", fileId.ToString());

            int rowsAffected = await command.ExecuteNonQueryAsync();
            return rowsAffected > 0;
        }
    }
}
