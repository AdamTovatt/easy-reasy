namespace EasyReasy.Auth
{
    /// <summary>
    /// The one bound on reported transports, applied where registration receives them and again where
    /// <see cref="WebAuthnOptionsGenerator"/> is handed them back, so any value registration accepted goes
    /// back unchanged and nothing else does.
    /// </summary>
    /// <remarks>
    /// Generous against reality — the specification names six, each under a dozen characters — and the
    /// point is only that a bound exists. Without one, a response inside the 64 KB envelope can carry
    /// thousands of transports or a single one tens of thousands of characters long, and the README tells
    /// applications to write this straight to a database column. It is a bound and not a vocabulary: the
    /// set of legal values grows with the spec, so an unrecognised one passes.
    /// </remarks>
    internal static class WebAuthnTransportBounds
    {
        /// <summary>Most transports accepted for one authenticator.</summary>
        internal const int MaximumCount = 16;

        /// <summary>Longest transport name accepted.</summary>
        internal const int MaximumLength = 32;

        /// <summary>
        /// Bounds a list of transports, returning a copy so a list the caller changes afterwards does not
        /// change what was checked.
        /// </summary>
        /// <param name="transports">The transports, or null.</param>
        /// <param name="parameterName">The argument to name in the exception.</param>
        /// <param name="credentialId">
        /// The credential the transports belong to, named in the message when the argument holds several,
        /// or null when the argument is the transports themselves.
        /// </param>
        /// <returns>A copy of the transports, or null when <paramref name="transports"/> is null.</returns>
        /// <exception cref="ArgumentException">The transports are too many, too long, or contain a null.</exception>
        internal static IReadOnlyList<string>? Check(IEnumerable<string>? transports, string parameterName, string? credentialId)
        {
            if (transports == null)
            {
                return null;
            }

            string[] reported = transports.ToArray();
            string owner = credentialId == null ? string.Empty : $" for credential \"{credentialId}\"";

            if (reported.Length > MaximumCount)
            {
                throw new ArgumentException(
                    $"'{parameterName}' carries {reported.Length} transports{owner}; no authenticator reports more than {MaximumCount}.",
                    parameterName);
            }

            foreach (string transport in reported)
            {
                if (transport == null)
                {
                    throw new ArgumentException($"'{parameterName}' contains a null transport{owner}.", parameterName);
                }

                if (transport.Length > MaximumLength)
                {
                    throw new ArgumentException(
                        $"'{parameterName}' contains a transport of {transport.Length} characters{owner}; none is longer than {MaximumLength}.",
                        parameterName);
                }
            }

            return reported;
        }
    }
}
