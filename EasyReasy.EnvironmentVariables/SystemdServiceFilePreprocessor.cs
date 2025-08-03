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

                    // Handle multiple quoted variables on one line
                    // This handles cases like: Environment="VAR1=value1" "VAR2=value2"
                    List<string> variables = new List<string>();

                    // Simple parsing for quoted strings separated by spaces
                    int startIndex = 0;
                    while (startIndex < environmentValue.Length)
                    {
                        // Skip leading whitespace
                        while (startIndex < environmentValue.Length && char.IsWhiteSpace(environmentValue[startIndex]))
                            startIndex++;

                        if (startIndex >= environmentValue.Length)
                            break;

                        // Find the start of a quoted string
                        char quoteChar = environmentValue[startIndex];
                        if (quoteChar != '"' && quoteChar != '\'')
                        {
                            // Not a quoted string, skip to next space or end
                            int endIndex = environmentValue.IndexOf(' ', startIndex);
                            if (endIndex == -1)
                                endIndex = environmentValue.Length;

                            string unquotedVariable = environmentValue.Substring(startIndex, endIndex - startIndex).Trim();
                            if (unquotedVariable.Contains('='))
                                variables.Add(unquotedVariable);

                            startIndex = endIndex;
                            continue;
                        }

                        // Find the end of the quoted string
                        int endQuoteIndex = environmentValue.IndexOf(quoteChar, startIndex + 1);
                        if (endQuoteIndex == -1)
                            break; // Unmatched quote, skip this line

                        // Extract the quoted variable
                        string quotedVariable = environmentValue.Substring(startIndex + 1, endQuoteIndex - startIndex - 1);
                        if (quotedVariable.Contains('='))
                            variables.Add(quotedVariable);

                        startIndex = endQuoteIndex + 1;
                    }

                    // Add all found variables
                    foreach (string variable in variables)
                    {
                        result.AppendLine(variable);
                    }
                }
            }

            return result.ToString();
        }
    }
}