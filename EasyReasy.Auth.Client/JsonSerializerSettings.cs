using System.Text.Json;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Provides centralized JSON serializer settings for the application.
    /// </summary>
    public static class JsonSerializerSettings
    {
        private static JsonSerializerOptions? _currentOptions;

        /// <summary>
        /// Gets or sets the current JSON serializer options.
        /// If not set, returns default options created by <see cref="CreateDefaultOptions"/>.
        /// </summary>
        public static JsonSerializerOptions CurrentOptions
        {
            get => _currentOptions ??= CreateDefaultOptions();
            set => _currentOptions = value;
        }

        /// <summary>
        /// Creates default JSON serializer options with common settings.
        /// </summary>
        /// <returns>Default JSON serializer options.</returns>
        private static JsonSerializerOptions CreateDefaultOptions()
        {
            return new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false,
                PropertyNameCaseInsensitive = false,
            };
        }
    }
}
