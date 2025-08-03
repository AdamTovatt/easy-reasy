using System.Text;

namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Preprocessor for Linux systemd service files that extracts Environment= lines
    /// and converts them to standard environment variable format.
    /// </summary>
    public class SystemdServiceFilePreprocessor : IFileContentPreprocessor
    {
        /// <summary>
        /// Preprocesses systemd service file content by extracting Environment= lines
        /// and converting them to standard environment variable format.
        /// </summary>
        /// <param name="content">The original systemd service file content.</param>
        /// <returns>Preprocessed content with only Environment= lines converted to VAR=value format.</returns>
        public string Preprocess(string content)
        {
            if (string.IsNullOrWhiteSpace(content))
                return string.Empty;

            StringBuilder result = new StringBuilder();
            string[] lines = content.Split('\n');

            foreach (string line in lines)
            {
                string trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) ||
                    trimmedLine.StartsWith("#") ||
                    trimmedLine.StartsWith("//"))
                {
                    continue;
                }

                                // Look for Environment= lines
                if (trimmedLine.StartsWith("Environment=", StringComparison.OrdinalIgnoreCase))
                {
                    // Extract the value after "Environment="
                    string environmentValue = trimmedLine.Substring("Environment=".Length).Trim();
                    
                    // In systemd service files, Environment= lines contain KEY=value format
                    // Each line can contain one environment variable
                    if (environmentValue.Contains('='))
                    {
                        result.AppendLine(environmentValue);
                    }
                }
            }

            return result.ToString();
        }
    }
}