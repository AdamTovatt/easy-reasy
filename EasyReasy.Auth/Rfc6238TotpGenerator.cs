using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace EasyReasy.Auth
{
    /// <summary>
    /// RFC 6238 (TOTP) over RFC 4226 (HOTP) with HMAC-SHA1, the algorithm every mainstream
    /// authenticator app (Google Authenticator, 1Password, Authy, …) implements by default.
    /// Implemented in-house — no third-party dependency on this security-critical path, so the
    /// validation story for the TOTP factor has clean provenance.
    /// </summary>
    /// <remarks>
    /// Pure and stateless: no clock and no persistence live here. The caller supplies the time
    /// step (so tests are deterministic) and owns replay protection via the matched step that
    /// <see cref="TryValidate"/> reports.
    /// </remarks>
    public sealed class Rfc6238TotpGenerator
    {
        /// <summary>Smallest supported code length.</summary>
        public const int MinDigits = 6;

        /// <summary>Largest supported code length.</summary>
        public const int MaxDigits = 8;

        /// <summary>Default code length — 6 digits, the authenticator-app standard.</summary>
        public const int DefaultDigits = 6;

        /// <summary>Default time-step — 30 seconds, the authenticator-app standard.</summary>
        public const int DefaultStepSeconds = 30;

        private readonly int _digits;
        private readonly int _stepSeconds;
        private readonly int _modulo;

        /// <summary>
        /// Creates a generator with the given code length and time-step. The defaults (6 digits,
        /// 30-second steps) match what mainstream authenticator apps assume.
        /// </summary>
        /// <param name="digits">Code length; must be between <see cref="MinDigits"/> and <see cref="MaxDigits"/>.</param>
        /// <param name="stepSeconds">Time-step length in seconds; must be positive.</param>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="digits"/> is outside the
        /// <see cref="MinDigits"/>–<see cref="MaxDigits"/> range, or <paramref name="stepSeconds"/> is not positive.</exception>
        public Rfc6238TotpGenerator(int digits = DefaultDigits, int stepSeconds = DefaultStepSeconds)
        {
            if (digits is < MinDigits or > MaxDigits)
            {
                throw new ArgumentOutOfRangeException(nameof(digits), digits, $"TOTP digit count must be between {MinDigits} and {MaxDigits}.");
            }
            if (stepSeconds <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(stepSeconds), stepSeconds, "TOTP step must be positive.");
            }

            _digits = digits;
            _stepSeconds = stepSeconds;
            _modulo = (int)Math.Pow(10, digits);
        }

        /// <summary>
        /// The time step for an instant: the number of whole <c>stepSeconds</c> intervals since the
        /// Unix epoch.
        /// </summary>
        /// <param name="timestamp">The instant to convert to a time step.</param>
        /// <returns>The number of whole time-step intervals between the Unix epoch and <paramref name="timestamp"/>.</returns>
        public long GetTimeStep(DateTimeOffset timestamp)
        {
            return timestamp.ToUnixTimeSeconds() / _stepSeconds;
        }

        /// <summary>
        /// Computes the code for a given secret and time step.
        /// </summary>
        /// <param name="secret">The shared secret bytes.</param>
        /// <param name="timeStep">The time step to compute the code for (see <see cref="GetTimeStep"/>).</param>
        /// <returns>The zero-padded numeric code of the configured length.</returns>
        [SuppressMessage("Security", "CA5350:Do Not Use Weak Cryptographic Algorithms",
            Justification = "HMAC-SHA1 is mandated by RFC 6238 / RFC 4226 for TOTP and is what every " +
                "mainstream authenticator app implements; it is used here for a one-time-password MAC " +
                "over a time counter, not for collision-resistant hashing.")]
        public string Generate(ReadOnlySpan<byte> secret, long timeStep)
        {
            Span<byte> counter = stackalloc byte[8];
            BinaryPrimitives.WriteInt64BigEndian(counter, timeStep);

            Span<byte> hash = stackalloc byte[HMACSHA1.HashSizeInBytes];
            HMACSHA1.HashData(secret, counter, hash);

            // RFC 4226 §5.3 dynamic truncation.
            int offset = hash[^1] & 0x0F;
            int binary =
                ((hash[offset] & 0x7F) << 24) |
                ((hash[offset + 1] & 0xFF) << 16) |
                ((hash[offset + 2] & 0xFF) << 8) |
                (hash[offset + 3] & 0xFF);

            int otp = binary % _modulo;
            return otp.ToString(CultureInfo.InvariantCulture).PadLeft(_digits, '0');
        }

        /// <summary>
        /// Validates a presented code against the steps in <c>[currentStep - window, currentStep + window]</c>
        /// (clock-skew tolerance). Comparison is constant-time. Returns the matched step in
        /// <paramref name="matchedStep"/> so the caller can enforce replay protection. RFC 6238 §5.2
        /// recommends persisting the highest accepted step and rejecting any code whose matched step is
        /// not greater than it — this also closes replay of a still-in-window neighbouring step, which
        /// per-step consumption alone would leave open.
        /// </summary>
        /// <param name="secret">The shared secret bytes.</param>
        /// <param name="code">The presented code; surrounding whitespace and spaces are ignored.</param>
        /// <param name="currentStep">The time step for "now".</param>
        /// <param name="window">Number of steps to check on each side of <paramref name="currentStep"/>.</param>
        /// <param name="matchedStep">The step the code matched, when the result is <c>true</c>.</param>
        /// <returns><c>true</c> if the code matches a step in the window; otherwise <c>false</c>.</returns>
        /// <exception cref="ArgumentOutOfRangeException"><paramref name="window"/> is negative.</exception>
        public bool TryValidate(ReadOnlySpan<byte> secret, string code, long currentStep, int window, out long matchedStep)
        {
            if (window < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(window), window, "TOTP validation window cannot be negative.");
            }

            matchedStep = 0;

            if (string.IsNullOrWhiteSpace(code))
            {
                return false;
            }

            string normalized = code.Replace(" ", string.Empty, StringComparison.Ordinal).Trim();
            if (normalized.Length != _digits || !normalized.All(char.IsAsciiDigit))
            {
                return false;
            }

            byte[] expectedBytes = Encoding.ASCII.GetBytes(normalized);

            bool matched = false;
            for (long step = currentStep - window; step <= currentStep + window; step++)
            {
                byte[] candidate = Encoding.ASCII.GetBytes(Generate(secret, step));
                // Constant-time compare; do not short-circuit the loop on first match so a timing
                // observer cannot learn which step matched.
                if (CryptographicOperations.FixedTimeEquals(candidate, expectedBytes))
                {
                    matched = true;
                    matchedStep = step;
                }
            }

            return matched;
        }
    }
}
