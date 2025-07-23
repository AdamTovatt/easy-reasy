namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Represents the name of an environment variable for use with EasyReasy.EnvironmentVariables.
    /// </summary>
    public readonly struct VariableName
    {
        /// <summary>
        /// Gets the environment variable name.
        /// </summary>
        public string Name { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="VariableName"/> struct with the specified name.
        /// </summary>
        /// <param name="name">The name of the environment variable.</param>
        public VariableName(string name)
        {
            Name = name;
        }

        /// <summary>
        /// Returns the environment variable name as a string.
        /// </summary>
        /// <returns>The environment variable name.</returns>
        public override string ToString() => Name;
    }
}