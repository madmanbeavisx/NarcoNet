using NarcoNet.Updater.Interfaces;
using NarcoNet.Updater.Services;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.Core;

/// <summary>
///   The cleanup crew boss - coordinates the whole operation for moving packages.
///   Implements the Facade pattern to keep the operation organized.
/// </summary>
public sealed class ApplicationCoordinator
{
  private readonly ApplicationConfiguration _configuration;
  private readonly IFileUpdateService _fileUpdateService;
  private readonly ILogger _logger;
  private readonly IProcessMonitor _processMonitor;
  private readonly IUserInterfaceService _uiService;

  /// <summary>
  ///   Assembles the cleanup crew and gets everyone ready for the operation.
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

    _fileUpdateService = new FileUpdateService(
      _logger,
      updateDirectory,
      removedFilesPath,
      Directory.GetCurrentDirectory()
    );
  }

  /// <summary>
  ///   Runs the operation (either ghost protocol or full visibility mode).
  /// </summary>
  /// <returns>The operation result code (0 = success, other = something went wrong).</returns>
  public int Execute()
  {
    try
    {
      _logger.LogInformation($"🔧 Starting {NarcoNetConstants.FullProductName} cleanup crew");
      _logger.LogInformation($"🎭 Operation mode: {(_configuration.IsSilentMode ? "Silent (Ghost Protocol)" : "Visible (Standard Operation)")}");
      _logger.LogInformation($"🎯 Target witness ID: {_configuration.TargetProcessId}");

      if (!ValidateExecutionEnvironment()) return ExitCode.EnvironmentValidationFailed;

      if (!_fileUpdateService.HasPendingUpdates())
      {
        _logger.LogInformation("📭 No packages to move. The shipment is clean. Standing down.");
        return ExitCode.Success;
      }

      if (_configuration.IsSilentMode) return ExecuteSilentMode();

      return ExecuteGraphicalMode();
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💀 Operation went sideways! The crew is compromised!");
      return ExitCode.UnexpectedError;
    }
  }

  /// <summary>
  ///   Verifies we're in the right territory before starting the operation.
  /// </summary>
  /// <returns>True if the coast is clear; false if we're in the wrong place.</returns>
  private bool ValidateExecutionEnvironment()
  {
    // Make sure we're in the right neighborhood
    if (!File.Exists(Path.Combine(Directory.GetCurrentDirectory(), "EscapeFromTarkov.exe")))
    {
      _logger.LogError("💥 Can't find the target location! This isn't the right territory, compadre!");

      if (!_configuration.IsSilentMode)
        _uiService.ShowError(
          NarcoNetConstants.Messages.ErrorTarkovNotFound,
          "Environment Validation Failed"
        );

      return false;
    }

    // Make sure the stash house is where it should be
    string dataDirectory = Path.Combine(
      Directory.GetCurrentDirectory(),
      NarcoNetConstants.DataDirectoryName
    );

    if (!Directory.Exists(dataDirectory))
    {
      _logger.LogError("💥 The stash house is missing! Someone cleaned out the warehouse!");

      if (!_configuration.IsSilentMode)
        _uiService.ShowError(
          NarcoNetConstants.Messages.ErrorDataDirectoryNotFound,
          "Environment Validation Failed"
        );

      return false;
    }

    _logger.LogInformation("✅ Territory secured. The coast is clear, let's move!");
    return true;
  }

  /// <summary>
  ///   Runs the operation under ghost protocol (silent, no witnesses).
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
      _logger.LogInformation("📦 Moving the merchandise into position...");
      _fileUpdateService.ApplyPendingUpdatesAsync(CancellationToken.None).Wait();

      // Execute the hit list
      _logger.LogInformation("🗑️ Disposing of the evidence...");
      _fileUpdateService.DeleteRemovedFilesAsync(CancellationToken.None).Wait();

      _logger.LogInformation("✅ Job's done, patron. The package is delivered!");
      return ExitCode.Success;
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💥 Silent op went loud! We got made!");
      return ExitCode.UpdateFailed;
    }
  }

  /// <summary>
  ///   Runs the operation in broad daylight (with progress window for all to see).
  /// </summary>
  /// <returns>The operation result code.</returns>
  private int ExecuteGraphicalMode()
  {
    try
    {
      _logger.LogInformation("🎬 Running the operation in full view...");

      Application.EnableVisualStyles();
      Application.SetCompatibleTextRenderingDefault(false);

      DialogResult result = _uiService.ShowProgressWindow(_configuration.TargetProcessId);

      return result == DialogResult.OK ? ExitCode.Success : ExitCode.UserCancelled;
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, "💥 The operation fell apart! Too many witnesses!");
      return ExitCode.UpdateFailed;
    }
  }

  /// <summary>
  ///   Keeps watch until the witness leaves the building (for ghost protocol operations).
  /// </summary>
  private void WaitForProcessExitSynchronously()
  {
    int iterationCount = 0;

    while (_processMonitor.IsProcessRunning(_configuration.TargetProcessId))
    {
      iterationCount++;
      _logger.LogInformation($"⏳ Waiting for the witness to leave the scene (check #{iterationCount})...");
      Thread.Sleep(1000);
    }

    _logger.LogInformation("👻 The witness has left. Moving in now!");
  }

  /// <summary>
  ///   Operation result codes - how'd the job go, boss?
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
