using System.Text.RegularExpressions;

namespace NarcoNet.Utilities;

public static class Glob
{
    private const string DotPattern = @"\.";
    private const string RestPattern = "(.+)";
    private static readonly Regex DotRe = new(@"\.", RegexOptions.Compiled);

    private static readonly Regex RestRe = new(@"\*\*$", RegexOptions.Compiled);

    private static readonly Regex GlobRe = new(@"(?:\*\*\/|\*\*|\*)", RegexOptions.Compiled);

    private static readonly Dictionary<string, string> GlobPatterns =
        new()
        {
            ["*"] = "([^/]+)", // no backslashes
            ["**"] = "(.+/)?([^/]+)", // short for "**/*"
            ["**/"] = "(.+/)?" // one or more directories
        };

    private static string MapToPattern(string str)
    {
        return GlobPatterns[str];
    }

    private static string Replace(string glob)
    {
        return GlobRe.Replace(RestRe.Replace(DotRe.Replace(glob, DotPattern), RestPattern),
            match => MapToPattern(match.Value));
    }

    private static string Join(string[] globs)
    {
        return $"(({string.Join(")|(", Array.ConvertAll(globs, Replace))}))";
    }

    public static Regex Create(object glob)
    {
        string pattern = glob is string[] globArray ? Join(globArray) : Replace((string)glob);
        return new Regex($"^{pattern}$", RegexOptions.Compiled);
    }

    public static Regex CreateNoEnd(object glob)
    {
        string pattern = glob is string[] globArray ? Join(globArray) : Replace((string)glob);
        return new Regex($"^{pattern}", RegexOptions.Compiled);
    }
}
