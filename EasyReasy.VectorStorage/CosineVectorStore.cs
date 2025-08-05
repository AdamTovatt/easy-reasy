using System.Collections.Concurrent;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace EasyReasy.VectorStorage
{
    /// <summary>
    /// A high-performance vector store implementation using cosine similarity for vector search.
    /// Optimized for large datasets with Structure-of-Arrays memory layout and pre-computed magnitudes.
    /// </summary>
    public class CosineVectorStore : IVectorStore
    {
        // Structure-of-Arrays layout for better cache locality
        private float[] _vectorValues = Array.Empty<float>();
        private Guid[] _vectorIds = Array.Empty<Guid>();
        private float[] _vectorMagnitudes = Array.Empty<float>();
        private readonly ReaderWriterLockSlim _lock = new();
        private readonly int _dimension;
        private int _count;
        private int _capacity;

        /// <summary>
        /// Initializes a new instance of the <see cref="CosineVectorStore"/> class with the specified dimension.
        /// </summary>
        /// <param name="dimension">The dimension of all vectors that will be stored. Must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when dimension is less than or equal to zero.</exception>
        public CosineVectorStore(int dimension)
        {
            if (dimension <= 0)
                throw new ArgumentOutOfRangeException(nameof(dimension), "Dimension must be positive.");

            _dimension = dimension;
        }

        /// <summary>
        /// Adds a vector to the store asynchronously.
        /// </summary>
        /// <param name="vector">The vector to add to the store.</param>
        /// <returns>A task that represents the asynchronous add operation.</returns>
        /// <exception cref="ArgumentException">Thrown when the vector dimension does not match the store's expected dimension.</exception>
        public async Task AddAsync(StoredVector vector)
        {
            if (vector.Values.Length != _dimension)
                throw new ArgumentException($"Vector must have {_dimension} dimensions.", nameof(vector));

            await Task.Run(() =>
            {
                _lock.EnterWriteLock();
                try
                {
                    EnsureCapacity(_count + 1);

                    // Calculate magnitude once and store it
                    float magnitude = CalculateMagnitude(vector.GetSpan());
                    
                    // Store vector data
                    int startIndex = _count * _dimension;
                    vector.Values.CopyTo(_vectorValues, startIndex);
                    _vectorIds[_count] = vector.Id;
                    _vectorMagnitudes[_count] = magnitude;
                    _count++;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            });
        }

        /// <summary>
        /// Removes a vector from the store by its ID asynchronously.
        /// </summary>
        /// <param name="id">The ID of the vector to remove.</param>
        /// <returns>A task that represents the asynchronous remove operation. Returns true if the vector was found and removed, false otherwise.</returns>
        public async Task<bool> RemoveAsync(Guid id)
        {
            return await Task.Run(() =>
            {
                _lock.EnterWriteLock();
                try
                {
                    for (int i = 0; i < _count; i++)
                    {
                        if (_vectorIds[i] == id)
                        {
                            // Move last element to this position to maintain contiguous storage
                            if (i < _count - 1)
                            {
                                int lastStartIndex = (_count - 1) * _dimension;
                                int currentStartIndex = i * _dimension;
                                
                                // Copy vector values
                                Array.Copy(_vectorValues, lastStartIndex, _vectorValues, currentStartIndex, _dimension);
                                
                                // Copy metadata
                                _vectorIds[i] = _vectorIds[_count - 1];
                                _vectorMagnitudes[i] = _vectorMagnitudes[_count - 1];
                            }
                            
                            _count--;
                            return true;
                        }
                    }

                    return false;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            });
        }

        /// <summary>
        /// Finds the most similar vectors to the query vector using cosine similarity.
        /// </summary>
        /// <param name="queryVector">The query vector to find similar vectors for.</param>
        /// <param name="count">The number of most similar vectors to return.</param>
        /// <returns>A task that represents the asynchronous search operation. Returns an enumerable of the most similar vectors.</returns>
        /// <exception cref="ArgumentException">Thrown when the query vector dimension does not match the store's expected dimension, or when count is less than or equal to zero.</exception>
        public async Task<IEnumerable<StoredVector>> FindMostSimilarAsync(float[] queryVector, int count)
        {
            if (queryVector == null || queryVector.Length == 0 || count <= 0)
                return Enumerable.Empty<StoredVector>();

            if (queryVector.Length != _dimension)
                throw new ArgumentException($"Query vector must have {_dimension} dimensions.", nameof(queryVector));

            float queryMagnitude = CalculateMagnitude(queryVector.AsSpan());
            if (queryMagnitude == 0)
                return Enumerable.Empty<StoredVector>();

            return await Task.Run(() =>
            {
                _lock.EnterReadLock();
                try
                {
                    // Use parallel processing for large datasets
                    if (_count > 1000)
                    {
                        return FindMostSimilarParallelAsync(queryVector, queryMagnitude, count);
                    }
                    else
                    {
                        return FindMostSimilarSequential(queryVector, queryMagnitude, count);
                    }
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            });
        }

        private IEnumerable<StoredVector> FindMostSimilarSequential(float[] queryVector, float queryMagnitude, int count)
        {
            MinHeap<(int index, float similarity)> minHeap = new MinHeap<(int, float)>(count);

            for (int i = 0; i < _count; i++)
            {
                int startIndex = i * _dimension;
                float similarity = CalculateCosineSimilarityOptimized(
                    queryVector.AsSpan(), 
                    queryMagnitude, 
                    _vectorValues.AsSpan(startIndex, _dimension),
                    _vectorMagnitudes[i]);
                
                minHeap.Add((i, similarity), similarity);
            }

            // Only create StoredVector objects for the top-k results
            var topK = minHeap.GetItems();
            StoredVector[] results = new StoredVector[topK.Length];
            
            for (int i = 0; i < topK.Length; i++)
            {
                int index = topK[i].index;
                int startIndex = index * _dimension;
                float[] vectorValues = new float[_dimension];
                Array.Copy(_vectorValues, startIndex, vectorValues, 0, _dimension);
                results[i] = new StoredVector(_vectorIds[index], vectorValues);
            }
            
            return results;
        }

        private IEnumerable<StoredVector> FindMostSimilarParallelAsync(float[] queryVector, float queryMagnitude, int count)
        {
            ConcurrentBag<List<(int index, float similarity)>> localResults = new ConcurrentBag<List<(int, float)>>();

            Parallel.ForEach(
                Partitioner.Create(0, _count),
                () => new List<(int, float)>(),
                (range, state, local) =>
                {
                    for (int i = range.Item1; i < range.Item2; i++)
                    {
                        if (i < _count)
                        {
                            int startIndex = i * _dimension;
                            float similarity = CalculateCosineSimilarityOptimized(
                                queryVector.AsSpan(), 
                                queryMagnitude, 
                                _vectorValues.AsSpan(startIndex, _dimension),
                                _vectorMagnitudes[i]);
                            
                            local.Add((i, similarity));
                        }
                    }

                    return local;
                },
                local => localResults.Add(local));

            // Use MinHeap for final selection
            MinHeap<(int index, float similarity)> minHeap = new MinHeap<(int, float)>(count);
            foreach (List<(int index, float similarity)> local in localResults)
            {
                foreach ((int index, float similarity) in local)
                {
                    minHeap.Add((index, similarity), similarity);
                }
            }

            // Only create StoredVector objects for the top-k results
            var topK = minHeap.GetItems();
            StoredVector[] results = new StoredVector[topK.Length];
            
            for (int i = 0; i < topK.Length; i++)
            {
                int index = topK[i].index;
                int startIndex = index * _dimension;
                float[] vectorValues = new float[_dimension];
                Array.Copy(_vectorValues, startIndex, vectorValues, 0, _dimension);
                results[i] = new StoredVector(_vectorIds[index], vectorValues);
            }
            
            return results;
        }

        /// <summary>
        /// Saves the vector store to a stream asynchronously.
        /// </summary>
        /// <param name="stream">The stream to save the vector store to.</param>
        /// <returns>A task that represents the asynchronous save operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when stream is not writable.</exception>
        public async Task SaveAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanWrite)
                throw new ArgumentException("Stream must be writable", nameof(stream));

            await Task.Run(() =>
            {
                _lock.EnterReadLock();
                try
                {
                    using BinaryWriter writer = new BinaryWriter(stream, System.Text.Encoding.UTF8, true);

                    writer.Write(_dimension);
                    writer.Write(_count);

                    for (int i = 0; i < _count; i++)
                    {
                        writer.Write(_vectorIds[i].ToByteArray());
                        writer.Write(_dimension);
                        
                        int startIndex = i * _dimension;
                        writer.Write(MemoryMarshal.AsBytes(_vectorValues.AsSpan(startIndex, _dimension)));
                        
                        writer.Write(_vectorMagnitudes[i]);
                    }
                }
                finally
                {
                    _lock.ExitReadLock();
                }
            });
        }

        /// <summary>
        /// Loads the vector store from a stream asynchronously.
        /// </summary>
        /// <param name="stream">The stream to load the vector store from.</param>
        /// <returns>A task that represents the asynchronous load operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown when stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when stream is not readable.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the loaded data has incompatible dimensions.</exception>
        public async Task LoadAsync(Stream stream)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            if (!stream.CanRead)
                throw new ArgumentException("Stream must be readable", nameof(stream));

            await Task.Run(() =>
            {
                _lock.EnterWriteLock();
                try
                {
                    using BinaryReader reader = new BinaryReader(stream, System.Text.Encoding.UTF8, true);

                    int loadedDimension = reader.ReadInt32();
                    if (loadedDimension != _dimension)
                        throw new InvalidOperationException($"Loaded vectors have dimension {loadedDimension} but store expects dimension {_dimension}.");

                    int count = reader.ReadInt32();
                    EnsureCapacity(count);

                    for (int i = 0; i < count; i++)
                    {
                        byte[] idBytes = reader.ReadBytes(16);
                        Guid id = new Guid(idBytes);

                        int vectorLength = reader.ReadInt32();
                        if (vectorLength != _dimension)
                            throw new InvalidOperationException($"Vector at index {i} has dimension {vectorLength} but store expects dimension {_dimension}.");

                        int startIndex = i * _dimension;
                        byte[] bytes = reader.ReadBytes(vectorLength * sizeof(float));
                        MemoryMarshal.Cast<byte, float>(bytes).CopyTo(_vectorValues.AsSpan(startIndex, vectorLength));

                        _vectorIds[i] = id;
                        _vectorMagnitudes[i] = reader.ReadSingle();
                    }

                    _count = count;
                }
                finally
                {
                    _lock.ExitWriteLock();
                }
            });
        }

        private void EnsureCapacity(int requiredCapacity)
        {
            if (requiredCapacity <= _capacity)
                return;

            int newCapacity = Math.Max(_capacity * 2, requiredCapacity);
            
            // Resize arrays
            Array.Resize(ref _vectorValues, newCapacity * _dimension);
            Array.Resize(ref _vectorIds, newCapacity);
            Array.Resize(ref _vectorMagnitudes, newCapacity);
            
            _capacity = newCapacity;
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
        private static float CalculateCosineSimilarityOptimized(ReadOnlySpan<float> queryVector, float queryMagnitude, ReadOnlySpan<float> storedVector, float storedMagnitude)
        {
            if (storedMagnitude == 0)
                return 0.0f;

            // Fast path for common dimensions
            if (queryVector.Length == 768)
            {
                return CalculateCosineSimilarity768Optimized(queryVector, queryMagnitude, storedVector, storedMagnitude);
            }

            float dotProduct = 0.0f;

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
                }

                // Handle remaining elements
                for (int i = vectorCount * vectorSize; i < queryVector.Length; i++)
                {
                    dotProduct += queryVector[i] * storedVector[i];
                }
            }
            else
            {
                // Fallback to standard loop
                for (int i = 0; i < queryVector.Length; i++)
                {
                    dotProduct += queryVector[i] * storedVector[i];
                }
            }

            return dotProduct / (queryMagnitude * storedMagnitude);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static float CalculateCosineSimilarity768Optimized(ReadOnlySpan<float> queryVector, float queryMagnitude, ReadOnlySpan<float> storedVector, float storedMagnitude)
        {
            if (storedMagnitude == 0)
                return 0.0f;

            // Optimized for 768-dimensional vectors (common in embeddings)
            float dotProduct = 0.0f;

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
            }

            // Handle remaining elements
            for (; i < queryVector.Length; i++)
            {
                float q = queryVector[i];
                float s = storedVector[i];
                dotProduct += q * s;
            }

            return dotProduct / (queryMagnitude * storedMagnitude);
        }
    }
}