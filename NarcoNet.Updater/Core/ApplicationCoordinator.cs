using NarcoNet.Updater.Interfaces;
using NarcoNet.Updater.Services;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.Core;

/// <summary>
///     The cleanup crew boss - coordinates the whole operation for moving packages.
///     Implements the Facade pattern to keep the operation organized.
/// </summary>
public sealed class ApplicationCoordinator
{
    private readonly ApplicationConfiguration _configuration;
    private readonly IFileUpdateService _fileUpdateService;
    private readonly ILogger _logger;
    private readonly IProcessMonitor _processMonitor;
    private readonly IUserInterfaceService _uiService;

    /// <summary>
    ///     Assembles the cleanup crew and gets everyone ready for the operation.
    /// </summary>
    /// <param name="configuration">The operation orders from headquarters.</param>
    public ApplicationCoordinator(ApplicationConfiguration configuration)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));

        // Assemble the crew
        _logger = FileLogger.Instance;
        _uiService = new UserInterfaceService();
        _processMonitor = new ProcessMonitorService(_logger);

        // Set up the package handlers
        string dataDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            NarcoNetConstants.DataDirectoryName
        );

        string updateDirectory = Path.Combine(
            dataDirectory,
            NarcoNetConstants.PendingUpdatesDirectoryName
        );

        string removedFilesPath = Path.Combine(
            dataDirectory,
            NarcoNetConstants.RemovedFilesFileName
        );

        string updateManifestPath = Path.Combine(
            dataDirectory,
            NarcoNetConstants.UpdateManifestFileName
        );

        _fileUpdateService = new FileUpdateService(
            _logger,
            updateDirectory,
            removedFilesPath,
            Directory.GetCurrentDirectory(),
            updateManifestPath
        );
    }

    /// <summary>
    ///     Runs the operation (either ghost protocol or full visibility mode).
    /// </summary>
    /// <returns>The operation result code (0 = success, other = something went wrong).</returns>
    public int Execute()
    {
        try
        {
            _logger.LogInformation($"Starting {NarcoNetConstants.FullProductName} updater");
            _logger.LogDebug($"Mode: {(_configuration.IsSilentMode ? "Silent" : "GUI")}");
            _logger.LogDebug($"Target process ID: {_configuration.TargetProcessId}");

            if (!ValidateExecutionEnvironment())
            {
                return ExitCode.EnvironmentValidationFailed;
            }

            if (!_fileUpdateService.HasPendingUpdates())
            {
                _logger.LogDebug("No pending updates found.");
                return ExitCode.Success;
            }

            if (_configuration.IsSilentMode)
            {
                return ExecuteSilentMode();
            }

            return ExecuteGraphicalMode();
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Fatal error in updater");
            return ExitCode.UnexpectedError;
        }
    }

    /// <summary>
    ///     Verifies we're in the right territory before starting the operation.
    /// </summary>
    /// <returns>True if the coast is clear; false if we're in the wrong place.</returns>
    private bool ValidateExecutionEnvironment()
    {
        // Make sure we're in the right neighborhood
        if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "EscapeFromTarkov.exe")))
        {
            _logger.LogError("EscapeFromTarkov.exe not found in current directory");

            if (!_configuration.IsSilentMode)
            {
                _uiService.ShowError(
                    NarcoNetConstants.Messages.ErrorTarkovNotFound,
                    "Environment Validation Failed"
                );
            }

            return false;
        }

        // Make sure the stash house is where it should be
        string dataDirectory = Path.Combine(
            Directory.GetCurrentDirectory(),
            NarcoNetConstants.DataDirectoryName
        );

        if (!Directory.Exists(dataDirectory))
        {
            _logger.LogError("Staging directory does not exist");

            if (!_configuration.IsSilentMode)
            {
                _uiService.ShowError(
                    NarcoNetConstants.Messages.ErrorDataDirectoryNotFound,
                    "Environment Validation Failed"
                );
            }

            return false;
        }

        _logger.LogDebug("Environment validation completed");
        return true;
    }

    /// <summary>
    ///     Runs the operation under ghost protocol (silent, no witnesses).
    /// </summary>
    /// <returns>The operation result code.</returns>
    private int ExecuteSilentMode()
    {
        try
        {
            _logger.LogInformation("🤫 Going dark... silent operation initiated...");

            // Wait for witness to leave the scene
            WaitForProcessExitSynchronously();

            // Move the packages into position
            _logger.LogDebug("Applying pending updates...");
            _fileUpdateService.ApplyPendingUpdatesAsync(CancellationToken.None).Wait();

            // Execute the hit list
            _logger.LogDebug("Deleting removed files...");
            _fileUpdateService.DeleteRemovedFilesAsync(CancellationToken.None).Wait();

            _logger.LogInformation("Update completed successfully");
            return ExitCode.Success;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Silent update failed");
            return ExitCode.UpdateFailed;
        }
    }

    /// <summary>
    ///     Runs the operation in broad daylight (with progress window for all to see).
    /// </summary>
    /// <returns>The operation result code.</returns>
    private int ExecuteGraphicalMode()
    {
        try
        {
            _logger.LogDebug("Running in GUI mode...");

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            DialogResult result = _uiService.ShowProgressWindow(_configuration.TargetProcessId);

            return result == DialogResult.OK ? ExitCode.Success : ExitCode.UserCancelled;
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "GUI update failed");
            return ExitCode.UpdateFailed;
        }
    }

    /// <summary>
    ///     Keeps watch until the witness leaves the building (for ghost protocol operations).
    /// </summary>
    private void WaitForProcessExitSynchronously()
    {
        int iterationCount = 0;

        while (_processMonitor.IsProcessRunning(_configuration.TargetProcessId))
        {
            iterationCount++;
            _logger.LogDebug($"Waiting for target process to exit (check #{iterationCount})...");
            Thread.Sleep(1000);
        }

        _logger.LogDebug("Target process exited, proceeding with update");
    }

    /// <summary>
    ///     Operation result codes - how'd the job go, boss?
    /// </summary>
    public static class ExitCode
    {
        public const int Success = 0;
        public const int InvalidArguments = 1;
        public const int EnvironmentValidationFailed = 2;
        public const int UpdateFailed = 3;
        public const int UserCancelled = 4;
        public const int UnexpectedError = 99;
    }
}
