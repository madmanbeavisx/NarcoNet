using NarcoNet.Updater.Interfaces;
using NarcoNet.Updater.Models;

using Newtonsoft.Json;

namespace NarcoNet.Updater.Services;

/// <summary>
///     Handles moving packages around and disposing of evidence - the muscle of the operation.
/// </summary>
public class FileUpdateService : IFileUpdateService
{
    private readonly ILogger _logger;
    private readonly string _removedFilesManifestPath;
    private readonly string _targetDirectory;
    private readonly string _updateStagingDirectory;
    private readonly string _updateManifestPath;
    private readonly string _previousSyncPath;

    /// <summary>
    ///     Sets up the package handler with all the intel needed for the job.
    /// </summary>
    /// <param name="logger">The record keeper tracking every move.</param>
    /// <param name="updateStagingDirectory">The staging area where packages wait.</param>
    /// <param name="removedFilesManifestPath">The hit list - who's gotta go.</param>
    /// <param name="targetDirectory">The final destination for all packages.</param>
    /// <param name="updateManifestPath">The operations manifest - what needs to be done.</param>
    /// <param name="previousSyncPath">The previous sync data path.</param>
    public FileUpdateService(
        ILogger logger,
        string updateStagingDirectory,
        string removedFilesManifestPath,
        string targetDirectory,
        string updateManifestPath,
        string previousSyncPath)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _updateStagingDirectory = updateStagingDirectory ?? throw new ArgumentNullException(nameof(updateStagingDirectory));
        _removedFilesManifestPath =
            removedFilesManifestPath ?? throw new ArgumentNullException(nameof(removedFilesManifestPath));
        _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
        _updateManifestPath = updateManifestPath ?? throw new ArgumentNullException(nameof(updateManifestPath));
        _previousSyncPath = previousSyncPath ?? throw new ArgumentNullException(nameof(previousSyncPath));
    }

    /// <inheritdoc />
    public bool HasPendingUpdates()
    {
        // Check if we have a manifest file
        if (File.Exists(_updateManifestPath))
        {
            return true;
        }

        // Fall back to checking staging directory for legacy updates
        return Directory.Exists(_updateStagingDirectory) &&
               Directory.EnumerateFiles(_updateStagingDirectory, "*", SearchOption.AllDirectories).Any();
    }

    /// <inheritdoc />
    public IEnumerable<string> GetPendingUpdateFiles()
    {
        if (!HasPendingUpdates())
        {
            return
            [
            ];
        }

        return Directory.EnumerateFiles(_updateStagingDirectory, "*", SearchOption.AllDirectories)
            .Select(file => GetRelativePath(_updateStagingDirectory, file));
    }

    /// <inheritdoc />
    public async Task ApplyPendingUpdatesAsync(CancellationToken cancellationToken = default)
    {
        // Check if we have a manifest file
        if (File.Exists(_updateManifestPath))
        {
            await ApplyManifestOperationsAsync(cancellationToken);
            return;
        }

        // Fall back to legacy file-based updates
        if (!HasPendingUpdates())
        {
            _logger.LogDebug("No pending updates in staging directory.");
            return;
        }

        List<string> filesToUpdate = GetPendingUpdateFiles().ToList();
        _logger.LogDebug($"Found {filesToUpdate.Count} files to update.");

        foreach (string relativeFilePath in filesToUpdate)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await ApplySingleFileUpdateAsync(relativeFilePath, cancellationToken);
        }

        await CleanupStagingDirectoryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task DeleteRemovedFilesAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_removedFilesManifestPath))
        {
            _logger.LogDebug("No removed files manifest found.");
            return;
        }

        List<string>? removedFiles = await ReadRemovedFilesManifestAsync(cancellationToken);

        if (removedFiles == null || removedFiles.Count == 0)
        {
            _logger.LogDebug("Removed files list is empty.");
            await DeleteManifestFileAsync(cancellationToken);
            return;
        }

        _logger.LogDebug($"Deleting {removedFiles.Count} removed files.");

        foreach (string relativeFilePath in removedFiles)
        {
            cancellationToken.ThrowIfCancellationRequested();

            await DeleteSingleFileAsync(relativeFilePath, cancellationToken);
        }

        await DeleteManifestFileAsync(cancellationToken);
    }

    /// <summary>
    ///     Gets the relative path from one path to another (polyfill for .NET Framework)
    /// </summary>
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

    /// <summary>
    ///     Moves a single package from the staging area to its final drop point.
    /// </summary>
    private async Task ApplySingleFileUpdateAsync(string relativeFilePath, CancellationToken cancellationToken)
    {
        try
        {
            string sourceFilePath = Path.Combine(_updateStagingDirectory, relativeFilePath);
            string targetFilePath = Path.Combine(_targetDirectory, relativeFilePath);

            _logger.LogDebug($"Updating file: {relativeFilePath}");

            // Make sure the drop location exists
            string? targetDirectoryPath = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(targetDirectoryPath) && !Directory.Exists(targetDirectoryPath))
            {
                Directory.CreateDirectory(targetDirectoryPath);
            }

            // Drop the package at the location
            await Task.Run(() => File.Copy(sourceFilePath, targetFilePath, true), cancellationToken);

            _logger.LogDebug($"File updated: {relativeFilePath}");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"Failed to update file: {relativeFilePath}");
            throw;
        }
    }

    /// <summary>
    ///     Takes out a single target from the location.
    /// </summary>
    private async Task DeleteSingleFileAsync(string relativeFilePath, CancellationToken cancellationToken)
    {
        try
        {
            // Normalize path - strip leading ..\ if present since we're already at game root
            string normalizedPath = relativeFilePath;
            if (normalizedPath.StartsWith("..\\", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath.Substring(3);
            }
            else if (normalizedPath.StartsWith("../", StringComparison.OrdinalIgnoreCase))
            {
                normalizedPath = normalizedPath.Substring(3);
            }

            ValidateFilePath(normalizedPath);

            string targetFilePath = Path.Combine(_targetDirectory, normalizedPath);

            if (!File.Exists(targetFilePath))
            {
                _logger.LogWarning($"File already removed: {normalizedPath}");
                return;
            }

            _logger.LogDebug($"Deleting file: {normalizedPath}");

            await Task.Run(() => File.Delete(targetFilePath), cancellationToken);

            _logger.LogDebug($"File deleted: {normalizedPath}");

            // Clean up empty safe houses
            await CleanupEmptyParentDirectoriesAsync(targetFilePath, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"Failed to delete file: {relativeFilePath}");
            // Don't abort the whole operation - move on to the next target
        }
    }

    /// <summary>
    ///     Makes sure the path is legit (stays in our territory, no funny business).
    /// </summary>
    private void ValidateFilePath(string relativeFilePath)
    {
        if (Path.IsPathRooted(relativeFilePath))
        {
            throw new InvalidOperationException($"File path must be relative, not absolute: {relativeFilePath}");
        }

        string fullPath = Path.GetFullPath(Path.Combine(_targetDirectory, relativeFilePath));
        if (!fullPath.StartsWith(_targetDirectory, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"File path is outside target directory: {relativeFilePath}");
        }
    }

    /// <summary>
    ///     Reads the hit list from the file.
    /// </summary>
    private async Task<List<string>?> ReadRemovedFilesManifestAsync(CancellationToken cancellationToken)
    {
        try
        {
            string json = await Task.Run(() => File.ReadAllText(_removedFilesManifestPath), cancellationToken);
            return JsonConvert.DeserializeObject<List<string>>(json);
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to read removed files manifest");
            throw;
        }
    }

    /// <summary>
    ///     Sweeps the staging area clean after packages are delivered.
    /// </summary>
    private async Task CleanupStagingDirectoryAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug($"Cleaning staging directory: {_updateStagingDirectory}");

            await Task.Run(() => Directory.Delete(_updateStagingDirectory, true), cancellationToken);

            _logger.LogDebug("Staging directory cleaned up");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to clean staging directory");
            // Not a big deal - keep moving
        }
    }

    /// <summary>
    ///     Burns the hit list after all targets are eliminated.
    /// </summary>
    private async Task DeleteManifestFileAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug($"Deleting removed files manifest: {_removedFilesManifestPath}");

            await Task.Run(() => File.Delete(_removedFilesManifestPath), cancellationToken);

            _logger.LogDebug("Removed files manifest deleted");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to delete removed files manifest");
            // Not a big deal - keep moving
        }
    }

    /// <summary>
    ///     Cleans out abandoned safe houses (empty folders).
    /// </summary>
    private async Task CleanupEmptyParentDirectoriesAsync(string filePath, CancellationToken cancellationToken)
    {
        try
        {
            string? parentDirectory = Path.GetDirectoryName(filePath);

            if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory))
            {
                return;
            }

            // Don't mess with the main territory
            if (parentDirectory.Equals(_targetDirectory, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            // See if the safe house is empty
            bool hasFiles = Directory.EnumerateFileSystemEntries(parentDirectory).Any();

            if (!hasFiles)
            {
                _logger.LogDebug($"Deleting empty directory: {parentDirectory}");
                await Task.Run(() => Directory.Delete(parentDirectory), cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to clean empty directories");
        }
    }

    /// <summary>
    ///     Applies operations from the update manifest
    /// </summary>
    private async Task ApplyManifestOperationsAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug($"Reading update manifest: {_updateManifestPath}");

            string json = await Task.Run(() => File.ReadAllText(_updateManifestPath), cancellationToken);
            UpdateManifest? manifest = JsonConvert.DeserializeObject<UpdateManifest>(json);

            if (manifest == null || manifest.Operations.Count == 0)
            {
                _logger.LogDebug("Update manifest is empty or invalid");
                await DeleteManifestAsync(cancellationToken);
                return;
            }

            _logger.LogDebug($"Applying {manifest.Operations.Count} operations from manifest");

            foreach (UpdateOperation operation in manifest.Operations)
            {
                cancellationToken.ThrowIfCancellationRequested();
                await ApplyOperationAsync(operation, cancellationToken);
            }

            await UpdatePreviousSyncAsync(manifest, cancellationToken);
            await CleanupStagingDirectoryAsync(cancellationToken);
            await DeleteManifestAsync(cancellationToken);

            _logger.LogDebug("All manifest operations completed");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to apply manifest operations");
            throw;
        }
    }

    /// <summary>
    ///     Updates the PreviousSync.json file from the manifest's RemoteSyncData
    /// </summary>
    private async Task UpdatePreviousSyncAsync(UpdateManifest manifest, CancellationToken cancellationToken)
    {
        try
        {
            if (manifest?.RemoteSyncData != null)
            {
                _logger.LogDebug("Updating PreviousSync.json with remote sync data");
                string previousSyncJson = JsonConvert.SerializeObject(manifest.RemoteSyncData);
                await Task.Run(() => File.WriteAllText(_previousSyncPath, previousSyncJson), cancellationToken);
                _logger.LogDebug("PreviousSync.json updated successfully");
            }
            else
            {
                _logger.LogWarning("No RemoteSyncData found in manifest");
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to update PreviousSync.json");
            // Don't throw - this isn't critical enough to fail the whole update
        }
    }

    /// <summary>
    ///     Applies a single operation from the manifest
    /// </summary>
    private async Task ApplyOperationAsync(UpdateOperation operation, CancellationToken cancellationToken)
    {
        try
        {
            switch (operation.Type)
            {
                case OperationType.CopyFile:
                    await ApplyCopyFileOperationAsync(operation, cancellationToken);
                    break;

                case OperationType.CreateDirectory:
                    await ApplyCreateDirectoryOperationAsync(operation, cancellationToken);
                    break;

                case OperationType.DeleteFile:
                    await ApplyDeleteFileOperationAsync(operation, cancellationToken);
                    break;

                case OperationType.MoveFile:
                    await ApplyMoveFileOperationAsync(operation, cancellationToken);
                    break;

                case OperationType.ExtractArchive:
                    _logger.LogWarning($"ExtractArchive operation not yet implemented: {operation.Destination}");
                    break;

                case OperationType.DecryptFile:
                    _logger.LogWarning($"DecryptFile operation not yet implemented: {operation.Destination}");
                    break;

                default:
                    _logger.LogWarning($"Unknown operation type: {operation.Type}");
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, $"Failed to apply operation {operation.Type}: {operation.Destination}");
            throw;
        }
    }

    /// <summary>
    ///     Copies a file from staging to target
    /// </summary>
    private async Task ApplyCopyFileOperationAsync(UpdateOperation operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(operation.Source) || string.IsNullOrEmpty(operation.Destination))
        {
            throw new InvalidOperationException("CopyFile operation requires both Source and Destination");
        }

        string sourceFilePath = Path.Combine(_updateStagingDirectory, operation.Source);
        string targetFilePath = Path.Combine(_targetDirectory, operation.Destination);

        _logger.LogDebug($"Copying file: {operation.Source} -> {operation.Destination}");

        string? targetDirectoryPath = Path.GetDirectoryName(targetFilePath);
        if (!string.IsNullOrEmpty(targetDirectoryPath) && !Directory.Exists(targetDirectoryPath))
        {
            Directory.CreateDirectory(targetDirectoryPath);
        }

        await Task.Run(() => File.Copy(sourceFilePath, targetFilePath, true), cancellationToken);

        _logger.LogDebug($"File copied: {operation.Destination}");
    }

    /// <summary>
    ///     Creates a directory at the target location
    /// </summary>
    private async Task ApplyCreateDirectoryOperationAsync(UpdateOperation operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(operation.Destination))
        {
            throw new InvalidOperationException("CreateDirectory operation requires Destination");
        }

        string targetDirectoryPath = Path.Combine(_targetDirectory, operation.Destination);

        _logger.LogDebug($"Creating directory: {operation.Destination}");

        await Task.Run(() => Directory.CreateDirectory(targetDirectoryPath), cancellationToken);

        _logger.LogDebug($"Directory created: {operation.Destination}");
    }

    /// <summary>
    ///     Deletes a file from the target location
    /// </summary>
    private async Task ApplyDeleteFileOperationAsync(UpdateOperation operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(operation.Destination))
        {
            throw new InvalidOperationException("DeleteFile operation requires Destination");
        }

        if (operation.Destination != null)
            await DeleteSingleFileAsync(operation.Destination, cancellationToken);
    }

    /// <summary>
    ///     Moves a file from one location to another
    /// </summary>
    private async Task ApplyMoveFileOperationAsync(UpdateOperation operation, CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(operation.Source) || string.IsNullOrEmpty(operation.Destination))
        {
            throw new InvalidOperationException("MoveFile operation requires both Source and Destination");
        }

        if (operation.Source != null && operation.Destination != null)
        {
            string sourceFilePath = Path.Combine(_targetDirectory, operation.Source);
            string targetFilePath = Path.Combine(_targetDirectory, operation.Destination);

            _logger.LogDebug($"Moving file: {operation.Source} -> {operation.Destination}");

            string? targetDirectoryPath = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(targetDirectoryPath) && !Directory.Exists(targetDirectoryPath))
            {
                Directory.CreateDirectory(targetDirectoryPath);
            }

            await Task.Run(() => File.Move(sourceFilePath, targetFilePath), cancellationToken);
        }

        _logger.LogDebug($"File moved: {operation.Destination}");
    }

    /// <summary>
    ///     Deletes the update manifest file
    /// </summary>
    private async Task DeleteManifestAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogDebug($"Deleting update manifest: {_updateManifestPath}");

            await Task.Run(() => File.Delete(_updateManifestPath), cancellationToken);

            _logger.LogDebug("Update manifest deleted");
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Failed to delete update manifest");
            // Not a big deal - keep moving
        }
    }
}
