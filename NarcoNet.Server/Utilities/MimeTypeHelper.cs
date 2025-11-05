using SPTarkov.DI.Annotations;

namespace NarcoNet.Server.Utilities;

/// <summary>
///     Helper for determining MIME types from file extensions
/// </summary>
[Injectable]
public class MimeTypeHelper
{
    private static readonly Dictionary<string, string> MimeTypes = new(StringComparer.OrdinalIgnoreCase)
    {
        { ".txt", "text/plain" },
        { ".html", "text/html" },
        { ".htm", "text/html" },
        { ".css", "text/css" },
        { ".js", "application/javascript" },
        { ".json", "application/json" },
        { ".xml", "application/xml" },
        { ".pdf", "application/pdf" },
        { ".zip", "application/zip" },
        { ".dll", "application/octet-stream" },
        { ".exe", "application/octet-stream" },
        { ".png", "image/png" },
        { ".jpg", "image/jpeg" },
        { ".jpeg", "image/jpeg" },
        { ".gif", "image/gif" },
        { ".svg", "image/svg+xml" },
        { ".ico", "image/x-icon" },
        { ".mp3", "audio/mpeg" },
        { ".wav", "audio/wav" },
        { ".mp4", "video/mp4" },
        { ".avi", "video/x-msvideo" },
        { ".bundle", "application/octet-stream" }
    };

    /// <summary>
    ///     Get the MIME type for a given file extension
    /// </summary>
    public string? GetMimeType(string extension)
    {
        if (string.IsNullOrEmpty(extension))
        {
            return null;
        }

        if (!extension.StartsWith('.'))
        {
            extension = $".{extension}";
        }

        return MimeTypes.TryGetValue(extension, out string? mimeType) ? mimeType : null;
    }
}
