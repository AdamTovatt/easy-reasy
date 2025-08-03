namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Extension methods for the <see cref="VariableName"/> struct.
    /// </summary>
    public static class VariableNameExtensions
    {
        /// <summary>
        /// Gets the value of the environment variable represented by this <see cref="VariableName"/>, with optional minimum length validation.
        /// </summary>
        /// <param name="variable">The environment variable name.</param>
        /// <param name="minLength">The minimum length required for the value. Defaults to 0.</param>
        /// <returns>The environment variable value.</returns>
        /// <exception cref="InvalidOperationException">Thrown if the variable is missing or does not meet the minimum length.</exception>
        public static string GetValue(this VariableName variable, int minLength = 0)
        {
            return EnvironmentVariableHelper.GetVariableValue(variable, minLength);
        }
    }
}