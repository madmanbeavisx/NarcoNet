using System.Diagnostics;

using NarcoNet.Updater.Interfaces;

namespace NarcoNet.Updater.Services;

/// <summary>
///     The lookout - keeps eyes on witnesses until they leave the scene.
/// </summary>
public class ProcessMonitorService : IProcessMonitor
{
    private const int PollingIntervalMilliseconds = 1000;
    private readonly ILogger _logger;

    /// <summary>
    ///     Posts up the lookout with communication gear.
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
            _logger.LogException(ex, $"Failed to check process {processId}");
            return false;
        }
    }

    /// <inheritdoc />
    public async Task WaitForProcessExitAsync(
        int processId,
        CancellationToken cancellationToken,
        Action<int>? progressCallback = null)
    {
        _logger.LogDebug($"Waiting for process {processId} to exit...");

        int iterationCount = 0;

        while (!cancellationToken.IsCancellationRequested)
        {
            if (!IsProcessRunning(processId))
            {
                _logger.LogDebug($"Process {processId} has exited");
                break;
            }

            iterationCount++;
            _logger.LogDebug($"Process {processId} still running (waited {iterationCount} seconds)");

            progressCallback?.Invoke(iterationCount);

            try
            {
                await Task.Delay(PollingIntervalMilliseconds, cancellationToken);
            }
            catch (TaskCanceledException)
            {
                _logger.LogWarning($"Process monitoring for {processId} was cancelled");
                throw;
            }
        }

        if (cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning($"Process monitoring for {processId} was cancelled before exit");
        }
    }
}
