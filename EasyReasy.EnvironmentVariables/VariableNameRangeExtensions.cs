using System;
using System.Collections.Generic;

namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Extension methods for <see cref="VariableNameRange"/>.
    /// </summary>
    public static class VariableNameRangeExtensions
    {
        /// <summary>
        /// Gets all values for environment variables in the specified range (e.g., FILE_PATH_01, FILE_PATH_02, ...).
        /// </summary>
        /// <param name="range">The variable name range.</param>
        /// <returns>A list of values for all found variables in the range.</returns>
        public static List<string> GetAllValues(this VariableNameRange range)
        {
            return EnvironmentVariableHelper.GetAllVariableValuesInRange(range);
        }
    }
} 