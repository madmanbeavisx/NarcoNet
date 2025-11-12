using BepInEx.Logging;
using NarcoNet.Models;
using NarcoNet.Updater.Models;
using NarcoNet.Utilities;
using SPT.Common.Utils;

namespace NarcoNet.Services;

using SyncPathFileList = Dictionary<string, List<string>>;
using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

/// <summary>
///     Handles file synchronization operations for the client
/// </summary>
public class ClientSyncService(ManualLogSource logger, ServerModule serverModule) : IClientSyncService
{
    private static readonly string NarcoNetDir = Path.Combine(Directory.GetCurrentDirectory(), "NarcoNet_Data");
    private static readonly string PreviousSyncPath = Path.Combine(NarcoNetDir, "PreviousSync.json");
    private static readonly string RemovedFilesPath = Path.Combine(NarcoNetDir, "RemovedFiles.json");
    private static readonly string SyncStatePath = Path.Combine(NarcoNetDir, "SyncState.json");

    /// <inheritdoc/>
    public void AnalyzeModFiles(
        SyncPathModFiles localModFiles,
        SyncPathModFiles remoteModFiles,
        SyncPathModFiles previousSync,
        List<SyncPath> enabledSyncPaths,
        out SyncPathFileList addedFiles,
        out SyncPathFileList updatedFiles,
        out SyncPathFileList removedFiles,
        out SyncPathFileList createdDirectories)
    {
        Sync.CompareModFiles(
            Directory.GetCurrentDirectory(),
            enabledSyncPaths,
            localModFiles,
            remoteModFiles,
            previousSync,
            out addedFiles,
            out updatedFiles,
            out removedFiles,
            out createdDirectories
        );

        int addedCount = addedFiles.SelectMany(path => path.Value).Count();
        int updatedCount = updatedFiles.SelectMany(path => path.Value).Count();
        int removedCount = removedFiles.SelectMany(path => path.Value).Count();

        logger.LogDebug($"File changes detected: {addedCount} added, {updatedCount} updated, {removedCount} removed");

        LogFileChanges("Added", addedFiles);
        LogFileChanges("Updated", updatedFiles);
        LogFileChanges("Removed", removedFiles);
    }

