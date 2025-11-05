using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

using NarcoNet.Server.Models;
using NarcoNet.Server.Utilities;

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
            _logger.LogWarning("NarcoNet: Drop zone '{Dir}' doesn't exist - marking it off the route, patron.", dir);
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
            if (IsExcluded(filePath, config.Exclusions))
            {
                continue;
            }

            files.Add(filePath);
        }

        // Get subdirectories
        foreach (DirectoryInfo subDir in dirInfo.GetDirectories())
        {
            string subDirPath = subDir.FullName;
            if (IsExcluded(subDirPath, config.Exclusions))
            {
                continue;
            }

            List<string> subFiles = await GetFilesInDirectoryAsync(baseDir, subDirPath, config);

            // Add empty directories
            if (subDir.GetFiles().Length == 0 && subDir.GetDirectories().Length == 0)
            {
                files.Add(subDirPath);
            }

            files.AddRange(subFiles);
        }

        // Add empty directories
        if (dirInfo.GetFiles().Length == 0 && dirInfo.GetDirectories().Length == 0)
        {
            files.Add(dir);
        }

        return files;
    }

    /// <summary>
    ///     Check if a path is excluded based on exclusion patterns
    /// </summary>
    private bool IsExcluded(string path, List<string> exclusions)
    {
        string unixPath = PathHelper.ToUnixPath(path);
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
            return new ModFile { Hash = "", IsDirectory = true };
        }

        int retryCount = 0;
        while (true)
        {
            try
            {
                await _limiter.WaitAsync(cancellationToken);
                try
                {
                    string hash = await FileHasher.HashFileAsync(file, cancellationToken);
                    return new ModFile { Hash = hash, IsDirectory = false };
                }
                finally
                {
                    _limiter.Release();
                }
            }
            catch (IOException ex) when (retryCount < 5)
            {
                _logger.LogError("Package '{File}' is locked up tight. Sending another crew member... (Attempt {RetryCount}/5)", file, retryCount);
                await Task.Delay(500, cancellationToken);
                retryCount++;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "The feds got package '{File}' - aborting mission!", file);
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

        foreach (SyncPath syncPath in syncPaths)
        {
            string fullPath = Path.GetFullPath(syncPath.Path);
            List<string> files = await GetFilesInDirectoryAsync(fullPath, fullPath, config);
            ConcurrentDictionary<string, ModFile> filesResult = new();

            // Process files in parallel
            await Parallel.ForEachAsync(files, cancellationToken, async (file, ct) =>
            {
                string winPath = PathHelper.ToWindowsPath(file);
                if (processedFiles.TryAdd(winPath, 0))
                {
                    ModFile modFile = await BuildModFileAsync(file, ct);
                    filesResult[winPath] = modFile;
                }
            });

            result[PathHelper.ToWindowsPath(fullPath)] = new Dictionary<string, ModFile>(filesResult);
        }

        double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("NarcoNet: Inventoried {Count} packages in the warehouse in {Elapsed:F0}ms", processedFiles.Count, elapsed);

        return result;
    }

    /// <summary>
    ///     Sanitize a download path to ensure it's within allowed sync paths
    /// </summary>
    public string SanitizeDownloadPath(string file, List<SyncPath> syncPaths)
    {
        string normalized = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), file));

        foreach (SyncPath syncPath in syncPaths)
        {
            string fullPath = Path.GetFullPath(syncPath.Path);
            string relativePath = Path.GetRelativePath(fullPath, normalized);
            if (!relativePath.StartsWith(".."))
            {
                return normalized;
            }
        }

        throw new UnauthorizedAccessException($"NarcoNet: Requested file '{file}' is not in an enabled sync path!");
    }
}
