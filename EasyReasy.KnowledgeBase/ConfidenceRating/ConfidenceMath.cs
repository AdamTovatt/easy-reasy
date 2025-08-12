namespace EasyReasy.KnowledgeBase.ConfidenceRating
{
    /// <summary>
    /// Provides mathematical operations for confidence calculations and vector operations.
    /// </summary>
    public static class ConfidenceMath
    {
        #region Vector Operations

        /// <summary>
        /// Calculates the dot product of two vectors.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The dot product of the two vectors.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either vector is null.</exception>
        /// <exception cref="ArgumentException">Thrown when vectors have different lengths.</exception>
        public static double DotProduct(float[] a, float[] b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Length != b.Length) throw new ArgumentException("Vectors must be same length.");

            double sum = 0d;
            for (int i = 0; i < a.Length; i++)
            {
                sum += a[i] * b[i];
            }
            return sum;
        }

        /// <summary>
        /// Calculates the L2 norm (magnitude) of a vector.
        /// </summary>
        /// <param name="v">The vector to calculate the norm for.</param>
        /// <returns>The L2 norm of the vector.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the vector is null.</exception>
        public static double VectorNorm(float[] v)
        {
            if (v == null) throw new ArgumentNullException(nameof(v));
            double sumSquares = 0d;
            for (int i = 0; i < v.Length; i++)
            {
                sumSquares += (double)v[i] * v[i];
            }
            return Math.Sqrt(sumSquares);
        }

        /// <summary>
        /// Normalizes a vector to unit length (L2 normalization).
        /// </summary>
        /// <param name="v">The vector to normalize.</param>
        /// <returns>A new normalized vector.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the vector is null.</exception>
        public static float[] NormalizeVector(float[] v)
        {
            if (v == null) throw new ArgumentNullException(nameof(v));
            double norm = VectorNorm(v);
            if (norm == 0d) return new float[v.Length];

            float[] result = new float[v.Length];
            double inv = 1d / norm;
            for (int i = 0; i < v.Length; i++)
            {
                result[i] = (float)(v[i] * inv);
            }
            return result;
        }

        /// <summary>
        /// Normalizes a vector to unit length in-place (L2 normalization).
        /// </summary>
        /// <param name="v">The vector to normalize in-place.</param>
        /// <exception cref="ArgumentNullException">Thrown when the vector is null.</exception>
        public static void NormalizeVectorInPlace(float[] v)
        {
            if (v == null) throw new ArgumentNullException(nameof(v));
            double norm = VectorNorm(v);
            if (norm == 0d) return;

            double inv = 1d / norm;
            for (int i = 0; i < v.Length; i++)
            {
                v[i] = (float)(v[i] * inv);
            }
        }

        /// <summary>
        /// Calculates the cosine similarity between two vectors.
        /// </summary>
        /// <param name="a">The first vector.</param>
        /// <param name="b">The second vector.</param>
        /// <returns>The cosine similarity between the vectors, ranging from -1 to 1.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either vector is null.</exception>
        /// <exception cref="ArgumentException">Thrown when vectors have different lengths.</exception>
        public static float CosineSimilarity(float[] a, float[] b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Length != b.Length) throw new ArgumentException("Vectors must be the same length.");

            double dot = 0d, na = 0d, nb = 0d;
            for (int i = 0; i < a.Length; i++)
            {
                double ai = a[i];
                double bi = b[i];
                dot += ai * bi;
                na += ai * ai;
                nb += bi * bi;
            }
            if (na == 0d || nb == 0d) return 0f;

            double denom = Math.Sqrt(na) * Math.Sqrt(nb);
            return (float)(dot / denom);
        }

        /// <summary>
        /// Calculates cosine similarity for vectors that are already L2-normalized.
        /// This is a fast path that avoids the normalization calculation.
        /// </summary>
        /// <param name="a">The first L2-normalized vector.</param>
        /// <param name="b">The second L2-normalized vector.</param>
        /// <returns>The cosine similarity between the vectors, ranging from -1 to 1.</returns>
        /// <exception cref="ArgumentNullException">Thrown when either vector is null.</exception>
        /// <exception cref="ArgumentException">Thrown when vectors have different lengths.</exception>
        public static float CosineSimilarityPreNormalized(float[] a, float[] b)
        {
            if (a == null) throw new ArgumentNullException(nameof(a));
            if (b == null) throw new ArgumentNullException(nameof(b));
            if (a.Length != b.Length) throw new ArgumentException("Vectors must be the same length.");
            return (float)DotProduct(a, b);
        }

        /// <summary>
        /// Updates a centroid vector in-place using an online algorithm for running mean.
        /// </summary>
        /// <param name="centroid">The centroid vector to update in-place.</param>
        /// <param name="nextVector">The next vector to incorporate into the centroid.</param>
        /// <param name="countBefore">The number of vectors that were used to compute the current centroid.</param>
        /// <exception cref="ArgumentNullException">Thrown when either vector is null.</exception>
        /// <exception cref="ArgumentException">Thrown when vectors have different lengths.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown when countBefore is negative.</exception>
        public static void UpdateCentroidInPlace(float[] centroid, float[] nextVector, int countBefore)
        {
            if (centroid == null) throw new ArgumentNullException(nameof(centroid));
            if (nextVector == null) throw new ArgumentNullException(nameof(nextVector));
            if (centroid.Length != nextVector.Length) throw new ArgumentException("Vectors must be the same length.");
            if (countBefore < 0) throw new ArgumentOutOfRangeException(nameof(countBefore));

            double denom = (double)countBefore + 1d;
            for (int i = 0; i < centroid.Length; i++)
            {
                centroid[i] = (float)(((centroid[i] * countBefore) + nextVector[i]) / denom);
            }
        }

        #endregion

        #region Statistics

        /// <summary>
        /// Calculates the arithmetic mean of a collection of values.
        /// </summary>
        /// <param name="values">The values to calculate the mean for.</param>
        /// <returns>The arithmetic mean of the values, or 0 if the collection is empty.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the values array is null.</exception>
        public static double CalculateMean(double[] values)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            if (values.Length == 0) return 0d;

            double sum = 0d;
            for (int i = 0; i < values.Length; i++) sum += values[i];
            return sum / values.Length;
        }

        /// <summary>
        /// Calculates the standard deviation of a collection of values.
        /// </summary>
        /// <param name="values">The values to calculate the standard deviation for.</param>
        /// <param name="sample">Whether to calculate sample standard deviation (n-1) or population standard deviation (n).</param>
        /// <returns>The standard deviation of the values, or 0 if the collection is empty or has insufficient data.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the values array is null.</exception>
        public static double CalculateStandardDeviation(double[] values, bool sample = false)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            int n = values.Length;
            if (n == 0) return 0d;
            if (sample && n < 2) return 0d;

            double mean = CalculateMean(values);
            double sumSq = 0d;
            for (int i = 0; i < n; i++)
            {
                double d = values[i] - mean;
                sumSq += d * d;
            }
            double denom = sample ? (n - 1) : n;
            return Math.Sqrt(sumSq / denom);
        }

        /// <summary>
        /// Performs min-max normalization on a collection of values, scaling them to a 0-100 range.
        /// </summary>
        /// <param name="values">The values to normalize.</param>
        /// <param name="min">The minimum value in the range.</param>
        /// <param name="max">The maximum value in the range.</param>
        /// <returns>An array of normalized values in the range 0-100.</returns>
        /// <exception cref="ArgumentNullException">Thrown when the values array is null.</exception>
        /// <exception cref="ArgumentException">Thrown when min is greater than max.</exception>
        public static double[] MinMaxNormalization(double[] values, double min, double max)
        {
            if (values == null) throw new ArgumentNullException(nameof(values));
            double[] result = new double[values.Length];

            double range = max - min;
            if (range == 0d)
            {
                for (int i = 0; i < values.Length; i++) result[i] = 50d;
                return result;
            }

            for (int i = 0; i < values.Length; i++)
            {
                double x = values[i];
                double norm01 = (x - min) / range;
                double norm0100 = norm01 * 100d;
                result[i] = Clamp(norm0100, 0d, 100d);
            }
            return result;
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Rounds a double value to the nearest integer using away-from-zero rounding.
        /// </summary>
        /// <param name="value">The value to round.</param>
        /// <returns>The rounded integer value.</returns>
        public static int RoundToInt(double value)
        {
            return (int)Math.Round(value, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Clamps a double value to be within the specified range.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum allowed value.</param>
        /// <param name="max">The maximum allowed value.</param>
        /// <returns>The clamped value.</returns>
        /// <exception cref="ArgumentException">Thrown when min is greater than max.</exception>
        public static double Clamp(double value, double min, double max)
        {
            if (min > max) throw new ArgumentException("min cannot be greater than max.");
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        /// <summary>
        /// Clamps a float value to be within the specified range.
        /// </summary>
        /// <param name="value">The value to clamp.</param>
        /// <param name="min">The minimum allowed value.</param>
        /// <param name="max">The maximum allowed value.</param>
        /// <returns>The clamped value.</returns>
        /// <exception cref="ArgumentException">Thrown when min is greater than max.</exception>
        public static float Clamp(float value, float min, float max)
        {
            if (min > max) throw new ArgumentException("min cannot be greater than max.");
            if (value < min) return min;
            if (value > max) return max;
            return value;
        }

        #endregion
    }
}
