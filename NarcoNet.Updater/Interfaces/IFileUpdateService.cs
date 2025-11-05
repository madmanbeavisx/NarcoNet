namespace NarcoNet.Updater.Interfaces;

/// <summary>
///     Defines the contract for file update operations.
/// </summary>
public interface IFileUpdateService
{
    /// <summary>
    ///     Applies pending file updates from the staging directory to the target directory.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task ApplyPendingUpdatesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Deletes files that have been marked for removal.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task DeleteRemovedFilesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    ///     Checks if there are any pending updates to apply.
    /// </summary>
    /// <returns>True if updates are pending; otherwise, false.</returns>
    bool HasPendingUpdates();

    /// <summary>
    ///     Gets the list of files that will be updated.
    /// </summary>
    /// <returns>A collection of file paths to be updated.</returns>
    IEnumerable<string> GetPendingUpdateFiles();
}
