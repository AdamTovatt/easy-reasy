using System.Buffers.Binary;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Runtime.CompilerServices;
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
    /// Stateless: no clock and no persistence live here. The caller supplies the time step (so tests
    /// are deterministic) and owns replay protection via the matched step that
    /// <see cref="TryValidate"/> reports. Every member is a pure function of its arguments except
    /// <see cref="GenerateSecret"/>, whose whole job is to draw from the system CSPRNG.
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

        // 160-bit secret — RFC 4226 §4 requires at least 128 bits and recommends 160, the length of
        // an HMAC-SHA1 output, which is what authenticator apps assume. Not a knob: a shorter secret
        // is weaker, a longer one buys no strength an HMAC-SHA1 key can use, and validation never
        // consults the length either way, so the choice is the library's to make once.
        private const int SecretSizeBytes = 20;

        // The label in the otpauth:// URI is "issuer:accountName", so the colon is the separator
        // authenticator apps split on.
        private const char LabelSeparator = ':';

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
        /// Generates a new shared secret: 20 cryptographically random bytes.
        /// </summary>
        /// <remarks>
        /// The size is fixed rather than a parameter: unlike the digit count and the time-step, it is
        /// not something validation ever consults, so it is knowledge this library owns rather than
        /// configuration a caller supplies.
        /// </remarks>
        /// <returns>A fresh 20-byte secret. Zero it with
        /// <see cref="System.Security.Cryptography.CryptographicOperations.ZeroMemory"/> once it is
        /// encrypted for storage and handed to the user.</returns>
        public static byte[] GenerateSecret()
        {
            return RandomNumberGenerator.GetBytes(SecretSizeBytes);
        }

        /// <summary>
        /// Builds the Key URI Format provisioning string (<c>otpauth://totp/…</c>) an authenticator
        /// app consumes, typically rendered as a QR code during enrollment.
        /// </summary>
        /// <remarks>
        /// The URI advertises <em>this instance's</em> digit count and time-step, so those cannot
        /// disagree with the validation this same instance performs. A hand-built URI can: it names
        /// the defaults while the generator was constructed with other values, and the mismatch
        /// surfaces only as codes that never match. The third parameter an authenticator app reads,
        /// <c>algorithm</c>, agrees for a different reason — this class is HMAC-SHA1 and offers no
        /// choice of anything else.
        /// </remarks>
        /// <param name="issuer">The service name shown by the authenticator app; also emitted as the
        /// <c>issuer</c> parameter.</param>
        /// <param name="accountName">The account the secret belongs to, commonly an email address.</param>
        /// <param name="secret">The shared secret bytes, base32-encoded into the URI.</param>
        /// <returns>The provisioning URI.</returns>
        /// <exception cref="ArgumentNullException"><paramref name="issuer"/> or <paramref name="accountName"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException"><paramref name="issuer"/> or <paramref name="accountName"/> is
        /// empty or whitespace, either of them contains a <c>:</c> (the label separator — a colon inside a
        /// component silently corrupts how an authenticator app splits the label), or
        /// <paramref name="secret"/> is empty.</exception>
        public string BuildProvisioningUri(string issuer, string accountName, ReadOnlySpan<byte> secret)
        {
            ValidateLabelComponent(issuer, "issuer");
            ValidateLabelComponent(accountName, "account name");

            if (secret.IsEmpty)
            {
                throw new ArgumentException("A TOTP secret cannot be empty.", nameof(secret));
            }

            string label = Uri.EscapeDataString($"{issuer}{LabelSeparator}{accountName}");
            string encodedIssuer = Uri.EscapeDataString(issuer);

            // Maintenance rule for the one parameter here that is a literal: give this class a choice
            // of algorithm and this SHA1 has to start reading it in the same change, or it becomes the
            // silent disagreement the method exists to prevent. (Why it is safe as written: the remarks.)
            return string.Create(CultureInfo.InvariantCulture, $"otpauth://totp/{label}?secret={Base32.Encode(secret)}&issuer={encodedIssuer}&algorithm=SHA1&digits={_digits}&period={_stepSeconds}");
        }

        /// <summary>
        /// Rejects a label component that would corrupt the provisioning URI: one carrying nothing, or
        /// one carrying the separator the label is split on.
        /// </summary>
        /// <remarks>
        /// Deliberately no length bound. An over-long component does not corrupt the URI — it only
        /// makes a QR code denser to scan, which fails visibly at whatever renders it, and that is
        /// where the real limit lives. Any number picked here would be invented.
        /// </remarks>
        /// <param name="value">The component to check.</param>
        /// <param name="description">How the component is named in the error message.</param>
        /// <param name="parameterName">Supplied by the compiler from the argument expression, so the
        /// caller learns which half is wrong without the call site repeating the name.</param>
        private static void ValidateLabelComponent(string value, string description, [CallerArgumentExpression(nameof(value))] string? parameterName = null)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(value, parameterName);

            if (value.Contains(LabelSeparator, StringComparison.Ordinal))
            {
                throw new ArgumentException($"A TOTP {description} cannot contain '{LabelSeparator}'; it separates the issuer from the account name in the provisioning URI's label.", parameterName);
            }
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
