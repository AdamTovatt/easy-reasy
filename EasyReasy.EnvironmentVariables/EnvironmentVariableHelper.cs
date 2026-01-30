using System.Reflection;
using System.Text;

namespace EasyReasy.EnvironmentVariables
{
    /// <summary>
    /// Helper class for environment variable validation and retrieval.
    /// </summary>
    public static class EnvironmentVariableHelper
    {
        /// <summary>
        /// Gets an environment variable value with validation.
        /// </summary>
        /// <param name="variableName">The name of the environment variable.</param>
        /// <param name="minLength">The minimum length requirement for the value.</param>
        /// <returns>The environment variable value.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the environment variable is missing or doesn't meet minimum length requirements.</exception>
        public static string GetVariableValue(
            VariableName variableName,
            int minLength = 0)
        {
            string? value = Environment.GetEnvironmentVariable(variableName.Name);

            if (string.IsNullOrWhiteSpace(value))
                throw new InvalidOperationException($"Environment variable '{variableName.Name}' is not set or is empty.");

            if (value.Length < minLength)
                throw new InvalidOperationException($"Environment variable '{variableName.Name}' has length {value.Length} but minimum required length is {minLength}.");

            return value;
        }

        /// <summary>
        /// Loads environment variables from a file and sets them using Environment.SetEnvironmentVariable.
        /// The file should be in the format:
        /// VARIABLE_NAME1=value1
        /// VARIABLE_NAME2=value2
        /// Lines starting with # or // are treated as comments and skipped.
        /// </summary>
        /// <param name="filePath">The path to the file containing environment variables.</param>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file format is invalid.</exception>
        public static void LoadVariablesFromFile(string filePath)
        {
            LoadVariablesFromFile(filePath, null);
        }

        /// <summary>
        /// Loads environment variables from a file and sets them using Environment.SetEnvironmentVariable.
        /// The file content will be preprocessed before parsing.
        /// </summary>
        /// <param name="filePath">The path to the file containing environment variables.</param>
        /// <param name="preprocessor">Optional preprocessor to transform the content before parsing.</param>
        /// <exception cref="FileNotFoundException">Thrown when the specified file does not exist.</exception>
        /// <exception cref="InvalidOperationException">Thrown when the file format is invalid.</exception>
        public static void LoadVariablesFromFile(
            string filePath,
            IFileContentPreprocessor? preprocessor)
        {
            if (!File.Exists(filePath))
            {
                string fullPath = Path.GetFullPath(filePath);
                throw new FileNotFoundException($"Environment variables file not found: {filePath} ({fullPath})");
            }

            string content = File.ReadAllText(filePath);
            LoadVariablesFromString(content, preprocessor);
        }

        /// <summary>
        /// Loads environment variables from a string and sets them using Environment.SetEnvironmentVariable.
        /// The string should be in the format:
        /// VARIABLE_NAME1=value1
        /// VARIABLE_NAME2=value2
        /// Lines starting with # or // are treated as comments and skipped.
        /// </summary>
        /// <param name="content">The string containing environment variables.</param>
        /// <param name="preprocessor">Optional preprocessor to transform the content before parsing.</param>
        /// <exception cref="InvalidOperationException">Thrown when the content format is invalid.</exception>
        public static void LoadVariablesFromString(
            string content,
            IFileContentPreprocessor? preprocessor = null)
        {
            if (string.IsNullOrWhiteSpace(content))
                return;

            // Apply preprocessing if provided
            string processedContent = preprocessor?.Preprocess(content) ?? content;

            string[] lines = processedContent.Split('\n');
            int lineNumber = 0;

            foreach (string line in lines)
            {
                lineNumber++;
                string trimmedLine = line.Trim();

                // Skip empty lines and comments
                if (string.IsNullOrWhiteSpace(trimmedLine) ||
                    trimmedLine.StartsWith("#") ||
                    trimmedLine.StartsWith("//"))
                {
                    continue;
                }

                // Parse the line for VARIABLE_NAME=value format
                int equalsIndex = trimmedLine.IndexOf('=');
                if (equalsIndex == -1)
                {
                    throw new InvalidOperationException($"Invalid format at line {lineNumber}: '{line}'. Expected format: VARIABLE_NAME=value");
                }

                string variableName = trimmedLine.Substring(0, equalsIndex).Trim();
                string value = trimmedLine.Substring(equalsIndex + 1).Trim();

                // Validate variable name is not empty
                if (string.IsNullOrWhiteSpace(variableName))
                {
                    throw new InvalidOperationException($"Invalid variable name at line {lineNumber}: '{line}'. Variable name cannot be empty.");
                }

                // Set the environment variable
                Environment.SetEnvironmentVariable(variableName, value);
            }
        }