    /// <inheritdoc/>
    public async Task SyncModsAsync(
        SyncPathFileList filesToAdd,
        SyncPathFileList filesToUpdate,
        SyncPathFileList directoriesToCreate,
        SyncPathFileList filesToRemove,
        List<SyncPath> enabledSyncPaths,
        bool deleteRemovedFiles,
        string pendingUpdatesDir,
        IProgress<(int current, int total)> progress,
        CancellationToken cancellationToken)
    {
        if (!Directory.Exists(pendingUpdatesDir))
        {
            Directory.CreateDirectory(pendingUpdatesDir);
        }

        // Delete removed files first (only for non-restart-required paths)
        if (deleteRemovedFiles)
        {
            foreach (SyncPath syncPath in enabledSyncPaths.Where(sp => !sp.RestartRequired))
            {
                if (!filesToRemove.TryGetValue(syncPath.Path, out var removeFiles))
                {
                    continue;
                }

                foreach (string file in removeFiles)
                {
                    try
                    {
                        string fullPath = Path.Combine(Directory.GetCurrentDirectory(), file);
                        if (File.Exists(fullPath))
                        {
                            File.Delete(fullPath);
                            logger.LogDebug($"Deleted file: {file}");
                        }
                    }
                    catch (Exception e)
                    {
                        logger.LogError($"Failed to delete file '{file}': {e.Message}");
                    }
                }
            }
        }
        // Also delete files for enforced paths regardless of deleteRemovedFiles setting
        foreach (SyncPath syncPath in enabledSyncPaths.Where(sp => sp.Enforced && !sp.RestartRequired))
        {
            if (!filesToRemove.TryGetValue(syncPath.Path, out var removeFiles))
            {
                continue;
            }

            foreach (string file in removeFiles)
            {
                try
                {
                    string fullPath = Path.Combine(Directory.GetCurrentDirectory(), file);
                    if (File.Exists(fullPath))
                    {
                        File.Delete(fullPath);
                        logger.LogDebug($"Deleted enforced file: {file}");
                    }
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to delete enforced file '{file}': {e.Message}");
                }
            }
        }

        // Create directories (only for non-restart-required paths)
        foreach (SyncPath syncPath in enabledSyncPaths.Where(sp => !sp.RestartRequired))
        {
            foreach (string dir in directoriesToCreate[syncPath.Path])
            {
                try
                {
                    string fullPath = Path.Combine(Directory.GetCurrentDirectory(), dir);
                    Directory.CreateDirectory(fullPath);
                }
                catch (Exception e)
                {
                    logger.LogError($"Failed to create directory: {e}");
                }
            }
        }

        // Prepare download tasks
        SemaphoreSlim limiter = new(8);
        SyncPathFileList filesToDownload = enabledSyncPaths
            .Select(syncPath => new KeyValuePair<string, List<string>>(
                syncPath.Path,
                [.. filesToAdd[syncPath.Path], .. filesToUpdate[syncPath.Path]]
            ))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        logger.LogDebug($"Downloading files...");

        List<Task> downloadTasks = enabledSyncPaths
            .SelectMany(syncPath =>
                filesToDownload.TryGetValue(syncPath.Path, out List<string>? pathFilesToDownload)
                    ? pathFilesToDownload.Select(file =>
                    {
                        // For restart-required files, download to PendingUpdates with normalized path
                        if (syncPath.RestartRequired)
                        {
                            // Strip ..\ prefix for local storage in PendingUpdates
                            string localPath = file.StartsWith("..\\", StringComparison.OrdinalIgnoreCase)
                                ? file.Substring(3)
                                : file;

                            // Still request the original file path from server
                            return serverModule.DownloadFile(
                                file,
                                pendingUpdatesDir,
                                limiter,
                                cancellationToken,
                                localPath
                            );
                        }

                        // For non-restart files, download directly to current directory
                        return serverModule.DownloadFile(
                            file,
                            Directory.GetCurrentDirectory(),
                            limiter,
                            cancellationToken
                        );
                    })
                    : []
            )
            .ToList();

        int totalDownloadCount = downloadTasks.Count;
        var downloadCount = 0;

        // Download files with progress reporting
        while (downloadTasks.Count > 0 && !cancellationToken.IsCancellationRequested)
        {
            Task task = await Task.WhenAny(downloadTasks);

            try
            {
                await task;
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException && cancellationToken.IsCancellationRequested)
                {
                    continue;
                }

                logger.LogError($"Download failed: {e.Message}");
                throw;
            }

            downloadTasks.Remove(task);
            downloadCount++;
            progress.Report((downloadCount, totalDownloadCount));
        }

