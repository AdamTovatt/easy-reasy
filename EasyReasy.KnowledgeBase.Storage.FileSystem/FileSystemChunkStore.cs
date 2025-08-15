using EasyReasy.FileStorage;
using EasyReasy.KnowledgeBase.Models;
using System.Text.Json;

namespace EasyReasy.KnowledgeBase.Storage.FileSystem
{
    public class FileSystemChunkStore : IChunkStore, IExplicitPersistence
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _storageFileName;
        private readonly Dictionary<Guid, KnowledgeFileChunk> _chunks;
        private readonly Dictionary<Guid, List<KnowledgeFileChunk>> _chunksBySection;

        public FileSystemChunkStore(IFileSystem fileSystem, string storageFileName = "knowledge-chunks.json")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _storageFileName = storageFileName;
            _chunks = new Dictionary<Guid, KnowledgeFileChunk>();
            _chunksBySection = new Dictionary<Guid, List<KnowledgeFileChunk>>();
        }

        public async Task AddAsync(KnowledgeFileChunk chunk)
        {
            if (chunk == null)
                throw new ArgumentNullException(nameof(chunk));

            _chunks[chunk.Id] = chunk;

            if (!_chunksBySection.ContainsKey(chunk.SectionId))
                _chunksBySection[chunk.SectionId] = new List<KnowledgeFileChunk>();

            _chunksBySection[chunk.SectionId].Add(chunk);
        }

        public async Task<KnowledgeFileChunk?> GetAsync(Guid chunkId)
        {
            return _chunks.TryGetValue(chunkId, out KnowledgeFileChunk? chunk) ? chunk : null;
        }

        public async Task<bool> DeleteByFileAsync(Guid fileId)
        {
            List<Guid> chunksToRemove = _chunks.Values
                .Where(chunk => chunk.FileId == fileId)
                .Select(chunk => chunk.Id)
                .ToList();

            if (chunksToRemove.Count == 0)
                return false;

            foreach (Guid chunkId in chunksToRemove)
            {
                if (_chunks.TryGetValue(chunkId, out KnowledgeFileChunk? chunk))
                {
                    _chunks.Remove(chunkId);

                    if (_chunksBySection.TryGetValue(chunk.SectionId, out List<KnowledgeFileChunk>? sectionChunks))
                    {
                        sectionChunks.RemoveAll(c => c.Id == chunkId);
                        if (sectionChunks.Count == 0)
                            _chunksBySection.Remove(chunk.SectionId);
                    }
                }
            }

            return true;
        }

        public async Task<KnowledgeFileChunk?> GetByIndexAsync(Guid sectionId, int chunkIndex)
        {
            if (_chunksBySection.TryGetValue(sectionId, out List<KnowledgeFileChunk>? sectionChunks))
            {
                if (chunkIndex >= 0 && chunkIndex < sectionChunks.Count)
                {
                    return sectionChunks[chunkIndex];
                }
            }

            return null;
        }

        public async Task<KnowledgeFileChunk?> GetNextAsync(Guid sectionId, int currentChunkIndex)
        {
            if (_chunksBySection.TryGetValue(sectionId, out List<KnowledgeFileChunk>? sectionChunks))
            {
                int nextIndex = currentChunkIndex + 1;
                if (nextIndex >= 0 && nextIndex < sectionChunks.Count)
                {
                    return sectionChunks[nextIndex];
                }
            }

            return null;
        }

        public async Task<KnowledgeFileChunk?> GetPreviousAsync(Guid sectionId, int currentChunkIndex)
        {
            if (_chunksBySection.TryGetValue(sectionId, out List<KnowledgeFileChunk>? sectionChunks))
            {
                int previousIndex = currentChunkIndex - 1;
                if (previousIndex >= 0 && previousIndex < sectionChunks.Count)
                {
                    return sectionChunks[previousIndex];
                }
            }

            return null;
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (await _fileSystem.FileExistsAsync(_storageFileName))
            {
                string jsonContent = await _fileSystem.ReadFileAsTextAsync(_storageFileName, cancellationToken: cancellationToken);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    List<KnowledgeFileChunk>? chunks = JsonSerializer.Deserialize<List<KnowledgeFileChunk>>(jsonContent);
                    if (chunks != null)
                    {
                        _chunks.Clear();
                        _chunksBySection.Clear();

                        foreach (KnowledgeFileChunk chunk in chunks)
                        {
                            _chunks[chunk.Id] = chunk;

                            if (!_chunksBySection.ContainsKey(chunk.SectionId))
                                _chunksBySection[chunk.SectionId] = new List<KnowledgeFileChunk>();

                            _chunksBySection[chunk.SectionId].Add(chunk);
                        }
                    }
                }
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            List<KnowledgeFileChunk> chunksList = _chunks.Values.ToList();
            string jsonContent = JsonSerializer.Serialize(chunksList, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            await _fileSystem.WriteFileAsTextAsync(_storageFileName, jsonContent, cancellationToken);
        }
    }
}
