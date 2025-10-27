using NarcoNet.Updater.Interfaces;

using Newtonsoft.Json;

namespace NarcoNet.Updater.Services;

/// <summary>
///   Handles moving packages around and disposing of evidence - the muscle of the operation.
/// </summary>
public class FileUpdateService : IFileUpdateService
{
  private readonly ILogger _logger;
  private readonly string _removedFilesManifestPath;
  private readonly string _targetDirectory;
  private readonly string _updateStagingDirectory;

  /// <summary>
  ///   Sets up the package handler with all the intel needed for the job.
  /// </summary>
  /// <param name="logger">The record keeper tracking every move.</param>
  /// <param name="updateStagingDirectory">The staging area where packages wait.</param>
  /// <param name="removedFilesManifestPath">The hit list - who's gotta go.</param>
  /// <param name="targetDirectory">The final destination for all packages.</param>
  public FileUpdateService(
    ILogger logger,
    string updateStagingDirectory,
    string removedFilesManifestPath,
    string targetDirectory)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    _updateStagingDirectory = updateStagingDirectory ?? throw new ArgumentNullException(nameof(updateStagingDirectory));
    _removedFilesManifestPath =
      removedFilesManifestPath ?? throw new ArgumentNullException(nameof(removedFilesManifestPath));
    _targetDirectory = targetDirectory ?? throw new ArgumentNullException(nameof(targetDirectory));
  }

  /// <inheritdoc />
  public bool HasPendingUpdates()
  {
    return Directory.Exists(_updateStagingDirectory) &&
           Directory.EnumerateFiles(_updateStagingDirectory, "*", SearchOption.AllDirectories).Any();
  }

  /// <inheritdoc />
  public IEnumerable<string> GetPendingUpdateFiles()
  {
    if (!HasPendingUpdates()) return Enumerable.Empty<string>();

    return Directory.EnumerateFiles(_updateStagingDirectory, "*", SearchOption.AllDirectories)
      .Select(file => Path.GetRelativePath(_updateStagingDirectory, file));
  }

  /// <inheritdoc />
  public async Task ApplyPendingUpdatesAsync(CancellationToken cancellationToken = default)
  {
    if (!HasPendingUpdates())
    {
      _logger.LogInformation("📭 No merchandise in the staging area.");
      return;
    }

    List<string> filesToUpdate = GetPendingUpdateFiles().ToList();
    _logger.LogInformation($"📦 Got {filesToUpdate.Count} packages ready to move.");

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
      _logger.LogInformation("📋 No hit list found. Nothing to dispose of.");
      return;
    }

    List<string>? removedFiles = await ReadRemovedFilesManifestAsync(cancellationToken);

    if (removedFiles == null || removedFiles.Count == 0)
    {
      _logger.LogInformation("✅ Hit list is empty. Clean slate!");
      await DeleteManifestFileAsync(cancellationToken);
      return;
    }

    _logger.LogInformation($"🗑️ Got {removedFiles.Count} targets on the hit list.");

    foreach (string relativeFilePath in removedFiles)
    {
      cancellationToken.ThrowIfCancellationRequested();

      await DeleteSingleFileAsync(relativeFilePath, cancellationToken);
    }

    await DeleteManifestFileAsync(cancellationToken);
  }

  /// <summary>
  ///   Moves a single package from the staging area to its final drop point.
  /// </summary>
  private async Task ApplySingleFileUpdateAsync(string relativeFilePath, CancellationToken cancellationToken)
  {
    try
    {
      string sourceFilePath = Path.Combine(_updateStagingDirectory, relativeFilePath);
      string targetFilePath = Path.Combine(_targetDirectory, relativeFilePath);

      _logger.LogInformation($"📦 Moving package: {relativeFilePath}");

      // Make sure the drop location exists
      string? targetDirectoryPath = Path.GetDirectoryName(targetFilePath);
      if (!string.IsNullOrEmpty(targetDirectoryPath) && !Directory.Exists(targetDirectoryPath))
        Directory.CreateDirectory(targetDirectoryPath);

      // Drop the package at the location
      await Task.Run(() => File.Copy(sourceFilePath, targetFilePath, true), cancellationToken);

      _logger.LogInformation($"✅ Package delivered: {relativeFilePath}");
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, $"💥 Package drop failed: {relativeFilePath}");
      throw;
    }
  }

  /// <summary>
  ///   Takes out a single target from the location.
  /// </summary>
  private async Task DeleteSingleFileAsync(string relativeFilePath, CancellationToken cancellationToken)
  {
    try
    {
      ValidateFilePath(relativeFilePath);

      string targetFilePath = Path.Combine(_targetDirectory, relativeFilePath);

      if (!File.Exists(targetFilePath))
      {
        _logger.LogWarning($"⚠️ Target already gone: {relativeFilePath}");
        return;
      }

      _logger.LogInformation($"🗑️ Eliminating target: {relativeFilePath}");

      await Task.Run(() => File.Delete(targetFilePath), cancellationToken);

      _logger.LogInformation($"✅ Target eliminated: {relativeFilePath}");

      // Clean up empty safe houses
      await CleanupEmptyParentDirectoriesAsync(targetFilePath, cancellationToken);
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, $"💥 Hit failed on target: {relativeFilePath}");
      // Don't abort the whole operation - move on to the next target
    }
  }

  /// <summary>
  ///   Makes sure the path is legit (stays in our territory, no funny business).
  /// </summary>
  private void ValidateFilePath(string relativeFilePath)
  {
    if (Path.IsPathRooted(relativeFilePath))
      throw new InvalidOperationException($"File path must be relative, not absolute: {relativeFilePath}");

    string fullPath = Path.GetFullPath(Path.Combine(_targetDirectory, relativeFilePath));
    if (!fullPath.StartsWith(_targetDirectory, StringComparison.OrdinalIgnoreCase))
      throw new InvalidOperationException($"File path is outside target directory: {relativeFilePath}");
  }

  /// <summary>
  ///   Reads the hit list from the file.
  /// </summary>
  private async Task<List<string>?> ReadRemovedFilesManifestAsync(CancellationToken cancellationToken)
  {
    try
    {
      string json = await File.ReadAllTextAsync(_removedFilesManifestPath, cancellationToken);
      return JsonConvert.DeserializeObject<List<string>>(json);
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💥 Can't read the hit list!");
      throw;
    }
  }

  /// <summary>
  ///   Sweeps the staging area clean after packages are delivered.
  /// </summary>
  private async Task CleanupStagingDirectoryAsync(CancellationToken cancellationToken)
  {
    try
    {
      _logger.LogInformation($"🧹 Cleaning the staging area: {_updateStagingDirectory}");

      await Task.Run(() => Directory.Delete(_updateStagingDirectory, true), cancellationToken);

      _logger.LogInformation("✅ Staging area is spotless!");
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💥 Couldn't clean the staging area!");
      // Not a big deal - keep moving
    }
  }

  /// <summary>
  ///   Burns the hit list after all targets are eliminated.
  /// </summary>
  private async Task DeleteManifestFileAsync(CancellationToken cancellationToken)
  {
    try
    {
      _logger.LogInformation($"🔥 Burning the hit list: {_removedFilesManifestPath}");

      await Task.Run(() => File.Delete(_removedFilesManifestPath), cancellationToken);

      _logger.LogInformation("✅ Hit list destroyed. No evidence!");
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💥 Couldn't burn the hit list!");
      // Not a big deal - keep moving
    }
  }

  /// <summary>
  ///   Cleans out abandoned safe houses (empty folders).
  /// </summary>
  private async Task CleanupEmptyParentDirectoriesAsync(string filePath, CancellationToken cancellationToken)
  {
    try
    {
      string? parentDirectory = Path.GetDirectoryName(filePath);

      if (string.IsNullOrEmpty(parentDirectory) || !Directory.Exists(parentDirectory)) return;

      // Don't mess with the main territory
      if (parentDirectory.Equals(_targetDirectory, StringComparison.OrdinalIgnoreCase)) return;

      // See if the safe house is empty
      bool hasFiles = Directory.EnumerateFileSystemEntries(parentDirectory).Any();

      if (!hasFiles)
      {
        _logger.LogInformation($"🧹 Cleaning empty safe house: {parentDirectory}");
        await Task.Run(() => Directory.Delete(parentDirectory), cancellationToken);
      }
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💥 Couldn't clean empty safe houses!");
      // Not a big deal - keep moving
    }
  }
}
