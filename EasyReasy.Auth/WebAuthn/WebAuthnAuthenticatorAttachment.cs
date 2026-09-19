using System.Text.Json.Serialization;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Which kind of authenticator a registration ceremony asks the browser for.
    /// </summary>
    /// <remarks>
    /// A hint to the browser's credential picker, not a security control: it is stated in the creation
    /// options and is not carried back in a way the server can verify, so it steers the user towards the
    /// right authenticator rather than constraining what may be registered.
    /// <para>
    /// There is deliberately no member meaning "either kind". Asking for no particular attachment is the
    /// absence of a preference, so the options API takes a nullable and <c>null</c> is how it is expressed —
    /// one spelling for one idea. Taking this enum by value anywhere would make <see cref="Platform"/> the
    /// silent default of every caller who did not think about it, which is the wrong way round for a
    /// feature whose subject is security keys.
    /// </para>
    /// </remarks>
    [JsonConverter(typeof(JsonStringEnumConverter<WebAuthnAuthenticatorAttachment>))]
    public enum WebAuthnAuthenticatorAttachment
    {
        /// <summary>
        /// An authenticator built into the device — Touch ID, Face ID, Windows Hello. The private key
        /// lives in the device's secure hardware and cannot be moved to another device.
        /// </summary>
        [JsonStringEnumMemberName("platform")]
        Platform,

        /// <summary>
        /// A removable authenticator — a USB, NFC or Bluetooth security key — that can be carried between
        /// devices.
        /// </summary>
        /// <remarks>
        /// The wire spelling is <c>cross-platform</c>, which no naming policy produces from the member name,
        /// so it is pinned here.
        /// </remarks>
        [JsonStringEnumMemberName("cross-platform")]
        CrossPlatform,
    }
}