        /// <summary>
        /// Loads environment variables from a stream and sets them using Environment.SetEnvironmentVariable.
        /// The stream should contain content in the format:
        /// VARIABLE_NAME1=value1
        /// VARIABLE_NAME2=value2
        /// Lines starting with # or // are treated as comments and skipped.
        /// </summary>
        /// <param name="stream">The stream containing environment variables.</param>
        /// <param name="preprocessor">Optional preprocessor to transform the content before parsing.</param>
        /// <exception cref="InvalidOperationException">Thrown when the stream format is invalid.</exception>
        public static void LoadVariablesFromStream(
            Stream stream,
            IFileContentPreprocessor? preprocessor = null)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            using StreamReader reader = new StreamReader(stream);
            string content = reader.ReadToEnd();
            LoadVariablesFromString(content, preprocessor);
        }

        /// <summary>
        /// Validates all environment variables defined in the specified configuration classes.
        /// This method uses reflection to find all environment variable name constants and validates that they exist
        /// and meet any minimum length requirements.
        /// </summary>
        /// <param name="configurationTypes">The types of configuration classes to validate. Each type should be marked with EnvironmentVariableNameContainerAttribute.</param>
        /// <exception cref="InvalidOperationException">Thrown when one or more required environment variables are missing or invalid.</exception>
        public static void ValidateVariableNamesIn(params Type[] configurationTypes)
        {
            StringBuilder errors = new StringBuilder();

            foreach (Type type in configurationTypes)
            {
                if (type.GetCustomAttribute<EnvironmentVariableNameContainerAttribute>() == null)
                {
                    throw new ArgumentException($"Type {type.Name} is not marked with EnvironmentVariableNameContainerAttribute.");
                }

                // Get all fields in this class
                FieldInfo[] fields = type.GetFields(BindingFlags.Public | BindingFlags.Static);

                foreach (FieldInfo field in fields)
                {
                    EnvironmentVariableNameAttribute? attribute = field.GetCustomAttribute<EnvironmentVariableNameAttribute>();

                    if (attribute != null)
                    {
                        VariableName? fieldValue = field.GetValue(null) as VariableName?;
                        if (fieldValue != null)
                        {
                            try
                            {
                                string value = GetVariableValue(fieldValue.Value, attribute.MinLength);
                            }
                            catch (InvalidOperationException ex)
                            {
                                errors.AppendLine($"---> Environment Variable '{fieldValue.Value.Name}' ({type.Name}.{field.Name}): {ex.Message}");
                            }
                        }
                    }

                    EnvironmentVariableNameRangeAttribute? rangeAttribute = field.GetCustomAttribute<EnvironmentVariableNameRangeAttribute>();

                    if (rangeAttribute != null)
                    {
                        VariableNameRange? rangeValue = field.GetValue(null) as VariableNameRange?;
                        if (rangeValue != null)
                        {
                            List<string> values = GetAllVariableValuesInRange(rangeValue.Value);
                            if (values.Count < rangeAttribute.MinCount)
                            {
                                errors.AppendLine($"---> Environment Variable Range '{rangeValue.Value.Prefix}' ({type.Name}.{field.Name}): Minimum count of {rangeAttribute.MinCount} not met. Found {values.Count}.");
                            }
                        }
                    }
                }
            }

            // Throw exception if there are any validation errors
            if (errors.Length > 0)
            {
                StringBuilder errorMessageBuilder = new StringBuilder($"Environment variable validation failed:\n{errors}");
                errorMessageBuilder.AppendLine("This validation ensures all required environment variables are properly configured before the application starts.");
                errorMessageBuilder.AppendLine("Please check your environment configuration and ensure all required variables are set.");

                throw new InvalidOperationException(errorMessageBuilder.ToString());
            }
        }

