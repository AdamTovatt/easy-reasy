using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EasyReasy.VectorStorage
{
    public class CosineVectorStore : ICosineVectorStore
    {
        // Use List for better memory locality during similarity search
        private readonly List<StoredVector> _vectors = new();
        private readonly SemaphoreSlim _semaphore = new(1, 1);
        private readonly int _dimension;

        public CosineVectorStore(int dimension)
        {
            if (dimension <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");

            _dimension = dimension;
        }

        public async Task AddAsync(StoredVector vector)
        {
            if (vector.Values.Length != _dimension)
                throw new ArgumentException($"Vector must have {_dimension} dimensions.", nameof(vector));

            await _semaphore.WaitAsync();
            try
            {
                _vectors.Add(vector);
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<bool> RemoveAsync(Guid id)
        {
            await _semaphore.WaitAsync();
            try
            {
                for (int i = 0; i < _vectors.Count; i++)
                {
                    if (_vectors[i].Id == id)
                    {
                        _vectors.RemoveAt(i);
                        return true;
                    }
                }

                return false;
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task<IEnumerable<StoredVector>> FindMostSimilarAsync(float[] queryVector, int count)
        {
            if (queryVector == null || queryVector.Length == 0 || count <= 0)
                return Enumerable.Empty<StoredVector>();

            if (queryVector.Length != _dimension)
                throw new ArgumentException($"Query vector must have {_dimension} dimensions.", nameof(queryVector));

            float queryMagnitude = CalculateMagnitude(queryVector.AsSpan());
            if (queryMagnitude == 0)
                return Enumerable.Empty<StoredVector>();

            await _semaphore.WaitAsync();
            try
            {
                // Use parallel processing for large datasets
                if (_vectors.Count > 1000)
                {
                    return await FindMostSimilarParallelAsync(queryVector, queryMagnitude, count);
                }
                else
                {
                    return FindMostSimilarSequential(queryVector, queryMagnitude, count);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        private IEnumerable<StoredVector> FindMostSimilarSequential(float[] queryVector, float queryMagnitude, int count)
        {
            MinHeap<StoredVector> minHeap = new MinHeap<StoredVector>(count);

            // Direct iteration over List for better cache locality
            for (int i = 0; i < _vectors.Count; i++)
            {
                StoredVector storedVector = _vectors[i];
                float similarity = CalculateCosineSimilarity(queryVector.AsSpan(), queryMagnitude, storedVector.GetSpan());
                minHeap.Add(storedVector, similarity);
            }

            return minHeap.GetItems();
        }

        private async Task<IEnumerable<StoredVector>> FindMostSimilarParallelAsync(float[] queryVector, float queryMagnitude, int count)
        {
            return await Task.Run(() =>
            {
                List<List<(StoredVector, float)>> localResults = new List<List<(StoredVector, float)>>();

                Parallel.ForEach(
                    Partitioner.Create(0, _vectors.Count),
                    () => new List<(StoredVector, float)>(),
                    (range, state, local) =>
                    {
                        for (int i = range.Item1; i < range.Item2; i++)
                        {
                            StoredVector stored = _vectors[i];
                            float sim = CalculateCosineSimilarity(queryVector.AsSpan(), queryMagnitude, stored.GetSpan());
                            local.Add((stored, sim));
                        }

                        return local;
                    },
                    local => localResults.Add(local));

                // Use MinHeap for final selection
                MinHeap<StoredVector> minHeap = new MinHeap<StoredVector>(count);
                foreach (List<(StoredVector, float)> local in localResults)
                {
                    foreach ((StoredVector vector, float similarity) in local)
                    {
                        minHeap.Add(vector, similarity);
                    }
                }

                return minHeap.GetItems();
            });
        }

        public async Task SaveAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable", nameof(stream));

            await _semaphore.WaitAsync();
            try
            {
                using BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);

                writer.Write(_dimension);
                writer.Write(_vectors.Count);

                foreach (StoredVector vector in _vectors)
                {
                    writer.Write(vector.Id.ToByteArray());
                    writer.Write(vector.Values.Length);
                    writer.Write(MemoryMarshal.AsBytes(vector.Values.AsSpan()));
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        public async Task LoadAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable", nameof(stream));

            await _semaphore.WaitAsync();
            try
            {
                using BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);

                _vectors.Clear();

                int loadedDimension = reader.ReadInt32();
                if (loadedDimension != _dimension)
                    throw new InvalidOperationException($"Loaded vectors have dimension {loadedDimension} but store expects dimension {_dimension}.");

                int count = reader.ReadInt32();

                for (int i = 0; i < count; i++)
                {
                    byte[] idBytes = reader.ReadBytes(16);
                    Guid id = new Guid(idBytes);

                    int vectorLength = reader.ReadInt32();
                    if (vectorLength != _dimension)
                        throw new InvalidOperationException($"Vector at index {i} has dimension {vectorLength} but store expects dimension {_dimension}.");

                    float[] floatArray = new float[vectorLength];

                    byte[] bytes = reader.ReadBytes(vectorLength * sizeof(float));
                    MemoryMarshal.Cast<byte, float>(bytes).CopyTo(floatArray);

                    StoredVector vector = new StoredVector(id, floatArray);
                    _vectors.Add(vector);
                }
            }
            finally
            {
                _semaphore.Release();
            }
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateMagnitude(ReadOnlySpan<float> vector)
        {
            float sum = 0.0f;
            for (int i = 0; i < vector.Length; i++)
            {
                sum += vector[i] * vector[i];
            }

            return MathF.Sqrt(sum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateCosineSimilarity(ReadOnlySpan<float> queryVector, float queryMagnitude, ReadOnlySpan<float> storedVector)
        {
            // Dimension validation is now done at the constructor and method entry points
            // so we can remove the length check here for better performance

            // Fast path for common dimensions
            if (queryVector.Length == 768)
            {
                return CalculateCosineSimilarity768(queryVector, queryMagnitude, storedVector);
            }

            float dotProduct = 0.0f;
            float storedMagnitude = 0.0f;

            // Use SIMD operations if available for better performance
            if (Vector.IsHardwareAccelerated && queryVector.Length >= Vector<float>.Count)
            {
                int vectorSize = Vector<float>.Count;
                int vectorCount = queryVector.Length / vectorSize;

                for (int i = 0; i < vectorCount; i++)
                {
                    int offset = i * vectorSize;
                    Vector<float> queryVec = new Vector<float>(queryVector.Slice(offset, vectorSize));
                    Vector<float> storedVec = new Vector<float>(storedVector.Slice(offset, vectorSize));

                    dotProduct += Vector.Dot(queryVec, storedVec);
                    storedMagnitude += Vector.Dot(storedVec, storedVec);
                }

                // Handle remaining elements
                for (int i = vectorCount * vectorSize; i < queryVector.Length; i++)
                {
                    dotProduct += queryVector[i] * storedVector[i];
                    storedMagnitude += storedVector[i] * storedVector[i];
                }
            }
            else
            {
                // Fallback to standard loop
                for (int i = 0; i < queryVector.Length; i++)
                {
                    dotProduct += queryVector[i] * storedVector[i];
                    storedMagnitude += storedVector[i] * storedVector[i];
                }
            }

            storedMagnitude = MathF.Sqrt(storedMagnitude);

            if (storedMagnitude == 0)
                return 0.0f;

            return dotProduct / (queryMagnitude * storedMagnitude);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateCosineSimilarity768(ReadOnlySpan<float> queryVector, float queryMagnitude, ReadOnlySpan<float> storedVector)
        {
            // Optimized for 768-dimensional vectors (common in embeddings)
            float dotProduct = 0.0f;
            float storedMagnitude = 0.0f;

            // Unroll by 4 for better performance
            int i = 0;
            for (; i <= queryVector.Length - 4; i += 4)
            {
                float q0 = queryVector[i];
                float q1 = queryVector[i + 1];
                float q2 = queryVector[i + 2];
                float q3 = queryVector[i + 3];

                float s0 = storedVector[i];
                float s1 = storedVector[i + 1];
                float s2 = storedVector[i + 2];
                float s3 = storedVector[i + 3];

                dotProduct += q0 * s0 + q1 * s1 + q2 * s2 + q3 * s3;
                storedMagnitude += s0 * s0 + s1 * s1 + s2 * s2 + s3 * s3;
            }

            // Handle remaining elements
            for (; i < queryVector.Length; i++)
            {
                float q = queryVector[i];
                float s = storedVector[i];
                dotProduct += q * s;
                storedMagnitude += s * s;
            }

            storedMagnitude = MathF.Sqrt(storedMagnitude);

            if (storedMagnitude == 0)
                return 0.0f;

            return dotProduct / (queryMagnitude * storedMagnitude);
        }
    }
}