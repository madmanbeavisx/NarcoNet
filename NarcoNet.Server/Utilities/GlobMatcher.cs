using System.Text.RegularExpressions;

namespace NarcoNet.Server.Utilities;

/// <summary>
///     Utility for matching file paths against glob patterns
/// </summary>
public static class GlobMatcher
{
    /// <summary>
    ///     Convert a glob pattern to a regex pattern
    /// </summary>
    private static string GlobToRegex(string glob)
    {
        string pattern = glob
            .Replace(".", "\\.")
            .Replace("**/", "(.+/)?")
            .Replace("**", "(.+/)?([^/]+)")
            .Replace("*", "([^/]+)");

        return $"^{pattern}$";
    }

    /// <summary>
    ///     Check if a path matches a glob pattern
    /// </summary>
    public static bool Matches(string path, string globPattern)
    {
        Regex regex = new(GlobToRegex(globPattern), RegexOptions.IgnoreCase);
        return regex.IsMatch(path);
    }

    /// <summary>
    ///     Check if a path matches any of the provided glob patterns
    /// </summary>
    public static bool MatchesAny(string path, IEnumerable<string> globPatterns)
    {
        return globPatterns.Any(pattern => Matches(path, pattern));
    }
}
