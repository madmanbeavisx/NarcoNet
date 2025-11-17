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
    private readonly ChangeLogService _changeLogService;

    public SyncService(ILogger<SyncService> logger, ChangeLogService changeLogService)
    {
        _logger = logger;
        _changeLogService = changeLogService;
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
#if NARCONET_DEBUG_LOGGING
        _logger.LogDebug($"HashModFilesAsync: Starting to hash files in {syncPaths.Count} sync paths");
#endif

        foreach (SyncPath syncPath in syncPaths)
        {
            string fullPath = Path.GetFullPath(syncPath.Path);
            List<string> files = await GetFilesInDirectoryAsync(baseDir, fullPath, config);
#if NARCONET_DEBUG_LOGGING
            _logger.LogDebug($"  {syncPath.Path}: Found {files.Count} files");
#endif
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

    /// <summary>
    ///     Build a filesystem snapshot from current directory state
    /// </summary>
    private async Task<FileSystemSnapshot> BuildSnapshotAsync(
        List<SyncPath> syncPaths,
        NarcoNetConfig config,
        long sequenceNumber,
        CancellationToken cancellationToken = default)
    {
        Dictionary<string, FileMetadata> files = new();
        string baseDir = Directory.GetCurrentDirectory();

        foreach (SyncPath syncPath in syncPaths)
        {
            string fullPath = Path.GetFullPath(syncPath.Path);
            List<string> fileList = await GetFilesInDirectoryAsync(baseDir, fullPath, config);

            foreach (string file in fileList)
            {
                string relativePath = Path.GetRelativePath(baseDir, file);
                string winPath = PathHelper.ToWindowsPath(relativePath);

                FileInfo fileInfo = new(file);
                bool isDirectory = fileInfo.Attributes.HasFlag(FileAttributes.Directory) || !File.Exists(file);

                string hash = "";
                long size = 0;
                DateTime lastModified = DateTime.MinValue;

                if (!isDirectory && File.Exists(file))
                {
                    // Only hash if file is not too large (optimization)
                    size = fileInfo.Length;
                    lastModified = fileInfo.LastWriteTimeUtc;
                }

                files[winPath] = new FileMetadata
                {
                    Hash = hash,
                    Size = size,
                    LastModified = lastModified,
                    IsDirectory = isDirectory
                };
            }
        }

        return new FileSystemSnapshot
        {
            Files = files,
            SequenceNumber = sequenceNumber,
            Timestamp = DateTime.UtcNow
        };
    }

    /// <summary>
    ///     Detect changes between old and new snapshots, generating changelog entries
    /// </summary>
    private async Task<List<FileChangeEntry>> DetectChangesAsync(
        FileSystemSnapshot? oldSnapshot,
        FileSystemSnapshot newSnapshot,
        long startSequence,
        CancellationToken cancellationToken = default)
    {
        List<FileChangeEntry> changes = [];
        long currentSequence = startSequence;

        Dictionary<string, FileMetadata> oldFiles = oldSnapshot?.Files ?? new Dictionary<string, FileMetadata>();
        Dictionary<string, FileMetadata> newFiles = newSnapshot.Files;

        // Find added and modified files
        foreach (var (filePath, newMetadata) in newFiles)
        {
            if (newMetadata.IsDirectory)
            {
                continue; // Skip directories
            }

            if (!oldFiles.TryGetValue(filePath, out FileMetadata? oldMetadata))
            {
                // File was added
                string hash = await HashSingleFileAsync(filePath, cancellationToken);
                changes.Add(new FileChangeEntry
                {
                    SequenceNumber = ++currentSequence,
                    Operation = ChangeOperation.Add,
                    FilePath = filePath,
                    Hash = hash,
                    Timestamp = DateTime.UtcNow,
                    FileSize = newMetadata.Size,
                    LastModified = newMetadata.LastModified
                });
            }
            else
            {
                // Check if file was modified (compare size and timestamp)
                if (oldMetadata.Size != newMetadata.Size || 
                    oldMetadata.LastModified != newMetadata.LastModified)
                {
                    string hash = await HashSingleFileAsync(filePath, cancellationToken);
                    
                    // Only add to changelog if hash actually changed
                    if (hash != oldMetadata.Hash)
                    {
                        changes.Add(new FileChangeEntry
                        {
                            SequenceNumber = ++currentSequence,
                            Operation = ChangeOperation.Modify,
                            FilePath = filePath,
                            Hash = hash,
                            Timestamp = DateTime.UtcNow,
                            FileSize = newMetadata.Size,
                            LastModified = newMetadata.LastModified
                        });
                    }
                }
            }
        }

        // Find deleted files
        foreach (var (filePath, oldMetadata) in oldFiles)
        {
            if (oldMetadata.IsDirectory)
            {
                continue; // Skip directories
            }

            if (!newFiles.ContainsKey(filePath))
            {
                changes.Add(new FileChangeEntry
                {
                    SequenceNumber = ++currentSequence,
                    Operation = ChangeOperation.Delete,
                    FilePath = filePath,
                    Hash = "",
                    Timestamp = DateTime.UtcNow,
                    FileSize = 0,
                    LastModified = DateTime.MinValue
                });
            }
        }

        return changes;
    }

    /// <summary>
    ///     Hash a single file by its relative path
    /// </summary>
    private async Task<string> HashSingleFileAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        string baseDir = Directory.GetCurrentDirectory();
        string fullPath = Path.Combine(baseDir, relativePath);

        if (!File.Exists(fullPath))
        {
            return "";
        }

        await _limiter.WaitAsync(cancellationToken);
        try
        {
            return await FileHasher.HashFileAsync(fullPath, cancellationToken);
        }
        finally
        {
            _limiter.Release();
        }
    }

    /// <summary>
    ///     Detect and log all file changes since last server run (called on startup)
    /// </summary>
    public async Task DetectStartupChangesAsync(
        List<SyncPath> syncPaths,
        NarcoNetConfig config,
        CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.UtcNow;
        _logger.LogInformation("Detecting file changes since last startup...");

        // Load existing changelog and snapshot
        FileChangeLog changeLog = await _changeLogService.LoadChangeLogAsync(cancellationToken);
        FileSystemSnapshot? lastSnapshot = await _changeLogService.LoadSnapshotAsync(cancellationToken);

        // Build current snapshot (without hashes initially for speed)
        FileSystemSnapshot currentSnapshot = await BuildSnapshotAsync(
            syncPaths, 
            config, 
            changeLog.CurrentSequence,
            cancellationToken);

        // Detect changes between snapshots
        List<FileChangeEntry> changes = await DetectChangesAsync(
            lastSnapshot,
            currentSnapshot,
            changeLog.CurrentSequence,
            cancellationToken);

        if (changes.Count > 0)
        {
            _logger.LogInformation("Detected {Count} file changes", changes.Count);
            
            // Log summary
            int added = changes.Count(c => c.Operation == ChangeOperation.Add);
            int modified = changes.Count(c => c.Operation == ChangeOperation.Modify);
            int deleted = changes.Count(c => c.Operation == ChangeOperation.Delete);
            _logger.LogInformation("Changes: {Added} added, {Modified} modified, {Deleted} deleted", 
                added, modified, deleted);

            // Append changes to changelog
            await _changeLogService.AppendChangesAsync(changes, cancellationToken);

            // Update snapshot with hashes for changed files
            foreach (var change in changes.Where(c => c.Operation != ChangeOperation.Delete))
            {
                if (currentSnapshot.Files.TryGetValue(change.FilePath, out FileMetadata? metadata))
                {
                    currentSnapshot.Files[change.FilePath] = metadata with { Hash = change.Hash };
                }
            }
        }
        else
        {
            _logger.LogInformation("No file changes detected");
        }

        // Save updated snapshot
        FileSystemSnapshot updatedSnapshot = currentSnapshot with
        {
            SequenceNumber = changeLog.CurrentSequence + changes.Count,
            Timestamp = DateTime.UtcNow
        };
        await _changeLogService.SaveSnapshotAsync(updatedSnapshot, cancellationToken);

        // Prune old changelog entries
        await _changeLogService.PruneOldEntriesAsync(30, cancellationToken);

        double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("Change detection completed in {Elapsed:F0}ms", elapsed);
    }

    /// <summary>
    ///     Perform an on-demand recheck of the configured sync paths, comparing
    ///     current filesystem state to the last saved snapshot. This is similar
    ///     to the startup detection logic but intended to be called at runtime
    ///     (e.g. from an HTTP endpoint) and returns the detected changes.
    /// </summary>
    public async Task<List<FileChangeEntry>> RecheckAsync(
        List<SyncPath> syncPaths,
        NarcoNetConfig config,
        CancellationToken cancellationToken = default)
    {
        DateTime startTime = DateTime.UtcNow;

        // Load existing changelog and snapshot
        FileChangeLog changeLog = await _changeLogService.LoadChangeLogAsync(cancellationToken);
        FileSystemSnapshot? lastSnapshot = await _changeLogService.LoadSnapshotAsync(cancellationToken);

        // Build current snapshot (without hashes initially for speed)
        FileSystemSnapshot currentSnapshot = await BuildSnapshotAsync(
            syncPaths,
            config,
            changeLog.CurrentSequence,
            cancellationToken);

        // Detect changes between snapshots
        List<FileChangeEntry> changes = await DetectChangesAsync(
            lastSnapshot,
            currentSnapshot,
            changeLog.CurrentSequence,
            cancellationToken);

        if (changes.Count > 0)
        {
            // Append changes to changelog
            await _changeLogService.AppendChangesAsync(changes, cancellationToken);

            // Update snapshot with hashes for changed files
            foreach (var change in changes.Where(c => c.Operation != ChangeOperation.Delete))
            {
                if (currentSnapshot.Files.TryGetValue(change.FilePath, out FileMetadata? metadata))
                {
                    currentSnapshot.Files[change.FilePath] = metadata with { Hash = change.Hash };
                }
            }
        }

        // Save updated snapshot
        FileSystemSnapshot updatedSnapshot = currentSnapshot with
        {
            SequenceNumber = changeLog.CurrentSequence + changes.Count,
            Timestamp = DateTime.UtcNow
        };
        await _changeLogService.SaveSnapshotAsync(updatedSnapshot, cancellationToken);

        // Prune old changelog entries
        await _changeLogService.PruneOldEntriesAsync(30, cancellationToken);

        double elapsed = (DateTime.UtcNow - startTime).TotalMilliseconds;
        _logger.LogInformation("Recheck completed in {Elapsed:F0}ms (detected {Count} changes)", elapsed, changes.Count);

        return changes;
    }
}