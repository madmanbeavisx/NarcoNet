using System.Collections;
using System.Diagnostics;
using System.Text.RegularExpressions;
using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Configuration;
using BepInEx.Logging;
using Comfort.Common;
using EFT.UI;
using NarcoNet.UI;
using NarcoNet.Utilities;
using SPT.Common.Utils;
using UnityEngine;
using static System.Diagnostics.Process;
using Debug = System.Diagnostics.Debug;
namespace NarcoNet;
using SyncPathFileList = Dictionary<string, List<string>>;
using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

[BepInPlugin("com.madmanbeavis.narconet.client", "MadManBeavis's NarcoNet", NarcoNetVersion.Version)]
public class NarcoPlugin : BaseUnityPlugin, IDisposable
{
    private static readonly string NarcoNetDir = Path.Combine(Directory.GetCurrentDirectory(), "NarcoNet_Data");
    private static readonly string PendingUpdatesDir = Path.Combine(NarcoNetDir, "PendingUpdates");
    private static readonly string PreviousSyncPath = Path.Combine(NarcoNetDir, "PreviousSync.json");
    private static readonly string LocalHashesPath = Path.Combine(NarcoNetDir, "LocalHashes.json");
    private static readonly string RemovedFilesPath = Path.Combine(NarcoNetDir, "RemovedFiles.json");
    private static readonly string LocalExclusionsPath = Path.Combine(NarcoNetDir, "Exclusions.json");
    private static readonly string UpdaterPath = Path.Combine(Directory.GetCurrentDirectory(), "NarcoNet.Updater.exe");

    private static readonly List<string> HeadlessDefaultExclusions =
    [
        "BepInEx/plugins/AmandsGraphics.dll",
        "BepInEx/plugins/AmandsSense.dll",
        "BepInEx/plugins/Sense",
        "BepInEx/plugins/MoreCheckmarks",
        "BepInEx/plugins/kmyuhkyuk-EFTApi",
        "BepInEx/plugins/DynamicMaps",
        "BepInEx/plugins/LootValue",
        "BepInEx/plugins/CactusPie.RamCleanerInterval.dll",
        "BepInEx/plugins/TYR_DeClutterer.dll"
    ];

    public new static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("NarcoNet");

    private readonly AlertWindow _downloadErrorWindow = new(
        new Vector2(640f, 240f),
        "Download failed!",
        "There was an error updating mod files.\nPlease check BepInEx/LogOutput.log for more information.",
        "QUIT"
    );

    private readonly ProgressWindow _progressWindow =
        new("Downloading Updates...", "Your game will need to be restarted\nafter update completes.");

    private readonly AlertWindow _restartWindow =
        new(new Vector2(480f, 200f), "Update Complete.", "Please restart your game to continue.");

    private readonly UpdateWindow _updateWindow = new("Installed mods do not match server", "Would you like to update?");

    private SyncPathFileList _addedFiles = [];
    private ConfigEntry<bool>? _configDeleteRemovedFiles;

    // Cartel distribution settings
    private Dictionary<string, ConfigEntry<bool>>? _configSyncPathToggles;
    private SyncPathFileList _createdDirectories = [];
    private CancellationTokenSource _cts = new();
    private int _downloadCount;

    private List<Task?> _downloadTasks = [];
    private List<string> _localExclusions = [];

    private List<string>? _noRestart;

    private List<string>? _optional;

    private bool _pluginFinished;
    private SyncPathModFiles _previousSync = [];
    private SyncPathModFiles _remoteModFiles = [];
    private SyncPathFileList _removedFiles = [];

    private List<string>? _required;

    private ServerModule? _server;

    private List<SyncPath>? _syncPaths = [];
    private int _totalDownloadCount;
    private SyncPathFileList _updatedFiles = [];

