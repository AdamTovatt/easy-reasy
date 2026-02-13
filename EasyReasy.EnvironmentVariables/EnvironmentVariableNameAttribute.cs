namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Attribute to mark individual environment variable name constants for validation.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnvironmentVariableNameAttribute : Attribute
    {
        /// <summary>
        /// Gets the minimum length requirement for the environment variable value.
        /// </summary>
        public int MinLength { get; }

        /// <summary>
        /// Gets the optional description for this environment variable.
        /// Used when generating example content.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets whether this environment variable is optional.
        /// Optional variables are skipped during startup validation.
        /// </summary>
        public bool Optional { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentVariableNameAttribute"/> class.
        /// </summary>
        /// <param name="minLength">The minimum length requirement for the environment variable value. Defaults to 0.</param>
        /// <param name="description">Optional description for this environment variable. Used when generating example content.</param>
        /// <param name="optional">Whether this environment variable is optional. Optional variables are skipped during startup validation. Defaults to false.</param>
        public EnvironmentVariableNameAttribute(int minLength = 0, string? description = null, bool optional = false)
        {
            MinLength = minLength;
            Description = description;
            Optional = optional;
        }
    }
}
