using EasyReasy.EnvironmentVariables;

namespace EasyReasy.KnowledgeBase.Tests.TestUtilities
{
    /// <summary>
    /// Environment variable configuration for Ollama integration tests.
    /// </summary>
    [EnvironmentVariableNameContainer]
    public static class OllamaTestEnvironmentVariables
    {
        /// <summary>
        /// The base URL for the Ollama server (e.g., http://localhost:11434).
        /// </summary>
        [EnvironmentVariableName(minLength: 10)]
        public static readonly VariableName OllamaBaseUrl = new VariableName("OLLAMA_BASE_URL");

        /// <summary>
        /// The API key for Ollama server authentication.
        /// </summary>
        [EnvironmentVariableName(minLength: 10)]
        public static readonly VariableName OllamaApiKey = new VariableName("OLLAMA_API_KEY");

        /// <summary>
        /// The model name to use for embeddings (e.g., llama3.1).
        /// </summary>
        [EnvironmentVariableName(minLength: 3)]
        public static readonly VariableName OllamaModelName = new VariableName("OLLAMA_MODEL_NAME");
    }
}