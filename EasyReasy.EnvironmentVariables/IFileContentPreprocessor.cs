namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Interface for preprocessing file content before parsing environment variables.
    /// </summary>
    public interface IFileContentPreprocessor
    {
        /// <summary>
        /// Preprocesses the content of a file before it's parsed for environment variables.
        /// </summary>
        /// <param name="content">The original file content.</param>
        /// <returns>The preprocessed content ready for environment variable parsing.</returns>
        string Preprocess(string content);
    }
}