namespace NarcoNet.Server.Utilities;

/// <summary>
///     Helper utilities for path operations
/// </summary>
public static class PathHelper
{
    /// <summary>
    ///     Convert path to Windows-style path with backslashes
    /// </summary>
    public static string ToWindowsPath(string path)
    {
        return path.Replace('/', '\\');
    }

    /// <summary>
    ///     Convert path to Unix-style path with forward slashes
    /// </summary>
    public static string ToUnixPath(string path)
    {
        return path.Replace('\\', '/');
    }
}
