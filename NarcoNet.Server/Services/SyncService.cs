using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using NarcoNet.Server.Models;
using NarcoNet.Server.Utilities;
using NarcoNet.Utilities;

using SPTarkov.DI.Annotations;

namespace NarcoNet.Server.Services;

/// <summary>
///     Service for handling file synchronization operations
/// </summary>
[Injectable]
public class SyncService
{
    private readonly SemaphoreSlim _limiter = new(1024, 1024);
    private readonly ILogger<SyncService> _logger;

    public SyncService(ILogger<SyncService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Get all files in a directory recursively, respecting exclusions
    /// </summary>
    private async Task<List<string>> GetFilesInDirectoryAsync(string baseDir, string dir, NarcoNetConfig config)
    {
        if (!Directory.Exists(dir))
        {
            _logger.LogWarning("Directory '{Dir}' does not exist", dir);
            return [];
        }

        FileInfo fileInfo = new(dir);
        if (fileInfo.Attributes.HasFlag(FileAttributes.Normal) || File.Exists(dir))
        {
            return [dir];
        }

        List<string> files =
        [
        ];
        DirectoryInfo dirInfo = new(dir);

        // Get files in current directory
        foreach (FileInfo file in dirInfo.GetFiles())
        {
            string filePath = file.FullName;
            if (IsExcluded(filePath, config.Exclusions, baseDir))
            {
                continue;
            }

            files.Add(filePath);
        }

        // Get subdirectories
        foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
        {
            string subDirPath = subDir.FullName;
            if (IsExcluded(subDirPath, config.Exclusions, baseDir))
            {
                continue;
            }

            List<string> subFiles = await GetFilesInDirectoryAsync(baseDir, subDirPath, config);
            files.AddRange(subFiles);
        }

        return files;
    }

    /// <summary>
    ///     Check if a path is excluded based on exclusion patterns
    /// </summary>
    private bool IsExcluded(string path, List<string> exclusions, string? baseDir = null)
    {
        // Convert absolute path to relative path from server root for pattern matching
        string relativePath;
        if (baseDir != null && Path.IsPathFullyQualified(path))
        {
            relativePath = Path.GetRelativePath(baseDir, path);
        }
        else
        {
            relativePath = path;
        }

        string unixPath = PathHelper.ToUnixPath(relativePath);
        return exclusions.Any(pattern => GlobMatcher.Matches(unixPath, pattern));
    }

    /// <summary>
    ///     Build a ModFile object for a given file path
    /// </summary>
    private async Task<ModFile> BuildModFileAsync(string file, CancellationToken cancellationToken = default)
    {
        FileInfo fileInfo = new(file);
        if (fileInfo.Attributes.HasFlag(FileAttributes.Directory))
        {
            return new ModFile("", true);
        }

        var retryCount = 0;
        while (true)
        {
            try
            {
                await _limiter.WaitAsync(cancellationToken);
                try
                {
                    string hash = await FileHasher.HashFileAsync(file, cancellationToken);
                    return new ModFile(hash);
                }
                finally
                {
                    _limiter.Release();
                }
            }
            catch (IOException) when (retryCount < 5)
            {
                _logger.LogDebug("File '{File}' is locked, retrying... (Attempt {RetryCount}/5)", file, retryCount);
                await Task.Delay(500, cancellationToken);
                retryCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading file '{File}'", file);
                throw new InvalidOperationException($"NarcoNet: Error reading '{file}'", ex);
            }
        }
    }

    /// <summary>
    ///     Hash all files in the configured sync paths
    /// </summary>
    public async Task<Dictionary<string, Dictionary<string, ModFile>>> HashModFilesAsync(
        List<SyncPath> syncPaths,
        NarcoNetConfig config,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, Dictionary<string, ModFile>> result = new();
        ConcurrentDictionary<string, byte> processedFiles = new();
        DateTime startTime = DateTime.UtcNow;

        string baseDir = Directory.GetCurrentDirectory();

        foreach (SyncPath syncPath in syncPaths)
        {
            string fullPath = Path.GetFullPath(syncPath.Path);
            List<string> files = await GetFilesInDirectoryAsync(baseDir, fullPath, config);
            ConcurrentDictionary<string, ModFile> filesResult = new();

            // Process files in parallel
            await Parallel.ForEachAsync(files, cancellationToken, async (file, ct) =>
            {
                // Convert absolute path to relative path from server root
                string relativePath = Path.GetRelativePath(baseDir, file);
                string winPath = PathHelper.ToWindowsPath(relativePath);
                if (processedFiles.TryAdd(winPath, 0))
                {
                    ModFile modFile = await BuildModFileAsync(file, ct);
                    filesResult[winPath] = modFile;
                }
            });

            // Use the original syncPath.Path as dictionary key to match client expectations
            result[PathHelper.ToWindowsPath(syncPath.Path)] = new Dictionary<string, ModFile>(filesResult);
        }

        double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogDebug("Hashed {Count} files in {Elapsed:F0}ms", processedFiles.Count, elapsed);

        return result;
    }

    /// <summary>
    ///     Sanitize a download path to ensure it's within allowed sync paths
    /// </summary>
    public string SanitizeDownloadPath(string file, List<SyncPath> syncPaths)
    {
        string baseDir = Directory.GetCurrentDirectory();
        string normalized = Path.GetFullPath(Path.Combine(baseDir, file));

        foreach (SyncPath syncPath in syncPaths)
        {
            string fullPath = Path.GetFullPath(Path.Combine(baseDir, syncPath.Path));

            // Check if the normalized file path is within the sync path
            // GetRelativePath returns a path without ".." if the file is within the base
            string relativePath = Path.GetRelativePath(fullPath, normalized);
            if (!relativePath.StartsWith("..") && !Path.IsPathRooted(relativePath))
            {
                return normalized;
            }
        }

        throw new UnauthorizedAccessException("Path must match one of the configured sync paths");
    }
}
