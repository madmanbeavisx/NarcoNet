namespace NarcoNet.Updater.Tests.TestHelpers;

/// <summary>
///     Helper class for creating and cleaning up test file systems.
/// </summary>
public class TestFileSystem : IDisposable
{
    private readonly List<string> _createdDirectories =
    [
    ];
    private readonly List<string> _createdFiles =
    [
    ];

    public TestFileSystem()
    {
        RootDirectory = Path.Combine(Path.GetTempPath(), $"NarcoNetTest_{Guid.NewGuid()}");
        Directory.CreateDirectory(RootDirectory);
        _createdDirectories.Add(RootDirectory);
    }

    public string RootDirectory { get; }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(RootDirectory))
            {
                Directory.Delete(RootDirectory, true);
            }
        }
        catch
        {
            // Best effort cleanup
        }
    }

    public string CreateDirectory(string relativePath)
    {
        string fullPath = Path.Combine(RootDirectory, relativePath);
        Directory.CreateDirectory(fullPath);
        _createdDirectories.Add(fullPath);
        return fullPath;
    }

    public string CreateFile(string relativePath, string content = "")
    {
        string fullPath = Path.Combine(RootDirectory, relativePath);
        string? directory = Path.GetDirectoryName(fullPath);

        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
            _createdDirectories.Add(directory);
        }

        File.WriteAllText(fullPath, content);
        _createdFiles.Add(fullPath);
        return fullPath;
    }

    public bool FileExists(string relativePath)
    {
        string fullPath = Path.Combine(RootDirectory, relativePath);
        return File.Exists(fullPath);
    }

    public bool DirectoryExists(string relativePath)
    {
        string fullPath = Path.Combine(RootDirectory, relativePath);
        return Directory.Exists(fullPath);
    }

    public string ReadFile(string relativePath)
    {
        string fullPath = Path.Combine(RootDirectory, relativePath);
        return File.ReadAllText(fullPath);
    }

    public IEnumerable<string> GetFiles(string relativePath = "", string pattern = "*")
    {
        string fullPath = string.IsNullOrEmpty(relativePath)
            ? RootDirectory
            : Path.Combine(RootDirectory, relativePath);

        return Directory.GetFiles(fullPath, pattern, SearchOption.AllDirectories)
            .Select(f => GetRelativePath(RootDirectory, f));
    }

    private static string GetRelativePath(string relativeTo, string path)
    {
        Uri fromUri = new(AppendDirectorySeparatorChar(relativeTo));
        Uri toUri = new(path);
        Uri relativeUri = fromUri.MakeRelativeUri(toUri);
        string relativePath = Uri.UnescapeDataString(relativeUri.ToString());
        return relativePath.Replace('/', Path.DirectorySeparatorChar);
    }

    private static string AppendDirectorySeparatorChar(string path)
    {
        if (!path.EndsWith(Path.DirectorySeparatorChar.ToString()))
        {
            return path + Path.DirectorySeparatorChar;
        }
        return path;
    }
}
