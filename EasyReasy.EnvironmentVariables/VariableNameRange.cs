namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Represents a range of environment variable names with a common prefix.
    /// </summary>
    public readonly struct VariableNameRange
    {
        /// <summary>
        /// Gets the prefix for the environment variable range (e.g., "FILE_PATH").
        /// </summary>
        public string Prefix { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableNameRange"/> struct with the specified prefix.
        /// </summary>
        /// <param name="prefix">The prefix for the environment variable range.</param>
        public VariableNameRange(string prefix)
        {
            Prefix = prefix;
        }
    }
} 