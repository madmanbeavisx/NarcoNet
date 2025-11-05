using NarcoNet.Utilities;

namespace NarcoNet.Services;

using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

/// <summary>
///     Handles client initialization and data loading
/// </summary>
public interface IClientInitializationService
{
    /// <summary>
    ///     Validates that sync paths are relative and within the server root
    /// </summary>
    /// <param name="syncPaths">Paths to validate</param>
    /// <param name="serverRoot">Server root directory</param>
    /// <returns>Validation error message, or null if valid</returns>
    string? ValidateSyncPaths(List<SyncPath> syncPaths, string serverRoot);

    /// <summary>
    ///     Loads previous sync data from disk
    /// </summary>
    /// <param name="previousSyncPath">Path to previous sync file</param>
    /// <returns>Previous sync data, or empty dictionary if not found</returns>
    SyncPathModFiles LoadPreviousSync(string previousSyncPath);

    /// <summary>
    ///     Loads local exclusions from disk, creating defaults for headless if needed
    /// </summary>
    /// <param name="localExclusionsPath">Path to exclusions file</param>
    /// <param name="isHeadless">Whether running in headless mode</param>
    /// <param name="defaultExclusions">Default exclusions for headless</param>
    /// <returns>List of exclusion patterns</returns>
    List<string> LoadLocalExclusions(string localExclusionsPath, bool isHeadless, List<string>? defaultExclusions);

    /// <summary>
    ///     Builds remote mod files dictionary from server hashes
    /// </summary>
    /// <param name="enabledSyncPaths">Enabled sync paths</param>
    /// <param name="remoteHashes">Remote file hashes from server</param>
    /// <param name="localExclusions">Local exclusion patterns</param>
    /// <returns>Dictionary of remote mod files by path</returns>
    SyncPathModFiles BuildRemoteModFiles(
        List<SyncPath> enabledSyncPaths,
        Dictionary<string, Dictionary<string, string>> remoteHashes,
        List<string> localExclusions);
}
