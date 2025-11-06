using System.Collections;
using System.Diagnostics;

using BepInEx;
using BepInEx.Bootstrap;
using BepInEx.Logging;
using Comfort.Common;
using EFT.UI;
using NarcoNet.Services;
using NarcoNet.Utilities;
using SPT.Common.Utils;
using UnityEngine;
using static System.Diagnostics.Process;

namespace NarcoNet;
using SyncPathFileList = Dictionary<string, List<string>>;
using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

/// <summary>
///     Main NarcoNet client plugin that coordinates file synchronization between SPT server and client
/// </summary>
[BepInPlugin("com.madmanbeavis.narconet.client", "MadManBeavis's NarcoNet", NarcoNetVersion.Version)]
public class NarcoPlugin : BaseUnityPlugin, IDisposable
{
    // Static paths
    private static readonly string NarcoNetDir = Path.Combine(Directory.GetCurrentDirectory(), "NarcoNet_Data");
    private static readonly string PendingUpdatesDir = Path.Combine(NarcoNetDir, "PendingUpdates");
    private static readonly string PreviousSyncPath = Path.Combine(NarcoNetDir, "PreviousSync.json");
    private static readonly string LocalHashesPath = Path.Combine(NarcoNetDir, "LocalHashes.json");
    private static readonly string RemovedFilesPath = Path.Combine(NarcoNetDir, "RemovedFiles.json");
    private static readonly string LocalExclusionsPath = Path.Combine(NarcoNetDir, "Exclusions.json");
    private static readonly string UpdaterPath = Path.Combine(Directory.GetCurrentDirectory(), "NarcoNet.Updater.exe");

    public new static readonly ManualLogSource Logger = BepInEx.Logging.Logger.CreateLogSource("NarcoNet");

    // Services
    private readonly IClientUIService _uiService;
    private readonly IClientConfigService _configService;
    private readonly IClientSyncService _syncService;
    private readonly IClientInitializationService _initService;
    private readonly ServerModule _server;

    // State
    private SyncPathFileList _addedFiles = [];
    private SyncPathFileList _updatedFiles = [];
    private SyncPathFileList _removedFiles = [];
    private SyncPathFileList _createdDirectories = [];
    private SyncPathModFiles _previousSync = [];
    private SyncPathModFiles _remoteModFiles = [];
    private List<string> _localExclusions = [];
    private List<string>? _optional;
    private List<string>? _required;
    private List<string>? _noRestart;
    private bool _pluginFinished;
    private CancellationTokenSource _cts = new();

    /// <summary>
    ///     Constructor - initializes services
    /// </summary>
    public NarcoPlugin()
    {
        _server = new ServerModule(Info.Metadata.Version);
        _uiService = new ClientUIService();
        _configService = new ClientConfigService();
        _syncService = new ClientSyncService(Logger, _server);
        _initService = new ClientInitializationService();
    }

    private int UpdateCount =>
        _syncService.GetUpdateCount(
            _addedFiles,
            _updatedFiles,
            _removedFiles,
            _createdDirectories,
            _configService.EnabledSyncPaths,
            _configService.DeleteRemovedFiles.Value
        );

    private List<SyncPath> EnabledSyncPaths => _configService.EnabledSyncPaths;

    private bool SilentMode =>
        _syncService.IsSilentMode(
            _addedFiles,
            _updatedFiles,
            _removedFiles,
            _createdDirectories,
            _configService.EnabledSyncPaths,
            _configService.DeleteRemovedFiles.Value,
            _configService.IsHeadless()
        );

    private bool NoRestartMode =>
        !_syncService.IsRestartRequired(
            _addedFiles,
            _updatedFiles,
            _removedFiles,
            _createdDirectories,
            _configService.EnabledSyncPaths,
            _configService.DeleteRemovedFiles.Value
        );

