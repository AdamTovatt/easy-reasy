using System.Text.Json;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Reads named fields out of a <see cref="JsonElement"/>, insisting each one is present and of the kind
    /// it is being read as, and reporting a field that is not through an exception the caller chooses.
    /// </summary>
    /// <remarks>
    /// The same JSON arrives at this library on two paths that must fail differently — a browser response
    /// posted to an endpoint, where a missing field is a malformed request, and collected client data inside
    /// a ceremony, where it is a verification failure carrying a reason. That difference is the exception,
    /// not the reading, so the reading lives here once and each caller supplies the failure it means.
    /// </remarks>
    internal static class JsonFieldReader
    {
        /// <summary>
        /// Reads a required, non-empty string property.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <param name="onFailure">Builds the exception to throw, from the message describing the problem.</param>
        /// <returns>The property's value.</returns>
        public static string ReadRequiredString(JsonElement element, string propertyName, string what, Func<string, Exception> onFailure)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.String)
            {
                throw onFailure($"{what} has no string \"{propertyName}\".");
            }

            string value = property.GetString()!;
            if (value.Length == 0)
            {
                // Present and empty is neither absent nor malformed, and saying so is what keeps the failure
                // from being reported further down as whatever the empty value then fails to be.
                throw onFailure($"{what} has an empty \"{propertyName}\".");
            }

            return value;
        }

        /// <summary>
        /// Reads an optional string property, returning null when it is absent or null.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <param name="onFailure">Builds the exception to throw, from the message describing the problem.</param>
        /// <returns>The property's value, or null.</returns>
        public static string? ReadOptionalString(JsonElement element, string propertyName, string what, Func<string, Exception> onFailure)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.ValueKind != JsonValueKind.String)
            {
                throw onFailure($"{what}'s \"{propertyName}\" is not a string.");
            }

            return property.GetString();
        }

        /// <summary>
        /// Reads an optional boolean property, returning <paramref name="valueWhenAbsent"/> when it is
        /// absent or null.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <param name="valueWhenAbsent">What an absent property means.</param>
        /// <param name="onFailure">Builds the exception to throw, from the message describing the problem.</param>
        /// <returns>The property's value, or <paramref name="valueWhenAbsent"/>.</returns>
        public static bool ReadOptionalBoolean(JsonElement element, string propertyName, string what, bool valueWhenAbsent, Func<string, Exception> onFailure)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return valueWhenAbsent;
            }

            if (property.ValueKind != JsonValueKind.True && property.ValueKind != JsonValueKind.False)
            {
                throw onFailure($"{what}'s \"{propertyName}\" is not a boolean.");
            }

            return property.ValueKind == JsonValueKind.True;
        }

        /// <summary>
        /// Reads an optional array of strings, returning null when it is absent or null.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <param name="onFailure">Builds the exception to throw, from the message describing the problem.</param>
        /// <returns>The property's values, or null.</returns>
        public static IReadOnlyList<string>? ReadOptionalStringArray(JsonElement element, string propertyName, string what, Func<string, Exception> onFailure)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (property.ValueKind != JsonValueKind.Array)
            {
                throw onFailure($"{what}'s \"{propertyName}\" is not an array.");
            }

            List<string> values = new List<string>();
            foreach (JsonElement item in property.EnumerateArray())
            {
                if (item.ValueKind != JsonValueKind.String)
                {
                    throw onFailure($"{what}'s \"{propertyName}\" contains something that is not a string.");
                }

                values.Add(item.GetString()!);
            }

            return values;
        }

        /// <summary>
        /// Reads a required object property.
        /// </summary>
        /// <param name="element">The JSON object to read from.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <param name="what">The structure being read, named as it reads in a message.</param>
        /// <param name="onFailure">Builds the exception to throw, from the message describing the problem.</param>
        /// <returns>The property's value.</returns>
        public static JsonElement ReadRequiredObject(JsonElement element, string propertyName, string what, Func<string, Exception> onFailure)
        {
            if (!element.TryGetProperty(propertyName, out JsonElement property) || property.ValueKind != JsonValueKind.Object)
            {
                // The kind is checked before anything is read out of it, because reading a property off a
                // JSON string throws InvalidOperationException — which is not the failure either caller means.
                throw onFailure($"{what} has no object \"{propertyName}\".");
            }

            return property;
        }
    }
}
