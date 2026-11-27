namespace EasyReasy.Auth
{
    /// <summary>
    /// The one way a package reports an argument it validated as a list of problems (options, a request, a provider's
    /// configuration): every problem in one <see cref="ArgumentException"/>, so the caller fixes them all at once.
    /// </summary>
    public static class ArgumentProblems
    {
        /// <summary>
        /// Throws when there is any problem, listing every one under <c>Invalid {subject}:</c>.
        /// </summary>
        /// <param name="problems">One message per problem; empty when the argument is valid.</param>
        /// <param name="subject">What was validated, as the message names it, such as a type name.</param>
        /// <param name="parameterName">The parameter to name in the exception, or null when the problems span
        /// several.</param>
        /// <exception cref="ArgumentException">There is at least one problem.</exception>
        public static void ThrowIfAny(IReadOnlyList<string> problems, string subject, string? parameterName = null)
        {
            ArgumentNullException.ThrowIfNull(problems);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            if (problems.Count > 0)
            {
                throw CreateException(problems, subject, parameterName);
            }
        }

        /// <summary>
        /// The exception listing every problem under <c>Invalid {subject}:</c>, for a caller that has already found
        /// there are problems, such as a parse that returns null with them.
        /// </summary>
        /// <param name="problems">One message per problem.</param>
        /// <param name="subject">What was validated, as the message names it, such as a type name.</param>
        /// <param name="parameterName">The parameter to name in the exception, or null when the problems span
        /// several.</param>
        /// <returns>The exception to throw.</returns>
        public static ArgumentException CreateException(IReadOnlyList<string> problems, string subject, string? parameterName = null)
        {
            ArgumentNullException.ThrowIfNull(problems);
            ArgumentException.ThrowIfNullOrWhiteSpace(subject);

            return new ArgumentException($"Invalid {subject}:\n{string.Join("\n", problems)}", parameterName);
        }
    }
}
