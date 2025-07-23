using System;

namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Attribute to mark a field as representing a range of environment variable names with a common prefix.
    /// </summary>
    [AttributeUsage(AttributeTargets.Field)]
    public class EnvironmentVariableNameRangeAttribute : Attribute
    {
        /// <summary>
        /// Gets the minimum number of variables required in the range.
        /// </summary>
        public int MinCount { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="EnvironmentVariableNameRangeAttribute"/> class.
        /// </summary>
        /// <param name="minCount">The minimum number of variables required in the range.</param>
        public EnvironmentVariableNameRangeAttribute(int minCount = 0)
        {
            MinCount = minCount;
        }
    }
} 