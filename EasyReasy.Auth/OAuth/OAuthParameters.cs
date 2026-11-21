using Microsoft.Extensions.Primitives;

namespace EasyReasy.Auth
{
    /// <summary>
    /// Reads OAuth request parameters from a query string or form body by RFC 6749's rules: a parameter sent without a
    /// value is treated as omitted (section 3.1), and one sent more than once makes the request invalid.
    /// </summary>
    internal sealed class OAuthParameters
    {
        private readonly IEnumerable<KeyValuePair<string, StringValues>> _values;

        /// <summary>
        /// Initializes the reader.
        /// </summary>
        /// <param name="values">The query or form collection.</param>
        public OAuthParameters(IEnumerable<KeyValuePair<string, StringValues>> values)
        {
            _values = values;
        }

        /// <summary>
        /// The name of the first parameter sent more than once, or null when none was.
        /// </summary>
        public string? FirstRepeated => _values.FirstOrDefault(pair => pair.Value.Count > 1).Key;

        /// <summary>
        /// A parameter's value, or null when it was omitted or empty. Check <see cref="FirstRepeated"/> first.
        /// </summary>
        /// <param name="name">The parameter name.</param>
        /// <returns>The value, or null.</returns>
        public string? Get(string name)
        {
            foreach (KeyValuePair<string, StringValues> pair in _values)
            {
                if (pair.Key == name)
                {
                    string? value = pair.Value.Count == 1 ? pair.Value[0] : null;
                    return string.IsNullOrEmpty(value) ? null : value;
                }
            }

            return null;
        }
    }
}
