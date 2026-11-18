using System.Buffers.Text;
using System.Text.Json;

namespace EasyReasy.Auth.Testing
{
    /// <summary>
    /// Reads the fields an authenticator and the browser act on out of the options JSON an endpoint
    /// returned, the way the browser's <c>PublicKeyCredential.parseCreationOptionsFromJSON()</c> and
    /// <c>parseRequestOptionsFromJSON()</c> do.
    /// </summary>
    /// <remarks>
    /// Typed options are read through the same path, from their <c>ToJson()</c>: the JSON is what an
    /// endpoint actually sends, so reading only the JSON means a test of the typed options and a test of
    /// the endpoint's body exercise the same reader.
    /// </remarks>
    internal static class WebAuthnTestOptionsReader
    {
        /// <summary>
        /// Parses an options document and hands its root object to <paramref name="read"/>.
        /// </summary>
        /// <param name="optionsJson">The options JSON.</param>
        /// <param name="parameterName">The parameter the JSON arrived in, for the exception.</param>
        /// <param name="read">Reads the fields wanted from the root object.</param>
        /// <exception cref="ArgumentException">The JSON is not an object.</exception>
        public static TOptions Read<TOptions>(string optionsJson, string parameterName, Func<JsonElement, TOptions> read)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(optionsJson);
            }
            catch (JsonException exception)
            {
                throw new ArgumentException($"The options are not JSON: {exception.Message}", parameterName, exception);
            }

            using (document)
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    throw new ArgumentException($"The options must be a JSON object; got {document.RootElement.ValueKind}.", parameterName);
                }

                try
                {
                    return read(document.RootElement);
                }
                catch (FormatException exception)
                {
                    throw new ArgumentException(exception.Message, parameterName, exception);
                }
            }
        }

        /// <summary>
        /// Reads a required object member.
        /// </summary>
        /// <exception cref="FormatException">The member is missing or not an object.</exception>
        public static JsonElement ReadRequiredObject(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Object)
            {
                throw new FormatException($"The options have no \"{name}\" object.");
            }

            return value;
        }

        /// <summary>
        /// Reads a required string member.
        /// </summary>
        /// <exception cref="FormatException">The member is missing or not a string.</exception>
        public static string ReadRequiredString(JsonElement parent, string name)
        {
            return ReadOptionalString(parent, name) ?? throw new FormatException($"The options have no \"{name}\" string.");
        }

        /// <summary>
        /// Reads an optional string member, or null when it is absent or null.
        /// </summary>
        /// <exception cref="FormatException">The member is present and neither a string nor null.</exception>
        public static string? ReadOptionalString(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                return null;
            }

            if (value.ValueKind != JsonValueKind.String)
            {
                throw new FormatException($"The options' \"{name}\" must be a string; got {value.ValueKind}.");
            }

            return value.GetString();
        }

        /// <summary>
        /// Reads a required base64url member and decodes it, as the browser does before handing the bytes to
        /// the authenticator.
        /// </summary>
        /// <exception cref="FormatException">The member is missing, not a string or not base64url.</exception>
        public static byte[] ReadRequiredBase64Url(JsonElement parent, string name)
        {
            return DecodeBase64Url(ReadRequiredString(parent, name), name);
        }

        /// <summary>
        /// Reads the credential ids of an optional array of credential descriptors, such as
        /// <c>excludeCredentials</c> or <c>allowCredentials</c>; an absent or null member reads as empty.
        /// </summary>
        /// <exception cref="FormatException">The member is not an array of descriptors with base64url ids.</exception>
        public static IReadOnlyList<byte[]> ReadCredentialIds(JsonElement parent, string name)
        {
            List<byte[]> ids = new List<byte[]>();

            foreach (JsonElement descriptor in ReadOptionalArray(parent, name))
            {
                if (descriptor.ValueKind != JsonValueKind.Object)
                {
                    throw new FormatException($"Each entry of the options' \"{name}\" must be an object; got {descriptor.ValueKind}.");
                }

                ids.Add(ReadRequiredBase64Url(descriptor, "id"));
            }

            return ids;
        }

        /// <summary>
        /// Reads the algorithms offered in <c>pubKeyCredParams</c>.
        /// </summary>
        /// <exception cref="FormatException">The member is missing or not an array of parameters with an integer <c>alg</c>.</exception>
        public static IReadOnlyList<int> ReadAlgorithms(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException($"The options have no \"{name}\" array.");
            }

            List<int> algorithms = new List<int>();
            foreach (JsonElement parameters in value.EnumerateArray())
            {
                if (parameters.ValueKind != JsonValueKind.Object
                    || !parameters.TryGetProperty("alg", out JsonElement algorithm)
                    || algorithm.ValueKind != JsonValueKind.Number
                    || !algorithm.TryGetInt32(out int algorithmValue))
                {
                    throw new FormatException($"Each entry of the options' \"{name}\" must be an object with an integer \"alg\".");
                }

                if (string.Equals(ReadOptionalString(parameters, "type"), WebAuthnWireValues.PublicKeyCredentialType, StringComparison.Ordinal))
                {
                    algorithms.Add(algorithmValue);
                }
            }

            return algorithms;
        }

        private static IEnumerable<JsonElement> ReadOptionalArray(JsonElement parent, string name)
        {
            if (!parent.TryGetProperty(name, out JsonElement value) || value.ValueKind == JsonValueKind.Null)
            {
                return Array.Empty<JsonElement>();
            }

            if (value.ValueKind != JsonValueKind.Array)
            {
                throw new FormatException($"The options' \"{name}\" must be an array; got {value.ValueKind}.");
            }

            return value.EnumerateArray().ToList();
        }

        private static byte[] DecodeBase64Url(string value, string name)
        {
            try
            {
                return Base64Url.DecodeFromChars(value);
            }
            catch (FormatException exception)
            {
                throw new FormatException($"The options' \"{name}\" is not base64url.", exception);
            }
        }
    }
}