    private List<string> Optional =>
        _optional ??= EnabledSyncPaths
            .Where(syncPath => !syncPath.Enforced)
            .SelectMany(syncPath =>
                _addedFiles[syncPath.Path]
                    .Select(file => $"ADDED {file}")
                    .Concat(_updatedFiles[syncPath.Path].Select(file => $"UPDATED {file}"))
                    .Concat(_configService.DeleteRemovedFiles.Value || syncPath.Enforced
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
                    .Concat(_configService.DeleteRemovedFiles.Value
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
                    .Concat(_configService.DeleteRemovedFiles.Value || syncPath.Enforced
                        ? _removedFiles[syncPath.Path]
                        : [])
                    .Concat(_createdDirectories[syncPath.Path])
            )
            .ToList();

    /// <summary>
    ///     Unity lifecycle - registers console command
    /// </summary>
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
    }

    /// <summary>
    ///     Unity lifecycle - starts the plugin initialization
    /// </summary>
    public void Start()
    {
        StartCoroutine(StartPlugin());
    }

    /// <summary>
    ///     Unity lifecycle - handles UI visibility management
    /// </summary>
    public void Update()
    {
        _uiService.HandleGameUIVisibility(_uiService.IsAnyWindowActive);

        if (!_uiService.IsAnyWindowActive && _pluginFinished)
        {
            _pluginFinished = false;
            _uiService.HandleGameUIVisibility(false);
        }
    }

    /// <summary>
    ///     Unity lifecycle - delegates UI rendering to service
    /// </summary>
    private void OnGUI()
    {
        _uiService.DrawWindows();
    }

    /// <summary>
    ///     Disposes resources
    /// </summary>
    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>
    ///     Analyzes differences between local and remote files, then shows update UI or silently syncs
    /// </summary>
    private void AnalyzeModFiles(SyncPathModFiles localModFiles)
    {
        _syncService.AnalyzeModFiles(
            localModFiles,
            _remoteModFiles,
            _previousSync,
            EnabledSyncPaths,
            out _addedFiles,
            out _updatedFiles,
            out _removedFiles,
            out _createdDirectories
        );

        if (UpdateCount > 0)
        {
            if (SilentMode)
            {
                Task.Run(() => SyncMods(_addedFiles, _updatedFiles, _createdDirectories));
            }
            else
            {
                _uiService.ShowUpdateWindow(
                    Optional,
                    Required,
                    () => Task.Run(() => SyncMods(_addedFiles, _updatedFiles, _createdDirectories)),
                    Required.Count != 0 && Optional.Count == 0 ? null : SkipUpdatingMods
                );
            }
        }
        else
        {
            _syncService.WriteNarcoNetData(_remoteModFiles, _removedFiles, EnabledSyncPaths, _configService.DeleteRemovedFiles.Value);
        }
    }

    /// <summary>
    ///     Handles user skipping optional updates - only syncs enforced changes
    /// </summary>
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
            _uiService.HideAllWindows();
        }
    }

    /// <summary>
    ///     Downloads and synchronizes mod files, showing progress UI
    /// </summary>
    private async Task SyncMods(SyncPathFileList filesToAdd, SyncPathFileList filesToUpdate,
        SyncPathFileList directoriesToCreate)
    {
        _uiService.HideAllWindows();
        

        if (!_configService.IsHeadless())
        {
            _uiService.ShowProgressWindow();
        }

        Progress<(int current, int total)> progress = new(p =>
        {
            _uiService.UpdateProgress(p.current, p.total,
                Required.Count != 0 || NoRestart.Count != 0 ? null : () => Task.Run(CancelUpdatingMods));
        });

        try
        {
            await _syncService.SyncModsAsync(
                filesToAdd,
                filesToUpdate,
                directoriesToCreate,
                EnabledSyncPaths,
                _configService.DeleteRemovedFiles.Value,
                PendingUpdatesDir,
                progress,
                _cts.Token
            );

            _uiService.HideProgressWindow();

            if (!_cts.IsCancellationRequested)
            {
                if (NoRestartMode)
                {
                    // Only write sync data if no restart is required (updates were applied immediately)
                    _syncService.WriteNarcoNetData(_remoteModFiles, _removedFiles, EnabledSyncPaths, _configService.DeleteRemovedFiles.Value);
                    Directory.Delete(PendingUpdatesDir, true);
                    _pluginFinished = true;
                }
                else
                {
                    // Write the update manifest for the updater to process
                    _syncService.WriteUpdateManifest(_addedFiles, _updatedFiles, _createdDirectories, _removedFiles, EnabledSyncPaths, _configService.DeleteRemovedFiles.Value, PendingUpdatesDir, _remoteModFiles);

                    if (!_configService.IsHeadless())
                    {
                        _uiService.ShowRestartWindow(StartUpdaterProcess);
                    }
                    else
                    {
                        StartUpdaterProcess();
                    }
                }
            }
        }
        catch (Exception)
        {
            _uiService.HideProgressWindow();
            if (!_configService.IsHeadless())
            {
                _uiService.ShowErrorWindow(Application.Quit);
            }
            throw;
        }
    }

    /// <summary>
    ///     Cancels the download process and cleans up pending updates
    /// </summary>
    private async Task CancelUpdatingMods()
    {
        _uiService.HideProgressWindow();
        _cts.Cancel();

        if (Directory.Exists(PendingUpdatesDir))
        {
            Directory.Delete(PendingUpdatesDir, true);
        }

        _pluginFinished = true;
        await Task.CompletedTask;
    }

    /// <summary>
    ///     Starts the external updater process to apply pending updates and restart the game
    /// </summary>
    private void StartUpdaterProcess()
    {
        List<string> options = [];

        if (_configService.IsHeadless())
        {
            options.Add("--silent");
        }

        Logger.LogInfo($"Starting updater with options: {string.Join(" ", options)} {GetCurrentProcess().Id} with executable at {UpdaterPath}");
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

    /// <summary>
    ///     Main plugin initialization coroutine - fetches server config and checks for updates
    /// </summary>
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
        Task<string> versionTask = _server.GetNarcoNetVersion();
        yield return new WaitUntil(() => versionTask is { IsCompleted: true });
        try
        {
            string? version = versionTask.Result;

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
        Task<List<SyncPath>> syncPathTask = _server.GetLocalSyncPaths();
        yield return new WaitUntil(() => syncPathTask is { IsCompleted: true });
        List<SyncPath>? syncPaths;
        try
        {
            syncPaths = syncPathTask.Result;
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
        string? validationError = _initService.ValidateSyncPaths(syncPaths, Directory.GetCurrentDirectory());
        if (validationError != null)
        {
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to invalid sync path. {validationError}"
            );
            yield break;
        }

        Logger.LogDebug("Checking for data migration...");
        new Migrator(Directory.GetCurrentDirectory()).TryMigrate(Info.Metadata.Version, syncPaths);

        Logger.LogDebug("Loading configuration...");
        try
        {
            _configService.Initialize(Config, syncPaths);
        }
        catch (Exception e)
        {
            Logger.LogError($"Failed to bind sync path configuration:\n{e}");
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to error binding sync path configs. Please check your server configuration and try again."
            );
        }

        Logger.LogDebug("Loading previous sync data...");
        try
        {
            _previousSync = _initService.LoadPreviousSync(PreviousSyncPath);
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
        try
        {
            _localExclusions = _initService.LoadLocalExclusions(
                LocalExclusionsPath,
                _configService.IsHeadless(),
                _configService.GetHeadlessDefaultExclusions()
            );
        }
        catch (Exception e)
        {
            Logger.LogError(e);
            Chainloader.DependencyErrors.Add(
                $"Could not load {Info.Metadata.Name} due to error with local exclusions. Please check BepInEx/LogOutput.log for more information."
            );
            yield break;
        }

        Logger.LogDebug("Requesting exclusions from server...");

        List<string>? exclusions;
        Task<List<string>> exclusionsTask = _server.GetListExclusions();
        yield return new WaitUntil(() => exclusionsTask is { IsCompleted: true });
        try
        {
            exclusions = exclusionsTask.Result;
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
            Task<Dictionary<string, Dictionary<string, ModFile>>> remoteHashesTask =
                _server.GetRemoteHashes(EnabledSyncPaths);
            yield return new WaitUntil(() => remoteHashesTask is { IsCompleted: true });
            try
            {
                Dictionary<string, Dictionary<string, ModFile>>? remoteHashes = remoteHashesTask.Result;
                if (remoteHashes == null)
                {
                    Logger.LogError("Remote hashes task returned null");
                    yield break;
                }

                _remoteModFiles = remoteHashes;
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
            }
        }
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cts.Dispose();
        }
    }
}