    private int UpdateCount =>
        EnabledSyncPaths
            .Select(syncPath =>
                _addedFiles[syncPath.Path].Count
                + _updatedFiles[syncPath.Path].Count
                + (_configDeleteRemovedFiles != null && (_configDeleteRemovedFiles.Value || syncPath.Enforced)
                    ? _removedFiles[syncPath.Path].Count
                    : 0)
                + _createdDirectories[syncPath.Path].Count
            )
            .Sum();

    private static bool IsHeadless => Chainloader.PluginInfos.ContainsKey("com.fika.headless");

    private List<SyncPath> EnabledSyncPaths
    {
        get
        {
            Debug.Assert(_syncPaths != null, nameof(_syncPaths) + " != null");
            if (_syncPaths != null)
            {
                return _syncPaths.Where(syncPath =>
                        _configSyncPathToggles != null && (_configSyncPathToggles[syncPath.Path].Value || syncPath.Enforced))
                    .ToList();
            }
            return [];
        }
    }

    private bool SilentMode =>
        IsHeadless
        || EnabledSyncPaths.All(syncPath =>
            _configDeleteRemovedFiles != null && (syncPath.Silent
                                                  || _addedFiles[syncPath.Path].Count == 0
                                                  && _updatedFiles[syncPath.Path].Count == 0
                                                  && (!(_configDeleteRemovedFiles.Value || syncPath.Enforced) ||
                                                      _removedFiles[syncPath.Path].Count == 0)
                                                  && _createdDirectories[syncPath.Path].Count == 0)
        );

    private bool NoRestartMode =>
        EnabledSyncPaths.All(syncPath =>
            _configDeleteRemovedFiles != null && (!syncPath.RestartRequired
                                                  || _addedFiles[syncPath.Path].Count == 0
                                                  && _updatedFiles[syncPath.Path].Count == 0
                                                  && (!(_configDeleteRemovedFiles.Value || syncPath.Enforced) ||
                                                      _removedFiles[syncPath.Path].Count == 0)
                                                  && _createdDirectories[syncPath.Path].Count == 0)
        );

