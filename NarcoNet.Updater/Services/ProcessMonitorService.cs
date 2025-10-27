using System.Diagnostics;

using NarcoNet.Updater.Interfaces;

namespace NarcoNet.Updater.Services;

/// <summary>
///   The lookout - keeps eyes on witnesses until they leave the scene.
/// </summary>
public class ProcessMonitorService : IProcessMonitor
{
  private const int PollingIntervalMilliseconds = 1000;
  private readonly ILogger _logger;

  /// <summary>
  ///   Posts up the lookout with communication gear.
  /// </summary>
  /// <param name="logger">The radio to report back what we see.</param>
  public ProcessMonitorService(ILogger logger)
  {
    _logger = logger ?? throw new ArgumentNullException(nameof(logger));
  }

  /// <inheritdoc />
  public bool IsProcessRunning(int processId)
  {
    try
    {
      Process process = Process.GetProcessById(processId);
      return !process.HasExited;
    }
    catch (ArgumentException)
    {
      // Witness is gone - all clear
      return false;
    }
    catch (Exception ex)
    {
      _logger.LogException(ex, $"💥 Can't track witness {processId}");
      return false;
    }
  }

  /// <inheritdoc />
  public async Task WaitForProcessExitAsync(
    int processId,
    CancellationToken cancellationToken,
    Action<int>? progressCallback = null)
  {
    _logger.LogInformation($"⏳ Watching for witness {processId} to leave the scene...");

    int iterationCount = 0;

    while (!cancellationToken.IsCancellationRequested)
    {
      if (!IsProcessRunning(processId))
      {
        _logger.LogInformation($"👻 Witness {processId} has left the building!");
        break;
      }

      iterationCount++;
      _logger.LogInformation($"⏰ Witness {processId} still around (waited {iterationCount} seconds)");

      progressCallback?.Invoke(iterationCount);

      try
      {
        await Task.Delay(PollingIntervalMilliseconds, cancellationToken);
      }
      catch (TaskCanceledException)
      {
        _logger.LogWarning($"⚠️ Surveillance on witness {processId} was aborted!");
        throw;
      }
    }

    if (cancellationToken.IsCancellationRequested)
      _logger.LogWarning($"⚠️ Pulled the lookout before witness {processId} left. Operation incomplete!");
  }
}
