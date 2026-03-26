using EasyReasy.EnvironmentVariables;

namespace EasyReasy.Metrics.IntegrationTests
{
    /// <summary>
    /// Environment variables required for metrics integration tests.
    /// </summary>
    [EnvironmentVariableNameContainer]
    public static class EnvironmentVariables
    {
        /// <summary>
        /// PostgreSQL database connection string for integration tests.
        /// </summary>
        [EnvironmentVariableName(minLength: 10)]
        public static readonly VariableName DatabaseConnectionString = new VariableName("DATABASE_CONNECTION_STRING");
    }
}
