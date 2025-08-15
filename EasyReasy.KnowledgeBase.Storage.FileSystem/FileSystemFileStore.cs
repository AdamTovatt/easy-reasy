using System.Text.Json;
using EasyReasy.FileStorage;
using EasyReasy.KnowledgeBase.Models;
using EasyReasy.KnowledgeBase.Storage;

namespace EasyReasy.KnowledgeBase.Storage.FileSystem
{
    public class FileSystemFileStore : IFileStore, IExplicitPersistence
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _storageFileName;
        private readonly Dictionary<Guid, KnowledgeFile> _files;

        public FileSystemFileStore(IFileSystem fileSystem, string storageFileName = "knowledge-files.json")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _storageFileName = storageFileName;
            _files = new Dictionary<Guid, KnowledgeFile>();
        }

        public async Task<Guid> AddAsync(KnowledgeFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            _files[file.Id] = file;
            return file.Id;
        }

        public async Task<KnowledgeFile?> GetAsync(Guid fileId)
        {
            return _files.TryGetValue(fileId, out KnowledgeFile? file) ? file : null;
        }

        public async Task<bool> ExistsAsync(Guid fileId)
        {
            return _files.ContainsKey(fileId);
        }

        public async Task<IEnumerable<KnowledgeFile>> GetAllAsync()
        {
            return _files.Values.ToList();
        }

        public async Task UpdateAsync(KnowledgeFile file)
        {
            if (file == null)
                throw new ArgumentNullException(nameof(file));

            if (!_files.ContainsKey(file.Id))
                throw new InvalidOperationException($"File with ID {file.Id} does not exist.");

            _files[file.Id] = file;
        }

        public async Task<bool> DeleteAsync(Guid fileId)
        {
            return _files.Remove(fileId);
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (await _fileSystem.FileExistsAsync(_storageFileName))
            {
                string jsonContent = await _fileSystem.ReadFileAsTextAsync(_storageFileName, cancellationToken: cancellationToken);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    List<KnowledgeFile>? files = JsonSerializer.Deserialize<List<KnowledgeFile>>(jsonContent);
                    if (files != null)
                    {
                        _files.Clear();
                        foreach (KnowledgeFile file in files)
                        {
                            _files[file.Id] = file;
                        }
                    }
                }
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            List<KnowledgeFile> filesList = _files.Values.ToList();
            string jsonContent = JsonSerializer.Serialize(filesList, new JsonSerializerOptions
            {
                WriteIndented = true,
            });
            
            await _fileSystem.WriteFileAsTextAsync(_storageFileName, jsonContent, cancellationToken);
        }
    }
}
