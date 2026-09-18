using System.Text.Json;

namespace EasyReasy.Auth.Tests
{
    /// <summary>
    /// Runs a test class under a serializer naming policy that produces something different for every
    /// unpinned property name.
    /// </summary>
    /// <remarks>
    /// <para>
    /// This is what makes a JSON contract assertion capable of failing. Under the library's own camelCase
    /// options most names come out right whether or not they are pinned, so a test asserting them passes
    /// against a type with no <c>JsonPropertyName</c> attributes at all. Under snake_case only the pinned
    /// ones survive.
    /// </para>
    /// <para>
    /// <see cref="JsonSerializerSettings.CurrentOptions"/> is global, so a class deriving from this must
    /// also carry <c>[DoNotParallelize]</c> — the attribute is not inherited, and a class running beside
    /// one of these would serialize under whichever policy happened to be installed.
    /// </para>
    /// </remarks>
    public abstract class HostileNamingPolicyTestBase
    {
        private JsonSerializerOptions _originalOptions = null!;

        /// <summary>Installs the hostile naming policy.</summary>
        [TestInitialize]
        public void SwapInAHostileNamingPolicy()
        {
            _originalOptions = JsonSerializerSettings.CurrentOptions;
            JsonSerializerSettings.CurrentOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            };
        }

        /// <summary>Puts back whatever was installed before.</summary>
        [TestCleanup]
        public void RestoreTheOriginalOptions()
        {
            JsonSerializerSettings.CurrentOptions = _originalOptions;
        }
    }
}