        /// <summary>
        /// Gets all values for environment variables whose names start with the specified prefix.
        /// </summary>
        /// <param name="range">The variable name range.</param>
        /// <returns>A list of values for all found variables in the range.</returns>
        public static List<string> GetAllVariableValuesInRange(VariableNameRange range)
        {
            List<string> values = new List<string>();

            foreach (System.Collections.DictionaryEntry entry in Environment.GetEnvironmentVariables())
            {
                if (entry.Key is string key && key.StartsWith(range.Prefix, StringComparison.OrdinalIgnoreCase))
                {
                    string? value = entry.Value as string;
                    if (!string.IsNullOrWhiteSpace(value))
                        values.Add(value);
                }
            }

            return values;
        }

        /// <summary>
        /// Gets example environment variable content as a string.
        /// Returns content with a comment line and default example key/value pairs.
        /// </summary>
        /// <returns>Example environment variable content string.</returns>
        public static string GetExampleContent()
        {
            return GetExampleContent("EXAMPLE_KEY1", "example_value1", "EXAMPLE_KEY2", "example_value2");
        }

        /// <summary>
        /// Gets example environment variable content as a string with one custom example.
        /// </summary>
        /// <param name="exampleKey1">The first example environment variable key.</param>
        /// <param name="exampleValue1">The first example environment variable value.</param>
        /// <returns>Example environment variable content string.</returns>
        public static string GetExampleContent(
            string exampleKey1,
            string exampleValue1)
        {
            StringBuilder content = new StringBuilder();
            content.AppendLine("# Use \"#\" to comment");
            content.AppendLine($"{exampleKey1}={exampleValue1}");
            return content.ToString();
        }

        /// <summary>
        /// Gets example environment variable content as a string with two custom examples.
        /// </summary>
        /// <param name="exampleKey1">The first example environment variable key.</param>
        /// <param name="exampleValue1">The first example environment variable value.</param>
        /// <param name="exampleKey2">The second example environment variable key.</param>
        /// <param name="exampleValue2">The second example environment variable value.</param>
        /// <returns>Example environment variable content string.</returns>
        public static string GetExampleContent(
            string exampleKey1,
            string exampleValue1,
            string exampleKey2,
            string exampleValue2)
        {
            StringBuilder content = new StringBuilder();
            content.AppendLine("# Use \"#\" to comment");
            content.AppendLine($"{exampleKey1}={exampleValue1}");
            content.AppendLine($"{exampleKey2}={exampleValue2}");
            return content.ToString();
        }

