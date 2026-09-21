using System.Text;

namespace EasyReasy.Auth.Client
{
    /// <summary>
    /// Turns a response body into something that can be put in a message: secrets blanked by
    /// <see cref="SecretRedaction"/>, runs of whitespace collapsed, and the length capped.
    /// </summary>
    internal static class ResponseBodySnippet
    {
        /// <summary>
        /// The maximum number of characters <see cref="Create"/> returns, not counting the <c>…</c> that says
        /// something was left out.
        /// </summary>
        internal const int MaximumLength = 256;

        /// <summary>
        /// Builds the snippet.
        /// </summary>
        /// <remarks>
        /// Redacting runs over the whole body and collapsing runs until the cap, so what this returns is bounded by
        /// the cap however large the body is, and the work is linear in the body. The body itself is bounded where
        /// it is read, by <see cref="ResponseBodyReader.MaximumCharacters"/>.
        /// </remarks>
        /// <param name="body">The body as it was read.</param>
        /// <returns>
        /// The snippet, at most <see cref="MaximumLength"/> characters plus a trailing <c>…</c> when the body
        /// continued past the cap. Empty when the body was empty or held nothing but whitespace.
        /// </returns>
        internal static string Create(string body)
        {
            return Cap(SecretRedaction.Redact(body));
        }

        /// <summary>
        /// Collapses runs of whitespace to single spaces and stops at the cap.
        /// </summary>
        /// <param name="body">The body to cap.</param>
        /// <returns>The capped, whitespace-collapsed text.</returns>
        private static string Cap(string body)
        {
            StringBuilder builder = new StringBuilder(MaximumLength + 1);
            bool sawWhitespace = false;

            foreach (char character in body)
            {
                if (char.IsWhiteSpace(character))
                {
                    sawWhitespace = true;
                    continue;
                }

                if (sawWhitespace && builder.Length > 0)
                {
                    // The separator is only worth writing if what it separates fits after it: at the cap it would
                    // end the snippet on a space, which says nothing and reads as a mistake.
                    if (builder.Length + 1 >= MaximumLength)
                    {
                        return builder.Append('…').ToString();
                    }

                    builder.Append(' ');
                }

                sawWhitespace = false;

                if (builder.Length >= MaximumLength)
                {
                    return builder.Append('…').ToString();
                }

                builder.Append(character);
            }

            return builder.ToString();
        }
    }
}
