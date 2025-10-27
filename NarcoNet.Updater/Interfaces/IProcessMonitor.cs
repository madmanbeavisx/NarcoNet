namespace NarcoNet.Updater.Interfaces;

/// <summary>
///   Defines the contract for monitoring process lifecycle.
/// </summary>
public interface IProcessMonitor
{
  /// <summary>
  ///   Checks if a process with the specified ID is currently running.
  /// </summary>
  /// <param name="processId">The process ID to check.</param>
  /// <returns>True if the process is running; otherwise, false.</returns>
  bool IsProcessRunning(int processId);

  /// <summary>
  ///   Waits asynchronously for a process to exit.
  /// </summary>
  /// <param name="processId">The process ID to wait for.</param>
  /// <param name="cancellationToken">Token to cancel the wait operation.</param>
  /// <param name="progressCallback">Optional callback to report wait progress.</param>
  /// <returns>A task representing the asynchronous wait operation.</returns>
  Task WaitForProcessExitAsync(int processId, CancellationToken cancellationToken,
    Action<int>? progressCallback = null);
}
