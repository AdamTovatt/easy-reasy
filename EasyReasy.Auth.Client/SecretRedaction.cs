using System.Text.RegularExpressions;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// The one rule for what reads as a secret in text about to be shown, and the one way of blanking it.
    /// </summary>
    /// <remarks>
    /// A response body reported as a wrong host's answer and an <see cref="AuthResponse"/> written to a log line
    /// end up in the same place, so a name one of them blanks and the other prints is a leak either way. Both
    /// redact through here, so there is nothing to drift.
    /// <para>
    /// Written against the text rather than a parsed document on purpose: a body that fails to parse — cut off
    /// mid-token, or JSON embedded in a page — is exactly a body no parser will hand back, and it carries the
    /// value all the same. What that costs is an outer bound on what can be recognised, and the limits are stated
    /// on <see cref="Redact"/>. Over-blanking is the safe side of every one of them.
    /// </para>
    /// </remarks>
    internal static class SecretRedaction
    {
        /// <summary>
        /// What a blanked value is replaced with.
        /// </summary>
        internal const string RedactedValue = "[REDACTED]";

        /// <summary>
        /// What makes a name read as a secret: it contains one of these, in any casing and anywhere in the name, so
        /// <c>refreshToken</c>, <c>access_token</c> and <c>__RequestVerificationToken</c> are covered along with
        /// <c>token</c>.
        /// </summary>
        private static readonly string[] SecretNameFragments = new string[] { "token", "secret", "password" };

        /// <summary>
        /// Matches a JSON property and captures its name and its value. The value is a string, an array or a bare
        /// scalar, and a string's closing quote is optional: a body cut off inside a token has none, and that body
        /// carries the whole secret.
        /// </summary>
        private static readonly Regex JsonPropertyPattern = new Regex(
            @"""(?<name>(?:[^""\\]|\\.)*)""\s*:\s*(?:""(?<value>(?:[^""\\]|\\.)*)(?:""|$)|\[(?<value>[^\]]*)(?:\]|$)|(?<value>[^\s,}\]]+))",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant);

        /// <summary>
        /// Matches an HTML attribute pair where one attribute names the field and another carries its value, in
        /// either order. This is the shape a sign-in page puts a secret in — <c>&lt;input name="…Token"
        /// value="…"&gt;</c> — and a sign-in page answering where an auth endpoint was expected is the case this
        /// whole diagnosis exists for.
        /// </summary>
        private static readonly Regex HtmlAttributePairPattern = new Regex(
            @"name\s*=\s*(?:""(?<name>[^""]*)""|'(?<name>[^']*)')[^<>]*?value\s*=\s*(?:""(?<value>[^""]*)(?:""|$)|'(?<value>[^']*)(?:'|$))"
            + @"|value\s*=\s*(?:""(?<value>[^""]*)""|'(?<value>[^']*)')[^<>]*?name\s*=\s*(?:""(?<name>[^""]*)""|'(?<name>[^']*)')",
            RegexOptions.NonBacktracking | RegexOptions.CultureInvariant | RegexOptions.IgnoreCase);

        /// <summary>
        /// Replaces the value of everything that reads as a secret, so that showing the text cannot put a token
        /// into a log.
        /// </summary>
        /// <remarks>
        /// Recognises a JSON property — string, array or bare scalar, terminated or cut off — and an HTML attribute
        /// pair naming a field and carrying its value. What it does not recognise, and so does not blank:
        /// <list type="bullet">
        /// <item><description>an unquoted HTML attribute value (<c>value=abc</c>);</description></item>
        /// <item><description>a value whose property name is out of reach behind an unescaped quote earlier in the
        /// text, which only malformed JSON produces;</description></item>
        /// <item><description>a secret under a name none of <see cref="SecretNameFragments"/> appears in.</description></item>
        /// </list>
        /// <see cref="RegexOptions.NonBacktracking"/> holds each pass to a single linear scan whatever the text is,
        /// and names are tested in code rather than spelled into the patterns, which is what keeps it that way.
        /// </remarks>
        /// <param name="text">The text to redact.</param>
        /// <returns>The text with secret values replaced by <see cref="RedactedValue"/>.</returns>
        internal static string Redact(string text)
        {
            return HtmlAttributePairPattern.Replace(JsonPropertyPattern.Replace(text, RedactValue), RedactValue);
        }

        /// <summary>
        /// Replaces a match's value when its name reads as a secret, and leaves the match as it was otherwise.
        /// </summary>
        /// <remarks>
        /// Splices the value out of the match rather than rebuilding it, so whatever the pattern matched around the
        /// value — quoting, spacing, attribute order — comes back unchanged and only the value is gone.
        /// </remarks>
        /// <param name="match">The matched property or attribute pair.</param>
        /// <returns>What the match is replaced by.</returns>
        private static string RedactValue(Match match)
        {
            if (!ReadsAsSecret(match.Groups["name"].Value))
            {
                return match.Value;
            }

            Group value = match.Groups["value"];
            int valueStart = value.Index - match.Index;

            return string.Concat(
                match.Value.AsSpan(0, valueStart),
                RedactedValue,
                match.Value.AsSpan(valueStart + value.Length));
        }

        /// <summary>
        /// Whether a name reads as naming a secret.
        /// </summary>
        /// <param name="name">The property or field name to judge.</param>
        /// <returns>True when the name contains one of <see cref="SecretNameFragments"/>; otherwise, false.</returns>
        private static bool ReadsAsSecret(string name)
        {
            foreach (string fragment in SecretNameFragments)
            {
                if (name.Contains(fragment, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