        /// <summary>
        /// Gets example environment variable content as a string based on a container type.
        /// The container type should be marked with <see cref="EnvironmentVariableNameContainerAttribute"/>.
        /// </summary>
        /// <param name="containerType">The type of the environment variable container class.</param>
        /// <param name="respectMinLength">When true, ensures example values meet minimum length requirements by appending suffixes and padding.</param>
        /// <returns>Example environment variable content string with comments for descriptions and requirements.</returns>
        /// <exception cref="ArgumentException">Thrown when the type is not marked with EnvironmentVariableNameContainerAttribute.</exception>
        public static string GetExampleContent(Type containerType, bool respectMinLength = true)
        {
            if (containerType.GetCustomAttribute<EnvironmentVariableNameContainerAttribute>() == null)
            {
                throw new ArgumentException($"Type {containerType.Name} is not marked with EnvironmentVariableNameContainerAttribute.");
            }

            StringBuilder content = new StringBuilder();
            content.AppendLine("# Use \"#\" to comment");

            FieldInfo[] fields = containerType.GetFields(BindingFlags.Public | BindingFlags.Static);
            bool isFirstVariable = true;

            foreach (FieldInfo field in fields)
            {
                EnvironmentVariableNameAttribute? attribute = field.GetCustomAttribute<EnvironmentVariableNameAttribute>();

                if (attribute != null)
                {
                    VariableName? fieldValue = field.GetValue(null) as VariableName?;
                    if (fieldValue != null)
                    {
                        if (!isFirstVariable)
                        {
                            content.AppendLine();
                        }
                        isFirstVariable = false;

                        if (!string.IsNullOrEmpty(attribute.Description))
                        {
                            content.AppendLine($"# {attribute.Description}");
                        }

                        if (attribute.MinLength > 0)
                        {
                            content.AppendLine($"# Min length: {attribute.MinLength}");
                        }

                        string variableName = fieldValue.Value.Name;
                        string exampleValue = BuildExampleValue(variableName, attribute.MinLength, respectMinLength);
                        content.AppendLine($"{variableName}={exampleValue}");
                    }
                }

                EnvironmentVariableNameRangeAttribute? rangeAttribute = field.GetCustomAttribute<EnvironmentVariableNameRangeAttribute>();

                if (rangeAttribute != null)
                {
                    VariableNameRange? rangeValue = field.GetValue(null) as VariableNameRange?;
                    if (rangeValue != null)
                    {
                        if (!isFirstVariable)
                        {
                            content.AppendLine();
                        }
                        isFirstVariable = false;

                        if (!string.IsNullOrEmpty(rangeAttribute.Description))
                        {
                            content.AppendLine($"# {rangeAttribute.Description}");
                        }

                        if (rangeAttribute.MinCount > 0)
                        {
                            content.AppendLine($"# Min count: {rangeAttribute.MinCount}");
                        }

                        string prefix = rangeValue.Value.Prefix;
                        int exampleCount = Math.Max(2, rangeAttribute.MinCount);

                        for (int i = 1; i <= exampleCount; i++)
                        {
                            content.AppendLine($"{prefix}_{i}=<YOUR_{prefix}_{i}>");
                        }
                    }
                }
            }

            return content.ToString();
        }