    private List<string> Optional =>
        _optional ??= EnabledSyncPaths
            .Where(syncPath => !syncPath.Enforced)
            .SelectMany(syncPath =>
                _addedFiles[syncPath.Path]
                    .Select(file => $"ADDED {file}")
                    .Concat(_updatedFiles[syncPath.Path].Select(file => $"UPDATED {file}"))
                    .Concat(_configDeleteRemovedFiles != null && (_configDeleteRemovedFiles.Value || syncPath.Enforced)
                        ? _removedFiles[syncPath.Path].Select(file => $"REMOVED {file}")
                        : [])
                    .Concat(_createdDirectories[syncPath.Path].Select(file => $@"CREATED {file}\"))
            )
            .ToList();

    private List<string> Required =>
        _required ??= EnabledSyncPaths
            .Where(syncPath => syncPath.Enforced)
            .SelectMany(syncPath =>
                _addedFiles[syncPath.Path]
                    .Select(file => $"ADDED {file}")
                    .Concat(_updatedFiles[syncPath.Path].Select(file => $"UPDATED {file}"))
                    .Concat(_configDeleteRemovedFiles is { Value: true }
                        ? _removedFiles[syncPath.Path].Select(file => $"REMOVED {file}")
                        : [])
                    .Concat(_createdDirectories[syncPath.Path].Select(file => $@"CREATED {file}\"))
            )
            .ToList();

    private List<string> NoRestart =>
        _noRestart ??= EnabledSyncPaths
            .Where(syncPath => !syncPath.RestartRequired)
            .SelectMany(syncPath =>
                _addedFiles[syncPath.Path]
                    .Concat(_updatedFiles[syncPath.Path])
                    .Concat(_configDeleteRemovedFiles != null && (_configDeleteRemovedFiles.Value || syncPath.Enforced)
                        ? _removedFiles[syncPath.Path]
                        : [])
                    .Concat(_createdDirectories[syncPath.Path])
            )
            .ToList();

    private void Awake()
    {
        ConsoleScreen.Processor.RegisterCommand(
            "narconet",
            () =>
            {
                ConsoleScreen.Log("Checking for updates.");
                StartCoroutine(StartPlugin());
            }
        );

        _server = new ServerModule(Info.Metadata.Version);

        _configDeleteRemovedFiles = Config.Bind("General", "Delete Removed Files", true,
            "Should the mod delete files that have been removed from the server?");
    }

    public void Start()
    {
        StartCoroutine(StartPlugin());
    }

    public void Update()
    {
        if (_updateWindow.Active || _progressWindow.Active || _restartWindow.Active || _downloadErrorWindow.Active)
        {
            if (Singleton<LoginUI>.Instantiated && Singleton<LoginUI>.Instance.gameObject.activeSelf)
            {
                Singleton<LoginUI>.Instance.gameObject.SetActive(false);
            }

            if (Singleton<PreloaderUI>.Instantiated && Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
            {
                Singleton<PreloaderUI>.Instance.gameObject.SetActive(false);
            }

            if (Singleton<CommonUI>.Instantiated && Singleton<CommonUI>.Instance.gameObject.activeSelf)
            {
                Singleton<CommonUI>.Instance.gameObject.SetActive(false);
            }
        }
        else if (_pluginFinished)
        {
            _pluginFinished = false;
            if (Singleton<LoginUI>.Instantiated && !Singleton<LoginUI>.Instance.gameObject.activeSelf)
            {
                Singleton<LoginUI>.Instance.gameObject.SetActive(true);
            }

            if (Singleton<PreloaderUI>.Instantiated && !Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
            {
                Singleton<PreloaderUI>.Instance.gameObject.SetActive(true);
            }

            if (Singleton<CommonUI>.Instantiated && !Singleton<CommonUI>.Instance.gameObject.activeSelf)
            {
                Singleton<CommonUI>.Instance.gameObject.SetActive(true);
            }
        }
    }

    private void OnGUI()
    {
        if (!Singleton<CommonUI>.Instantiated)
        {
            return;
        }

        if (_restartWindow.Active)
        {
            _restartWindow.Draw(StartUpdaterProcess);
        }

        if (_progressWindow.Active)
        {
            _progressWindow.Draw(_downloadCount, _totalDownloadCount,
                Required.Count != 0 || NoRestart.Count != 0 ? null : () => Task.Run(CancelUpdatingMods));
        }

        if (_updateWindow.Active)
        {
            _updateWindow.Draw(
                (Optional.Count != 0 ? string.Join("\n", Optional) : "")
                + (Optional.Count != 0 && Required.Count != 0 ? "\n\n" : "")
                + (Required.Count != 0 ? "[Enforced]\n" + string.Join("\n", Required) : ""),
                () => Task.Run(() => SyncMods(_addedFiles, _updatedFiles, _createdDirectories)),
                Required.Count != 0 && Optional.Count == 0 ? null : SkipUpdatingMods
            );
        }

        if (_downloadErrorWindow.Active)
        {
            _downloadErrorWindow.Draw(Application.Quit);
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    private void AnalyzeModFiles(SyncPathModFiles localModFiles)
    {
        Sync.CompareModFiles(
            Directory.GetCurrentDirectory(),
            EnabledSyncPaths,
            localModFiles,
            _remoteModFiles,
            _previousSync,
            out _addedFiles,
            out _updatedFiles,
            out _removedFiles,
            out _createdDirectories
        );

        int addedCount = _addedFiles.SelectMany(path => path.Value).Count();
        int updatedCount = _updatedFiles.SelectMany(path => path.Value).Count();
        int removedCount = _removedFiles.SelectMany(path => path.Value).Count();

        Logger.LogDebug($"{UpdateCount} file changes detected: {addedCount} added, {updatedCount} updated, {removedCount} removed");

        if (addedCount > 0)
        {
            foreach (KeyValuePair<string, List<string>> syncPath in _addedFiles.Where(kvp => kvp.Value.Count > 0))
            {
                Logger.LogDebug($"  [{syncPath.Key}]");
                foreach (string? file in syncPath.Value)
                {
                    Logger.LogDebug($"    + {file}");
                }
            }
        }

        if (updatedCount > 0)
        {
            foreach (KeyValuePair<string, List<string>> syncPath in _updatedFiles.Where(kvp => kvp.Value.Count > 0))
            {
                Logger.LogDebug($"  [{syncPath.Key}]");
                foreach (string? file in syncPath.Value)
                {
                    Logger.LogDebug($"    * {file}");
                }
            }
        }

        if (removedCount > 0)
        {
            foreach (KeyValuePair<string, List<string>> syncPath in _removedFiles.Where(kvp => kvp.Value.Count > 0))
            {
                Logger.LogDebug($"  [{syncPath.Key}]");
                foreach (string? file in syncPath.Value)
                {
                    Logger.LogDebug($"    - {file}");
                }
            }
        }

        if (UpdateCount > 0)
        {
            if (SilentMode)
            {
                Task.Run(() => SyncMods(_addedFiles, _updatedFiles, _createdDirectories));
            }
            else
            {
                _updateWindow.Show();
            }
        }
        else
        {
            WriteNarcoNetData();
        }
    }

    private void SkipUpdatingMods()
    {
        SyncPathFileList enforcedAddedFiles = EnabledSyncPaths.ToDictionary(
            syncPath => syncPath.Path,
            syncPath => syncPath.Enforced ? _addedFiles[syncPath.Path] : [],
            StringComparer.OrdinalIgnoreCase
        );

        SyncPathFileList enforcedUpdatedFiles = EnabledSyncPaths.ToDictionary(
            syncPath => syncPath.Path,
            syncPath => syncPath.Enforced ? _updatedFiles[syncPath.Path] : [],
            StringComparer.OrdinalIgnoreCase
        );

        SyncPathFileList enforcedCreatedDirectories = EnabledSyncPaths.ToDictionary(
            syncPath => syncPath.Path,
            syncPath => syncPath.Enforced ? _createdDirectories[syncPath.Path] : [],
            StringComparer.OrdinalIgnoreCase
        );

        if (
            enforcedAddedFiles.Values.Any(files => files.Any())
            || enforcedUpdatedFiles.Values.Any(files => files.Any())
            || enforcedCreatedDirectories.Values.Any(files => files.Any())
        )
        {
            Task.Run(() => SyncMods(enforcedAddedFiles, enforcedUpdatedFiles, enforcedCreatedDirectories));
        }
        else
        {
            _pluginFinished = true;
            _updateWindow.Hide();
        }
    }

    private async Task SyncMods(SyncPathFileList filesToAdd, SyncPathFileList filesToUpdate,
        SyncPathFileList directoriesToCreate)
    {
        _updateWindow.Hide();

        if (!Directory.Exists(PendingUpdatesDir))
        {
            Directory.CreateDirectory(PendingUpdatesDir);
        }

        foreach (SyncPath syncPath in EnabledSyncPaths)
        {
            foreach (string dir in directoriesToCreate[syncPath.Path])
            {
                try
                {
                    Directory.CreateDirectory(dir);
                }
                catch (Exception e)
                {
                    Logger.LogError("Failed to create directory: " + e);
                }
            }
        }

        _downloadCount = 0;
        _totalDownloadCount = 0;

        SemaphoreSlim limiter = new(8);
        SyncPathFileList filesToDownload = EnabledSyncPaths
            .Select(syncPath =>
                new KeyValuePair<string, List<string>>(syncPath.Path,
                    [.. filesToAdd[syncPath.Path], .. filesToUpdate[syncPath.Path]]))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        Logger.LogDebug($"Downloading {UpdateCount} files...");
        _downloadTasks = EnabledSyncPaths
            .SelectMany(syncPath =>
                filesToDownload.TryGetValue(syncPath.Path, out List<string>? pathFilesToDownload)
                    ? pathFilesToDownload.Select(file =>
                        _server?.DownloadFile(file, syncPath.RestartRequired ? PendingUpdatesDir : Directory.GetCurrentDirectory(),
                            limiter, _cts.Token)
                    )
                    : []
            )
            .ToList();

        _totalDownloadCount = _downloadTasks.Count;

        if (!IsHeadless)
        {
            _progressWindow.Show();
        }

        while (_downloadTasks.Count > 0 && !_cts.IsCancellationRequested)
        {
            Task task = await Task.WhenAny(_downloadTasks!);

            try
            {
                await task;
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException && _cts.IsCancellationRequested)
                {
                    continue;
                }

                _cts.Cancel();
                _progressWindow.Hide();
                if (!IsHeadless)
                {
                    _downloadErrorWindow.Show();
                }
            }

            _downloadTasks.Remove(task);
            _downloadCount++;
        }

        _downloadTasks.Clear();
        _progressWindow.Hide();

        Logger.LogDebug("All files downloaded successfully");

        if (!_cts.IsCancellationRequested)
        {
            WriteNarcoNetData();

            if (NoRestartMode)
            {
                Directory.Delete(PendingUpdatesDir, true);
                _pluginFinished = true;
            }
            else if (!IsHeadless)
            {
                _restartWindow.Show();
            }
            else
            {
                StartUpdaterProcess();
            }
        }
    }

    private async Task CancelUpdatingMods()
    {
        _progressWindow.Hide();
        _cts.Cancel();

        await Task.WhenAll(_downloadTasks!);

        Directory.Delete(PendingUpdatesDir, true);
        _pluginFinished = true;
    }

    private void WriteNarcoNetData()
    {
        VFS.WriteTextFile(PreviousSyncPath, Json.Serialize(_remoteModFiles));
        if (EnabledSyncPaths.Any(syncPath =>
                _configDeleteRemovedFiles != null && (_configDeleteRemovedFiles.Value || syncPath.Enforced) &&
                _removedFiles[syncPath.Path].Count != 0))
        {
            VFS.WriteTextFile(RemovedFilesPath, Json.Serialize(_removedFiles.SelectMany(kvp => kvp.Value).ToList()));
        }
    }

    private void StartUpdaterProcess()
    {
        List<string> options = [];

        if (IsHeadless)
        {
            options.Add("--silent");
        }

        Logger.LogDebug($"Starting updater with options: {string.Join(" ", options)} {GetCurrentProcess().Id}");
        ProcessStartInfo updaterStartInfo = new()
        {
            FileName = UpdaterPath,
            Arguments = string.Join(" ", options) + " " + GetCurrentProcess().Id,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        Process updaterProcess = new() { StartInfo = updaterStartInfo };

        updaterProcess.Start();
        Application.Quit();
    }

    private IEnumerator StartPlugin()
    {
        _cts = new CancellationTokenSource();
        if (Directory.Exists(PendingUpdatesDir) || File.Exists(RemovedFilesPath))
        {
            Logger.LogWarning(
                "Found pending updates from previous session. Check 'NarcoNet_Data/Updater.log' for details."
            );
        }

        Logger.LogDebug("Requesting server version...");
        Task<string>? versionTask = _server?.GetNarcoNetVersion();
        yield return new WaitUntil(() => versionTask is { IsCompleted: true });
        try
        {
            string? version = versionTask?.Result;

            Logger.LogInfo($"NarcoNet plugin loaded");
            Logger.LogDebug($"Server version: {version}");
            if (version != Info.Metadata.Version.ToString())
            {
                Logger.LogWarning(
                    $"Version mismatch: Server is running {version}, client is running {Info.Metadata.Version}");
            }
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to error requesting server version. Please ensure the server mod is properly installed and try again."
            );
            yield break;
        }

        Logger.LogDebug("Requesting sync paths...");
        Task<List<SyncPath>>? syncPathTask = _server?.GetLocalSyncPaths();
        yield return new WaitUntil(() => syncPathTask is { IsCompleted: true });
        try
        {
            _syncPaths = syncPathTask?.Result;
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to get sync paths: {e.GetType().Name}: {e.Message}");
            Logger.LogError($"Stack trace: {e.StackTrace}");
            if (e.InnerException != null)
            {
                Logger.LogError($"Inner exception: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
            }
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to error requesting sync paths: {e.Message}"
            );
            yield break;
        }

        Logger.LogDebug("Validating sync paths...");
        if (_syncPaths != null)
        {
            foreach (SyncPath syncPath in _syncPaths)
            {
                if (Path.IsPathRooted(syncPath.Path))
                {
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to invalid sync path. Paths must be relative to SPT server root! Invalid path '{syncPath}'"
                    );
                    yield break;
                }

                if (!Path.GetFullPath(syncPath.Path).StartsWith(Directory.GetCurrentDirectory()))
                {
                    Chainloader.DependencyErrors.Add(
                        $"Could not load {Info.Metadata.Name} due to invalid sync path. Paths must be within SPT server root! Invalid path '{syncPath}'"
                    );
                    yield break;
                }
            }

            Logger.LogDebug("Checking for data migration...");
            new Migrator(Directory.GetCurrentDirectory()).TryMigrate(Info.Metadata.Version, _syncPaths);

            Logger.LogDebug("Loading configuration...");

            try
            {
                _configSyncPathToggles = _syncPaths
                    .Select(syncPath => new KeyValuePair<string, ConfigEntry<bool>>(
                        syncPath.Path,
                        Config.Bind(
                            "Synced Paths",
                            syncPath.Name.Replace("\\", "/"),
                            syncPath.Enabled,
                            new ConfigDescription(
                                $"Should the mod attempt to sync files from {syncPath.Path.Replace("\\", "/")}",
                                null,
                                new ConfigurationManagerAttributes { ReadOnly = syncPath.Enforced }
                            )
                        )
                    ))
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
            catch (Exception e)
            {
                Logger.LogError(
                    $"Failed to bind sync path configuration:\n{e}");
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to error binding sync path configs. Please check your server configuration and try again."
                );
            }
        }

        Logger.LogDebug("Loading previous sync data...");
        try
        {
            _previousSync = VFS.Exists(PreviousSyncPath)
                ? Json.Deserialize<SyncPathModFiles>(VFS.ReadTextFile(PreviousSyncPath))
                : [];
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to malformed previous sync data. Please check NarcoNet_Data/PreviousSync.json for errors or delete it, and try again."
            );
            yield break;
        }

        Logger.LogDebug("Loading local exclusions...");
        if (IsHeadless && !VFS.Exists(LocalExclusionsPath))
        {
            try
            {
                VFS.WriteTextFile(LocalExclusionsPath, Json.Serialize(HeadlessDefaultExclusions));
            }
            catch (Exception e)
            {
                Logger.LogError(e);
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to error writing local exclusions file for headless client. Please check BepInEx/LogOutput.log for more information."
                );
                yield break;
            }
        }

        try
        {
            _localExclusions = VFS.Exists(LocalExclusionsPath)
                ? Json.Deserialize<List<string>>(VFS.ReadTextFile(LocalExclusionsPath))
                : [];
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to malformed local exclusion data. Please check NarcoNet_Data/Exclusions.json for errors or delete it, and try again."
            );
            yield break;
        }

        Logger.LogDebug("Requesting exclusions from server...");

        List<string>? exclusions;
        Task<List<string>>? exclusionsTask = _server?.GetListExclusions();
        yield return new WaitUntil(() => exclusionsTask is { IsCompleted: true });
        try
        {
            exclusions = exclusionsTask?.Result;
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to error requesting exclusions. Please ensure the server mod is properly installed and try again."
            );
            yield break;
        }

        Logger.LogDebug("Waiting for UI to initialize...");
        yield return new WaitUntil(() => Singleton<CommonUI>.Instantiated);

        Logger.LogDebug("Hashing local files...");
        if (exclusions == null)
        {
            yield break;
        }

        {
            Task<SyncPathModFiles> localModFilesTask = Sync.HashLocalFiles(
                Directory.GetCurrentDirectory(),
                EnabledSyncPaths,
                exclusions.Select(Glob.Create).ToList(),
                _localExclusions.Select(Glob.Create).ToList()
            );

            yield return new WaitUntil(() => localModFilesTask.IsCompleted);

            if (localModFilesTask.IsFaulted)
            {
                Logger.LogError($"Failed to hash local files: {localModFilesTask.Exception?.GetType().Name}: {localModFilesTask.Exception?.Message}");
                if (localModFilesTask.Exception?.InnerException != null)
                {
                    Logger.LogError($"Inner exception: {localModFilesTask.Exception.InnerException.GetType().Name}: {localModFilesTask.Exception.InnerException.Message}");
                }
                Logger.LogError($"Stack trace: {localModFilesTask.Exception?.StackTrace}");
                yield break;
            }

            SyncPathModFiles localModFiles = localModFilesTask.Result;

            Logger.LogDebug($"Hashed {localModFiles.Sum(kvp => kvp.Value.Count)} local files");

            VFS.WriteTextFile(LocalHashesPath, Json.Serialize(localModFiles));

            Logger.LogDebug("Requesting remote hashes...");
            Task<Dictionary<string, Dictionary<string, string>>>? remoteHashesTask =
                _server?.GetRemoteHashes(EnabledSyncPaths);
            yield return new WaitUntil(() => remoteHashesTask is { IsCompleted: true });
            try
            {
                Dictionary<string, Dictionary<string, string>>? remoteHashes = remoteHashesTask?.Result;

                List<Regex> localExclusionsForRemote = _localExclusions.Select(Glob.CreateNoEnd).ToList();
                _remoteModFiles = EnabledSyncPaths
                    .Select(syncPath =>
                        {
                            // Get remote hashes for this path, or empty dict if path doesn't exist on server
                            Dictionary<string, string>? remotePathHashes = remoteHashes?.TryGetValue(syncPath.Path, out Dictionary<string, string>? hashes) == true ? hashes : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

                            if (!syncPath.Enforced)
                            {
                                // Filter out locally excluded files for non-enforced paths
                                remotePathHashes = remotePathHashes
                                    .Where(kvp => !Sync.IsExcluded(localExclusionsForRemote, kvp.Key))
                                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
                            }

                            Dictionary<string, ModFile> remoteModFilesForPath = remotePathHashes
                                .ToDictionary(
                                    kvp => kvp.Key,
                                    kvp => new ModFile(kvp.Value, kvp.Key.EndsWith("\\") || kvp.Key.EndsWith("/")),
                                    StringComparer.OrdinalIgnoreCase
                                );

                            return new KeyValuePair<string, Dictionary<string, ModFile>>(syncPath.Path, remoteModFilesForPath);
                        }
                    )
                    .ToDictionary(kvp => kvp.Key, kvp => kvp.Value, StringComparer.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to get remote hashes: {e.GetType().Name}: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
                if (e.InnerException != null)
                {
                    Logger.LogError($"Inner exception: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
                }
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to error requesting server mod list: {e.Message}"
                );
                yield break;
            }

            Logger.LogDebug("Comparing local and remote files...");
            try
            {
                AnalyzeModFiles(localModFiles);
            }
            catch (Exception e)
            {
                Logger.LogError($"Failed to analyze mod files: {e.GetType().Name}: {e.Message}");
                Logger.LogError($"Stack trace: {e.StackTrace}");
                if (e.InnerException != null)
                {
                    Logger.LogError($"Inner exception: {e.InnerException.GetType().Name}: {e.InnerException.Message}");
                }
                Chainloader.DependencyErrors.Add(
                    $"Could not load {Info.Metadata.Name} due to error analyzing mod files: {e.Message}"
                );
                yield break;
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts?.Dispose();
        }
    }
}
