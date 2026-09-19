namespace EasyReasy.Auth
{
    /// <summary>
    /// Guards for arguments whose type is an enum.
    /// </summary>
    internal static class EnumArgument
    {
        /// <summary>
        /// Rejects an enum value outside the declared members.
        /// </summary>
        /// <remarks>
        /// A value cast in from outside the declared set fails differently and silently depending on what
        /// reads it. Serialized through the string enum converter it is written as a number rather than
        /// rejected, so a browser is handed <c>"userVerification":99</c>; compared against a particular
        /// member it simply is not that member, so a requirement nobody declared reads as one of the
        /// settings that enforces nothing. Both are a cast turning a policy off instead of failing, which
        /// is what this makes impossible.
        /// </remarks>
        /// <typeparam name="TEnum">The enum type.</typeparam>
        /// <param name="value">The value to check.</param>
        /// <param name="parameterName">The parameter to name in the exception.</param>
        /// <exception cref="ArgumentOutOfRangeException">The value is not a declared member.</exception>
        public static void ThrowIfUndefined<TEnum>(TEnum value, string parameterName)
            where TEnum : struct, Enum
        {
            if (!Enum.IsDefined(value))
            {
                throw new ArgumentOutOfRangeException(parameterName, value, $"'{parameterName}' is not a defined {typeof(TEnum).Name} value.");
            }
        }
    }
}