        /// <summary>
        /// Writes an example environment variable file with default examples.
        /// </summary>
        /// <param name="filePath">The path to the file to write.</param>
        public static void WriteExampleFile(string filePath)
        {
            string content = GetExampleContent();
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Writes an example environment variable file with one custom example.
        /// </summary>
        /// <param name="filePath">The path to the file to write.</param>
        /// <param name="exampleKey1">The first example environment variable key.</param>
        /// <param name="exampleValue1">The first example environment variable value.</param>
        public static void WriteExampleFile(
            string filePath,
            string exampleKey1,
            string exampleValue1)
        {
            string content = GetExampleContent(exampleKey1, exampleValue1);
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Writes an example environment variable file with two custom examples.
        /// </summary>
        /// <param name="filePath">The path to the file to write.</param>
        /// <param name="exampleKey1">The first example environment variable key.</param>
        /// <param name="exampleValue1">The first example environment variable value.</param>
        /// <param name="exampleKey2">The second example environment variable key.</param>
        /// <param name="exampleValue2">The second example environment variable value.</param>
        public static void WriteExampleFile(
            string filePath,
            string exampleKey1,
            string exampleValue1,
            string exampleKey2,
            string exampleValue2)
        {
            string content = GetExampleContent(exampleKey1, exampleValue1, exampleKey2, exampleValue2);
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Writes an example environment variable file based on a container type.
        /// The container type should be marked with <see cref="EnvironmentVariableNameContainerAttribute"/>.
        /// </summary>
        /// <param name="filePath">The path to the file to write.</param>
        /// <param name="containerType">The type of the environment variable container class.</param>
        /// <param name="respectMinLength">When true, ensures example values meet minimum length requirements by appending suffixes and padding.</param>
        /// <exception cref="ArgumentException">Thrown when the type is not marked with EnvironmentVariableNameContainerAttribute.</exception>
        public static void WriteExampleFile(string filePath, Type containerType, bool respectMinLength = true)
        {
            string content = GetExampleContent(containerType, respectMinLength);
            File.WriteAllText(filePath, content);
        }

        /// <summary>
        /// Writes example environment variable content to a stream with default examples.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static void WriteExampleToStream(Stream stream)
        {
            WriteExampleToStream(stream, "EXAMPLE_KEY1", "example_value1", "EXAMPLE_KEY2", "example_value2");
        }

        /// <summary>
        /// Writes example environment variable content to a stream with one custom example.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="exampleKey1">The first example environment variable key.</param>
        /// <param name="exampleValue1">The first example environment variable value.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static void WriteExampleToStream(
            Stream stream,
            string exampleKey1,
            string exampleValue1)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string content = GetExampleContent(exampleKey1, exampleValue1);
            using StreamWriter writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(content);
            writer.Flush();
        }

        /// <summary>
        /// Writes example environment variable content to a stream with two custom examples.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="exampleKey1">The first example environment variable key.</param>
        /// <param name="exampleValue1">The first example environment variable value.</param>
        /// <param name="exampleKey2">The second example environment variable key.</param>
        /// <param name="exampleValue2">The second example environment variable value.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        public static void WriteExampleToStream(
            Stream stream,
            string exampleKey1,
            string exampleValue1,
            string exampleKey2,
            string exampleValue2)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string content = GetExampleContent(exampleKey1, exampleValue1, exampleKey2, exampleValue2);
            using StreamWriter writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(content);
            writer.Flush();
        }

        /// <summary>
        /// Writes example environment variable content to a stream based on a container type.
        /// The container type should be marked with <see cref="EnvironmentVariableNameContainerAttribute"/>.
        /// </summary>
        /// <param name="stream">The stream to write to.</param>
        /// <param name="containerType">The type of the environment variable container class.</param>
        /// <param name="respectMinLength">When true, ensures example values meet minimum length requirements by appending suffixes and padding.</param>
        /// <exception cref="ArgumentNullException">Thrown when the stream is null.</exception>
        /// <exception cref="ArgumentException">Thrown when the type is not marked with EnvironmentVariableNameContainerAttribute.</exception>
        public static void WriteExampleToStream(Stream stream, Type containerType, bool respectMinLength = true)
        {
            if (stream == null)
                throw new ArgumentNullException(nameof(stream));

            string content = GetExampleContent(containerType, respectMinLength);
            using StreamWriter writer = new StreamWriter(stream, leaveOpen: true);
            writer.Write(content);
            writer.Flush();
        }

        /// <summary>
        /// Builds an example value string for an environment variable, optionally respecting minimum length requirements.
        /// </summary>
        /// <param name="variableName">The name of the environment variable.</param>
        /// <param name="minLength">The minimum length requirement.</param>
        /// <param name="respectMinLength">Whether to ensure the value meets the minimum length.</param>
        /// <returns>An example value string.</returns>
        private static string BuildExampleValue(string variableName, int minLength, bool respectMinLength)
        {
            string baseValue = $"<YOUR_{variableName}>";

            if (!respectMinLength || minLength <= 0)
            {
                return baseValue;
            }

            // Add min length suffix
            string valueWithSuffix = $"<YOUR_{variableName}_min_length_{minLength}>";

            // If still too short, pad with underscores
            if (valueWithSuffix.Length < minLength)
            {
                valueWithSuffix = valueWithSuffix.PadRight(minLength, '_');
            }

            return valueWithSuffix;
        }
    }
}