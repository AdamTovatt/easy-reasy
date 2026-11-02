using Microsoft.AspNetCore.Http;

namespace EasyReasy.Auth
{
    /// <summary>
    /// The path prefixes an option lists, matched against a request path by whole segments, ignoring case, as
    /// routing does: <c>/files</c> matches <c>/files</c> and <c>/FILES/a</c>, not <c>/filesx</c>. The one home of that
    /// rule for every token fallback and for the startup check on nested prefixes.
    /// </summary>
    internal sealed class PathPrefixList
    {
        private readonly PathString[] _prefixes;

        internal PathPrefixList(IEnumerable<string> prefixes)
        {
            _prefixes = prefixes.Select(prefix => new PathString(prefix)).ToArray();
        }

        /// <summary>
        /// The prefixes, in the order listed.
        /// </summary>
        internal IReadOnlyList<PathString> Prefixes => _prefixes;

        /// <summary>
        /// Whether <paramref name="path"/> lies under any listed prefix.
        /// </summary>
        internal bool Matches(PathString path)
        {
            return _prefixes.Any(prefix => path.StartsWithSegments(prefix));
        }

        /// <summary>
        /// Finds a prefix that lies under another listed one, a prefix listed twice included.
        /// </summary>
        /// <returns><c>true</c> when such a pair exists.</returns>
        internal bool TryFindNestedPair(out PathString inner, out PathString outer)
        {
            for (int outerIndex = 0; outerIndex < _prefixes.Length; outerIndex++)
            {
                for (int innerIndex = 0; innerIndex < _prefixes.Length; innerIndex++)
                {
                    if (outerIndex != innerIndex && _prefixes[innerIndex].StartsWithSegments(_prefixes[outerIndex]))
                    {
                        inner = _prefixes[innerIndex];
                        outer = _prefixes[outerIndex];
                        return true;
                    }
                }
            }

            inner = PathString.Empty;
            outer = PathString.Empty;
            return false;
        }
    }
}
