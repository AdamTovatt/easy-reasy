using System.Text.Json;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// The JSON serializer options the request models this client sends are built with.
    /// </summary>
    /// <remarks>
    /// Settable, so that a consumer can make the requests match a server that expects something else. That is also
    /// why it stops at the requests: <see cref="AuthResponse"/> reads with options of its own, carrying the check
    /// that a body which is not an auth response fails, and a setter here must not be able to switch that off.
    /// The wire field names are pinned by attributes on the models either way.
    /// </remarks>
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