        logger.LogDebug("All files downloaded successfully");
    }

    /// <inheritdoc/>
    public int GetUpdateCount(
        SyncPathFileList addedFiles,
        SyncPathFileList updatedFiles,
        SyncPathFileList removedFiles,
        SyncPathFileList createdDirectories,
        List<SyncPath> enabledSyncPaths,
        bool deleteRemovedFiles)
    {
        return enabledSyncPaths
            .Select(syncPath =>
                addedFiles[syncPath.Path].Count
                + updatedFiles[syncPath.Path].Count
                + (deleteRemovedFiles || syncPath.Enforced ? removedFiles[syncPath.Path].Count : 0)
                + createdDirectories[syncPath.Path].Count
            )
            .Sum();
    }

    /// <inheritdoc/>
    public bool IsSilentMode(
        SyncPathFileList addedFiles,
        SyncPathFileList updatedFiles,
        SyncPathFileList removedFiles,
        SyncPathFileList createdDirectories,
        List<SyncPath> enabledSyncPaths,
        bool deleteRemovedFiles,
        bool isHeadless)
    {
        if (isHeadless)
        {
            return true;
        }

        return enabledSyncPaths.All(syncPath =>
            syncPath.Silent
            || addedFiles[syncPath.Path].Count == 0
            && updatedFiles[syncPath.Path].Count == 0
            && (!(deleteRemovedFiles || syncPath.Enforced) || removedFiles[syncPath.Path].Count == 0)
            && createdDirectories[syncPath.Path].Count == 0
        );
    }

    /// <inheritdoc/>
    public bool IsRestartRequired(
        SyncPathFileList addedFiles,
        SyncPathFileList updatedFiles,
        SyncPathFileList removedFiles,
        SyncPathFileList createdDirectories,
        List<SyncPath> enabledSyncPaths,
        bool deleteRemovedFiles)
    {
        return !enabledSyncPaths.All(syncPath =>
            !syncPath.RestartRequired
            || addedFiles[syncPath.Path].Count == 0
            && updatedFiles[syncPath.Path].Count == 0
            && (!(deleteRemovedFiles || syncPath.Enforced) || removedFiles[syncPath.Path].Count == 0)
            && createdDirectories[syncPath.Path].Count == 0
        );
    }

    /// <inheritdoc/>
    public void WriteNarcoNetData(
        SyncPathModFiles remoteModFiles,
        SyncPathFileList removedFiles,
        List<SyncPath> enabledSyncPaths,
        bool deleteRemovedFiles)
    {
        VFS.WriteTextFile(PreviousSyncPath, Json.Serialize(remoteModFiles));

        if (enabledSyncPaths.Any(syncPath =>
            (deleteRemovedFiles || syncPath.Enforced) && removedFiles[syncPath.Path].Count != 0))
        {
            VFS.WriteTextFile(RemovedFilesPath, Json.Serialize(removedFiles.SelectMany(kvp => kvp.Value).ToList()));
        }
    }

    /// <summary>
    ///     Writes an update manifest for the updater exe to process
    /// </summary>
    public void WriteUpdateManifest(
        SyncPathFileList addedFiles,
        SyncPathFileList updatedFiles,
        SyncPathFileList directoriesToCreate,
        SyncPathFileList removedFiles,
        List<SyncPath> enabledSyncPaths,
        bool deleteRemovedFiles,
        string pendingUpdatesDir,
        SyncPathModFiles remoteModFiles)
    {
        UpdateManifest manifest = new()
        {
            RemoteSyncData = remoteModFiles.ToDictionary(
                kvp => kvp.Key,
                kvp => kvp.Value.ToDictionary(
                    fileKvp => fileKvp.Key,
                    fileKvp => (object)fileKvp.Value
                )
            )
        };

        // Add directory creation operations (only for restart-required paths)
        foreach (SyncPath syncPath in enabledSyncPaths.Where(sp => sp.RestartRequired))
        {
            foreach (string dir in directoriesToCreate[syncPath.Path])
            {
                manifest.Operations.Add(new UpdateOperation
                {
                    Type = OperationType.CreateDirectory,
                    Destination = dir
                });
            }
        }

        // Add file copy operations
        foreach (SyncPath syncPath in enabledSyncPaths.Where(sp => sp.RestartRequired))
        {
            foreach (string file in addedFiles[syncPath.Path].Concat(updatedFiles[syncPath.Path]))
            {
                // File paths contain ..\ because they're relative to NarcoNet_Data
                // Source is relative to PendingUpdates, Destination is relative to game root
                // Both need the ..\ stripped since updater works from game root
                string normalizedPath = file.StartsWith("..\\", StringComparison.OrdinalIgnoreCase)
                    ? file.Substring(3)
                    : file;

                manifest.Operations.Add(new UpdateOperation
                {
                    Type = OperationType.CopyFile,
                    Source = normalizedPath,  // Relative to PendingUpdates
                    Destination = normalizedPath  // Relative to game root
                });
            }
        }

        // Add file deletion operations
        foreach (SyncPath syncPath in enabledSyncPaths)
        {
            if ((deleteRemovedFiles || syncPath.Enforced) && removedFiles[syncPath.Path].Count > 0)
            {
                foreach (string file in removedFiles[syncPath.Path])
                {
                    manifest.Operations.Add(new UpdateOperation
                    {
                        Type = OperationType.DeleteFile,
                        Destination = file
                    });
                }
            }
        }

        string manifestPath = Path.Combine(NarcoNetDir, NarcoNetConstants.UpdateManifestFileName);
        VFS.WriteTextFile(manifestPath, Json.Serialize(manifest));

        logger.LogDebug($"Wrote update manifest with {manifest.Operations.Count} operations");
    }

    /// <summary>
    ///     Load the client's last known sync state
    /// </summary>
    public ClientSyncState? LoadSyncState()
    {
        if (!File.Exists(SyncStatePath))
        {
            return null;
        }

        try
        {
            string json = VFS.ReadTextFile(SyncStatePath);
            return Json.Deserialize<ClientSyncState>(json);
        }
        catch (Exception e)
        {
            logger.LogWarning($"Failed to load sync state: {e.Message}");
            return null;
        }
    }

    /// <summary>
    ///     Save the client's current sync state
    /// </summary>
    public void SaveSyncState(long sequence)
    {
        var state = new ClientSyncState
        {
            LastSequence = sequence,
            LastSyncTime = DateTime.UtcNow
        };

        try
        {
            VFS.WriteTextFile(SyncStatePath, Json.Serialize(state));
            logger.LogDebug($"Saved sync state at sequence {sequence}");
        }
        catch (Exception e)
        {
            logger.LogError($"Failed to save sync state: {e.Message}");
        }
    }

    /// <summary>
    ///     Apply incremental changes from the server changelog
    /// </summary>
    public async Task<(SyncPathFileList added, SyncPathFileList updated, SyncPathFileList removed)> 
        ApplyIncrementalChangesAsync(
            ChangesResponse changesResponse,
            List<SyncPath> enabledSyncPaths,
            CancellationToken cancellationToken = default)
    {
        logger.LogInfo($"Applying {changesResponse.Changes.Count} incremental changes from server");

        // Group changes by operation type
        SyncPathFileList addedFiles = enabledSyncPaths.ToDictionary(sp => sp.Path, _ => new List<string>());
        SyncPathFileList updatedFiles = enabledSyncPaths.ToDictionary(sp => sp.Path, _ => new List<string>());
        SyncPathFileList removedFiles = enabledSyncPaths.ToDictionary(sp => sp.Path, _ => new List<string>());

        foreach (var change in changesResponse.Changes.OrderBy(c => c.SequenceNumber))
        {
            // Find which sync path this file belongs to
            // The file path from server includes the full path, need to match against sync path
            SyncPath? matchingSyncPath = null;
            
            foreach (var sp in enabledSyncPaths)
            {
                // Normalize paths for comparison
                string normalizedSyncPath = sp.Path.Replace("/", "\\").TrimEnd('\\');
                string normalizedFilePath = change.FilePath.Replace("/", "\\");
                
                // Check if file path starts with sync path (case insensitive)
                if (normalizedFilePath.StartsWith(normalizedSyncPath, StringComparison.OrdinalIgnoreCase))
                {
                    matchingSyncPath = sp;
                    break;
                }
            }

            if (matchingSyncPath == null)
            {
                // If no direct match, just add to the first enabled sync path
                // This handles cases where the file path doesn't exactly match the sync path prefix
                logger.LogDebug($"No exact sync path match for '{change.FilePath}', adding to first enabled path");
                matchingSyncPath = enabledSyncPaths.FirstOrDefault();
                
                if (matchingSyncPath == null)
                {
                    logger.LogWarning($"No enabled sync paths available for file '{change.FilePath}'");
                    continue;
                }
            }

            switch (change.Operation)
            {
                case "Add":
                    addedFiles[matchingSyncPath.Path].Add(change.FilePath);
                    logger.LogDebug($"  + {change.FilePath}");
                    break;

                case "Modify":
                    updatedFiles[matchingSyncPath.Path].Add(change.FilePath);
                    logger.LogDebug($"  * {change.FilePath}");
                    break;

                case "Delete":
                    removedFiles[matchingSyncPath.Path].Add(change.FilePath);
                    logger.LogDebug($"  - {change.FilePath}");
                    break;
            }
        }

        int totalAdded = addedFiles.Sum(kvp => kvp.Value.Count);
        int totalUpdated = updatedFiles.Sum(kvp => kvp.Value.Count);
        int totalRemoved = removedFiles.Sum(kvp => kvp.Value.Count);

        logger.LogInfo($"Changes: {totalAdded} added, {totalUpdated} updated, {totalRemoved} removed");

        return (addedFiles, updatedFiles, removedFiles);
    }

    private void LogFileChanges(string changeType, SyncPathFileList changes)
    {
        int totalCount = changes.SelectMany(path => path.Value).Count();
        if (totalCount > 0)
        {
            foreach (KeyValuePair<string, List<string>> syncPath in changes.Where(kvp => kvp.Value.Count > 0))
            {
                logger.LogDebug($"  [{syncPath.Key}]");
                string prefix = changeType switch
                {
                    "Added" => "+",
                    "Updated" => "*",
                    "Removed" => "-",
                    _ => "?"
                };

                foreach (string file in syncPath.Value)
                {
                    logger.LogDebug($"    {prefix} {file}");
                }
            }
        }
    }
}
