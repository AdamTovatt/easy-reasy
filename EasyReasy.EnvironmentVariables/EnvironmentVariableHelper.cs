﻿using System.Reflection;
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
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Environment variables file not found: {filePath}");
            }

            string[] lines = File.ReadAllLines(filePath);
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
    }
}