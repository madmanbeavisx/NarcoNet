using BepInEx.Logging;
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

        // Create directories first
        foreach (SyncPath syncPath in enabledSyncPaths)
        {
            foreach (string dir in directoriesToCreate[syncPath.Path])
            {
                try
                {
                    Directory.CreateDirectory(dir);
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
                        serverModule.DownloadFile(
                            file,
                            syncPath.RestartRequired ? pendingUpdatesDir : Directory.GetCurrentDirectory(),
                            limiter,
                            cancellationToken
                        )
                    )
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
