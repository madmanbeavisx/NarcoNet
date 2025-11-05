using System.Diagnostics;
using System.Text.RegularExpressions;

using NarcoNet.Utilities;

namespace NarcoNet;

using SyncPathFileList = Dictionary<string, List<string>>;
using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

public static class Sync
{
    public static SyncPathFileList GetAddedFiles(List<SyncPath> syncPaths, SyncPathModFiles localModFiles,
        SyncPathModFiles remoteModFiles)
    {
        return syncPaths
            .Select(syncPath => new KeyValuePair<string, List<string>>(
                syncPath.Path,
                remoteModFiles[syncPath.Path]
                    .Where(kvp => !kvp.Value.Directory)
                    .Select(kvp => kvp.Key)
                    .Except(
                        localModFiles.TryGetValue(syncPath.Path, out Dictionary<string, ModFile>? modFiles)
                            ? modFiles.Keys
                            : [], StringComparer.OrdinalIgnoreCase)
                    .ToList()
            ))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public static SyncPathFileList GetUpdatedFiles(
        List<SyncPath> syncPaths,
        SyncPathModFiles localModFiles,
        SyncPathModFiles remoteModFiles,
        SyncPathModFiles previousRemoteModFiles
    )
    {
        return syncPaths
            .Select(syncPath =>
            {
                if (!localModFiles.TryGetValue(syncPath.Path, out Dictionary<string, ModFile>? localPathFiles))
                {
                    return new KeyValuePair<string, List<string>>(syncPath.Path, []);
                }

                IEnumerable<string> query = remoteModFiles[syncPath.Path].Keys
                    .Intersect(localPathFiles.Keys, StringComparer.OrdinalIgnoreCase);

                if (!syncPath.Enabled)
                {
                    query = query.Where(file =>
                        !previousRemoteModFiles.TryGetValue(syncPath.Path, out Dictionary<string, ModFile>? previousPathFiles)
                        || !previousPathFiles.TryGetValue(file, out ModFile? modFile)
                        || remoteModFiles[syncPath.Path][file].Hash != modFile.Hash
                    );
                }

                query = query.Where(file => remoteModFiles[syncPath.Path][file].Hash != localPathFiles[file].Hash);

                return new KeyValuePair<string, List<string>>(syncPath.Path, query.ToList());
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public static SyncPathFileList GetRemovedFiles(
        List<SyncPath> syncPaths,
        SyncPathModFiles localModFiles,
        SyncPathModFiles remoteModFiles,
        SyncPathModFiles previousRemoteModFiles
    )
    {
        return syncPaths
            .Select(syncPath =>
            {
                if (!localModFiles.TryGetValue(syncPath.Path, out Dictionary<string, ModFile>? localPathFiles))
                {
                    return new KeyValuePair<string, List<string>>(syncPath.Path, []);
                }

                IEnumerable<string> query;
                // NOTE: For ENFORCED paths, deleted files are handled in GetAddedFiles (they get re-downloaded)
                // For NON-ENFORCED paths, only remove files that the SERVER deleted
                query = !previousRemoteModFiles.TryGetValue(syncPath.Path, out Dictionary<string, ModFile>? previousPathFiles)
                    ? []
                    : previousPathFiles
                        .Keys.Intersect(localPathFiles.Keys, StringComparer.OrdinalIgnoreCase)
                        .Except(remoteModFiles[syncPath.Path].Keys, StringComparer.OrdinalIgnoreCase);

                return new KeyValuePair<string, List<string>>(syncPath.Path, query.ToList());
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    public static SyncPathFileList GetCreatedDirectories(
        string basePath,
        List<SyncPath> syncPaths,
        SyncPathModFiles localModFiles,
        SyncPathModFiles remoteModFiles
    )
    {
        return syncPaths
            .Select(syncPath =>
            {
                return new KeyValuePair<string, List<string>>(
                    syncPath.Path,
                    remoteModFiles[syncPath.Path]
                        .Where(kvp => kvp.Value.Directory)
                        .Select(kvp => kvp.Key)
                        .Except(localModFiles[syncPath.Path].Keys, StringComparer.OrdinalIgnoreCase)
                        .Where(dir => !Directory.Exists(Path.Combine(basePath, dir)))
                        .ToList()
                );
            })
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    private static List<string> GetFilesInDirectory(string basePath, string directory, List<Regex> exclusions)
    {
        if (File.Exists(directory))
        {
            return [directory];
        }

        if (!Directory.Exists(directory))
        {
            return [];
        }

        return Directory
            .GetFiles(directory, "*", SearchOption.TopDirectoryOnly)
            .Where(file => !IsExcluded(exclusions, file.Replace($"{basePath}\\", "")))
            .Concat(
                Directory
                    .GetDirectories(directory, "*", SearchOption.TopDirectoryOnly)
                    .Where(subDir => !IsExcluded(exclusions, subDir.Replace($"{basePath}\\", "")))
                    .SelectMany(subDir => Directory.GetFileSystemEntries(subDir).Length == 0
                        ? [subDir]
                        : GetFilesInDirectory(basePath, subDir, exclusions))
            )
            .ToList();
    }

    public static async Task<SyncPathModFiles> HashLocalFiles(
        string basePath,
        List<SyncPath> syncPaths,
        List<Regex> remoteExclusions,
        List<Regex> localExclusions
    )
    {
        Stopwatch watch = Stopwatch.StartNew();
        HashSet<string> processedFiles =
        [
        ];
        SemaphoreSlim limitOpenFiles = new(1024);

        SyncPathModFiles results = new();

        foreach (SyncPath syncPath in syncPaths)
        {
            string path = Path.Combine(basePath, syncPath.Path);

            results[syncPath.Path] = (
                await Task.WhenAll(
                    GetFilesInDirectory(basePath, path, [.. remoteExclusions, .. syncPath.Enforced ? [] : localExclusions])
                        .Where(file => !processedFiles.Contains(file))
                        .AsParallel()
                        .Select(async file =>
                            {
                                await limitOpenFiles.WaitAsync();
                                ModFile modFile = await CreateModFile(file);
                                limitOpenFiles.Release();

                                processedFiles.Add(file);
                                return new KeyValuePair<string, ModFile>(file.Replace($"{basePath}\\", ""), modFile);
                            }
                        )
                )
            ).ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
        }

        watch.Stop();
        NarcoPlugin.Logger.LogInfo(
            $"🔍 Catalogued {processedFiles.Count} packages in the warehouse in {watch.Elapsed.TotalMilliseconds}ms");

        return results;
    }

    public static async Task<ModFile> CreateModFile(string file)
    {
        string hash = "";

        if (Directory.Exists(file))
        {
            return new ModFile(hash, true);
        }

        try
        {
            hash = await ImoHash.HashFile(file);
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"💥 Error verifying package '{file}': {e.Message}");
            hash = "";
        }

        return new ModFile(hash);
    }

    public static void CompareModFiles(
        string basePath,
        List<SyncPath> syncPaths,
        SyncPathModFiles localModFiles,
        SyncPathModFiles remoteModFiles,
        SyncPathModFiles previousSync,
        out SyncPathFileList addedFiles,
        out SyncPathFileList updatedFiles,
        out SyncPathFileList removedFiles,
        out SyncPathFileList createdDirectories
    )
    {
        addedFiles = GetAddedFiles(syncPaths, localModFiles, remoteModFiles);
        updatedFiles = GetUpdatedFiles(syncPaths, localModFiles, remoteModFiles, previousSync);
        removedFiles = GetRemovedFiles(syncPaths, localModFiles, remoteModFiles, previousSync);
        createdDirectories = GetCreatedDirectories(basePath, syncPaths, localModFiles, remoteModFiles);
    }

    public static bool IsExcluded(List<Regex> exclusions, string path)
    {
        return exclusions.Any(regex => regex.IsMatch(path.Replace(@"\", "/")));
    }
}
