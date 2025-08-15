using EasyReasy.FileStorage;
using EasyReasy.KnowledgeBase.Models;
using System.Text.Json;

namespace EasyReasy.KnowledgeBase.Storage.FileSystem
{
    public class FileSystemSectionStore : ISectionStore, IExplicitPersistence
    {
        private readonly IFileSystem _fileSystem;
        private readonly string _storageFileName;
        private readonly Dictionary<Guid, KnowledgeFileSection> _sections;
        private readonly Dictionary<Guid, List<KnowledgeFileSection>> _sectionsByFile;

        public FileSystemSectionStore(IFileSystem fileSystem, string storageFileName = "knowledge-sections.json")
        {
            _fileSystem = fileSystem ?? throw new ArgumentNullException(nameof(fileSystem));
            _storageFileName = storageFileName;
            _sections = new Dictionary<Guid, KnowledgeFileSection>();
            _sectionsByFile = new Dictionary<Guid, List<KnowledgeFileSection>>();
        }

        public async Task AddAsync(KnowledgeFileSection section)
        {
            if (section == null)
                throw new ArgumentNullException(nameof(section));

            _sections[section.Id] = section;

            if (!_sectionsByFile.ContainsKey(section.FileId))
                _sectionsByFile[section.FileId] = new List<KnowledgeFileSection>();

            _sectionsByFile[section.FileId].Add(section);
        }

        public async Task<KnowledgeFileSection?> GetAsync(Guid sectionId)
        {
            return _sections.TryGetValue(sectionId, out KnowledgeFileSection? section) ? section : null;
        }

        public async Task<KnowledgeFileSection?> GetByIndexAsync(Guid fileId, int sectionIndex)
        {
            if (_sectionsByFile.TryGetValue(fileId, out List<KnowledgeFileSection>? fileSections))
            {
                if (sectionIndex >= 0 && sectionIndex < fileSections.Count)
                {
                    return fileSections[sectionIndex];
                }
            }

            return null;
        }

        public async Task<KnowledgeFileSection?> GetNextAsync(Guid fileId, int currentSectionIndex)
        {
            if (_sectionsByFile.TryGetValue(fileId, out List<KnowledgeFileSection>? fileSections))
            {
                int nextIndex = currentSectionIndex + 1;
                if (nextIndex >= 0 && nextIndex < fileSections.Count)
                {
                    return fileSections[nextIndex];
                }
            }

            return null;
        }

        public async Task<KnowledgeFileSection?> GetPreviousAsync(Guid fileId, int currentSectionIndex)
        {
            if (_sectionsByFile.TryGetValue(fileId, out List<KnowledgeFileSection>? fileSections))
            {
                int previousIndex = currentSectionIndex - 1;
                if (previousIndex >= 0 && previousIndex < fileSections.Count)
                {
                    return fileSections[previousIndex];
                }
            }

            return null;
        }

        public async Task<bool> DeleteByFileAsync(Guid fileId)
        {
            if (_sectionsByFile.TryGetValue(fileId, out List<KnowledgeFileSection>? fileSections))
            {
                foreach (KnowledgeFileSection section in fileSections)
                {
                    _sections.Remove(section.Id);
                }

                _sectionsByFile.Remove(fileId);
                return true;
            }

            return false;
        }

        public async Task LoadAsync(CancellationToken cancellationToken = default)
        {
            if (await _fileSystem.FileExistsAsync(_storageFileName))
            {
                string jsonContent = await _fileSystem.ReadFileAsTextAsync(_storageFileName, cancellationToken: cancellationToken);
                if (!string.IsNullOrEmpty(jsonContent))
                {
                    List<KnowledgeFileSection>? sections = JsonSerializer.Deserialize<List<KnowledgeFileSection>>(jsonContent);
                    if (sections != null)
                    {
                        _sections.Clear();
                        _sectionsByFile.Clear();

                        foreach (KnowledgeFileSection section in sections)
                        {
                            _sections[section.Id] = section;

                            if (!_sectionsByFile.ContainsKey(section.FileId))
                                _sectionsByFile[section.FileId] = new List<KnowledgeFileSection>();

                            _sectionsByFile[section.FileId].Add(section);
                        }
                    }
                }
            }
        }

        public async Task SaveAsync(CancellationToken cancellationToken = default)
        {
            List<KnowledgeFileSection> sectionsList = _sections.Values.ToList();
            string jsonContent = JsonSerializer.Serialize(sectionsList, new JsonSerializerOptions
            {
                WriteIndented = true,
            });

            await _fileSystem.WriteFileAsTextAsync(_storageFileName, jsonContent, cancellationToken);
        }
    }
}
