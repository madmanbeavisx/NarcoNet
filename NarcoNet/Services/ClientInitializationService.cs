using NarcoNet.Utilities;
using SPT.Common.Utils;
using System.Text.RegularExpressions;

namespace NarcoNet.Services;

using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

/// <summary>
///     Handles client initialization and data loading
/// </summary>
public class ClientInitializationService : IClientInitializationService
{
    /// <inheritdoc/>
    public string? ValidateSyncPaths(List<SyncPath> syncPaths, string serverRoot)
    {
        foreach (SyncPath syncPath in syncPaths)
        {
            if (Path.IsPathRooted(syncPath.Path))
            {
                return $"Paths must be relative to SPT server root! Invalid path '{syncPath}'";
            }

            // Get the full resolved path
            string fullPath = Path.GetFullPath(Path.Combine(serverRoot, syncPath.Path));

            // Check if the path exists or can be created (validate it's a legitimate path)
            try
            {
                // Just validate the path format is valid, don't check if it exists yet
                _ = Path.GetDirectoryName(fullPath);
            }
            catch (Exception)
            {
                return $"Invalid path format! Invalid path '{syncPath}'";
            }
        }

        return null;
    }

    /// <inheritdoc/>
    public SyncPathModFiles LoadPreviousSync(string previousSyncPath)
    {
        if (!VFS.Exists(previousSyncPath))
        {
            return [];
        }

        string json = VFS.ReadTextFile(previousSyncPath);
        return Json.Deserialize<SyncPathModFiles>(json);
    }

    /// <inheritdoc/>
    public List<string> LoadLocalExclusions(string localExclusionsPath, bool isHeadless, List<string>? defaultExclusions)
    {
        // Create defaults for headless if file doesn't exist
        if (isHeadless && !VFS.Exists(localExclusionsPath) && defaultExclusions != null)
        {
            VFS.WriteTextFile(localExclusionsPath, Json.Serialize(defaultExclusions));
        }

        if (!VFS.Exists(localExclusionsPath))
        {
            return [];
        }

        string json = VFS.ReadTextFile(localExclusionsPath);
        return Json.Deserialize<List<string>>(json);
    }

    /// <inheritdoc/>
    public SyncPathModFiles BuildRemoteModFiles(
        List<SyncPath> enabledSyncPaths,
        Dictionary<string, Dictionary<string, string>> remoteHashes,
        List<string> localExclusions)
    {
        List<Regex> localExclusionsRegex = localExclusions.Select(Glob.CreateNoEnd).ToList();

        return enabledSyncPaths
            .Select(syncPath =>
            {
                // Get remote hashes for this path, or empty dict if path doesn't exist on server
                var remotePathHashes = remoteHashes.TryGetValue(syncPath.Path, out var hashes)
                    ? hashes
                    : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                if (!syncPath.Enforced)
                {
                    // Filter out locally excluded files for non-enforced paths
                    remotePathHashes = remotePathHashes
                        .Where(kvp => !Sync.IsExcluded(localExclusionsRegex, kvp.Key))
                        .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
                }

                var remoteModFilesForPath = remotePathHashes
                    .ToDictionary(
                        kvp => kvp.Key,
                        kvp => new ModFile(kvp.Value, kvp.Key.EndsWith("\\") || kvp.Key.EndsWith("/")),
                        StringComparer.OrdinalIgnoreCase
                    );

                return new KeyValuePair<string, Dictionary<string, ModFile>>(syncPath.Path, remoteModFilesForPath);
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
    }
}
