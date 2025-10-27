using System.Diagnostics;

using NarcoNet.Utilities;

namespace NarcoNet.Updater.Infrastructure;

/// <summary>
///   Represents the result of a health check.
/// </summary>
public enum HealthStatus
{
  Healthy,
  Degraded,
  Unhealthy
}

/// <summary>
///   Represents the result of a health check operation.
/// </summary>
public sealed class HealthCheckResult
{
  public HealthStatus Status { get; init; }
  public string Description { get; init; } = string.Empty;
  public Dictionary<string, object> Data { get; init; } = new();
  public TimeSpan Duration { get; init; }
  public Exception? Exception { get; init; }

  public static HealthCheckResult Healthy(string description, Dictionary<string, object>? data = null,
    TimeSpan? duration = null)
  {
    return new HealthCheckResult
    {
      Status = HealthStatus.Healthy,
      Description = description,
      Data = data ?? new Dictionary<string, object>(),
      Duration = duration ?? TimeSpan.Zero
    };
  }

  public static HealthCheckResult Degraded(string description, Dictionary<string, object>? data = null,
    TimeSpan? duration = null)
  {
    return new HealthCheckResult
    {
      Status = HealthStatus.Degraded,
      Description = description,
      Data = data ?? new Dictionary<string, object>(),
      Duration = duration ?? TimeSpan.Zero
    };
  }

  public static HealthCheckResult Unhealthy(string description, Exception? exception = null,
    Dictionary<string, object>? data = null, TimeSpan? duration = null)
  {
    return new HealthCheckResult
    {
      Status = HealthStatus.Unhealthy,
      Description = description,
      Exception = exception,
      Data = data ?? new Dictionary<string, object>(),
      Duration = duration ?? TimeSpan.Zero
    };
  }
}

/// <summary>
///   Provides health check functionality for the updater application.
///   Implements the Health Check pattern for monitoring system health.
/// </summary>
public class HealthCheck
{
  /// <summary>
  ///   Checks if the environment is ready for update operations.
  /// </summary>
  public static async Task<HealthCheckResult> CheckEnvironmentAsync(CancellationToken cancellationToken = default)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    Dictionary<string, object> data = new();

    try
    {
      // Check if running directory is valid
      string currentDirectory = Directory.GetCurrentDirectory();
      data["CurrentDirectory"] = currentDirectory;

      // Check if EscapeFromTarkov.exe exists
      string tarkovExePath = Path.Combine(currentDirectory, "EscapeFromTarkov.exe");
      bool tarkovExists = File.Exists(tarkovExePath);
      data["TarkovExeExists"] = tarkovExists;

      if (!tarkovExists)
        return HealthCheckResult.Unhealthy(
          "EscapeFromTarkov.exe not found in current directory",
          data: data,
          duration: stopwatch.Elapsed
        );

      // Check if NarcoNet_Data directory exists
      string dataDirectory = Path.Combine(currentDirectory, NarcoNetConstants.DataDirectoryName);
      bool dataDirectoryExists = Directory.Exists(dataDirectory);
      data["DataDirectoryExists"] = dataDirectoryExists;

      if (!dataDirectoryExists)
        return HealthCheckResult.Unhealthy(
          "NarcoNet_Data directory not found",
          data: data,
          duration: stopwatch.Elapsed
        );

      // Check disk space
      DriveInfo driveInfo = new(Path.GetPathRoot(currentDirectory) ?? "C:");
      double availableSpaceGB = driveInfo.AvailableFreeSpace / (1024.0 * 1024.0 * 1024.0);
      data["AvailableDiskSpaceGB"] = Math.Round(availableSpaceGB, 2);

      if (availableSpaceGB < 1.0) // Less than 1GB
        return HealthCheckResult.Degraded(
          $"Low disk space: {availableSpaceGB:F2} GB available",
          data,
          stopwatch.Elapsed
        );

      // Check write permissions
      string testFile = Path.Combine(currentDirectory, $".narconet_write_test_{Guid.NewGuid()}.tmp");
      try
      {
        await File.WriteAllTextAsync(testFile, "test", cancellationToken);
        File.Delete(testFile);
        data["WritePermissions"] = true;
      }
      catch
      {
        data["WritePermissions"] = false;
        return HealthCheckResult.Unhealthy(
          "No write permissions in current directory",
          data: data,
          duration: stopwatch.Elapsed
        );
      }

      return HealthCheckResult.Healthy(
        "Environment is healthy and ready for updates",
        data,
        stopwatch.Elapsed
      );
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy(
        "Health check failed with exception",
        ex,
        data,
        stopwatch.Elapsed
      );
    }
  }

  /// <summary>
  ///   Checks if pending updates are valid and can be applied.
  /// </summary>
  public static async Task<HealthCheckResult> CheckPendingUpdatesAsync(string updateDirectory,
    CancellationToken cancellationToken = default)
  {
    Stopwatch stopwatch = Stopwatch.StartNew();
    Dictionary<string, object> data = new();

    try
    {
      data["UpdateDirectory"] = updateDirectory;

      if (!Directory.Exists(updateDirectory))
        return HealthCheckResult.Healthy(
          "No pending updates",
          data,
          stopwatch.Elapsed
        );

      string[] updateFiles = Directory.GetFiles(updateDirectory, "*", SearchOption.AllDirectories);
      data["UpdateFileCount"] = updateFiles.Length;

      if (updateFiles.Length == 0)
        return HealthCheckResult.Healthy(
          "No pending updates",
          data,
          stopwatch.Elapsed
        );

      // Calculate total size
      long totalSize = updateFiles.Sum(f => new FileInfo(f).Length);
      double totalSizeMB = totalSize / (1024.0 * 1024.0);
      data["TotalUpdateSizeMB"] = Math.Round(totalSizeMB, 2);

      // Check for suspicious files
      string[] suspiciousExtensions = new[] { ".exe", ".dll", ".bat", ".cmd", ".ps1" };
      List<string> suspiciousFiles = updateFiles
        .Where(f => suspiciousExtensions.Contains(Path.GetExtension(f).ToLowerInvariant())).ToList();
      data["SuspiciousFileCount"] = suspiciousFiles.Count;

      if (suspiciousFiles.Any())
      {
        data["SuspiciousFiles"] = suspiciousFiles.Select(Path.GetFileName).ToList();
        return HealthCheckResult.Degraded(
          $"Found {suspiciousFiles.Count} potentially executable files in update",
          data,
          stopwatch.Elapsed
        );
      }

      return HealthCheckResult.Healthy(
        $"{updateFiles.Length} files ready for update ({totalSizeMB:F2} MB)",
        data,
        stopwatch.Elapsed
      );
    }
    catch (Exception ex)
    {
      return HealthCheckResult.Unhealthy(
        "Failed to check pending updates",
        ex,
        data,
        stopwatch.Elapsed
      );
    }
  }

  /// <summary>
  ///   Performs a comprehensive health check of all systems.
  /// </summary>
  public static async Task<Dictionary<string, HealthCheckResult>> CheckAllAsync(
    CancellationToken cancellationToken = default)
  {
    Dictionary<string, HealthCheckResult> results = new();

    // Run checks in parallel for better performance
    Task<HealthCheckResult> environmentTask = CheckEnvironmentAsync(cancellationToken);

    string dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), NarcoNetConstants.DataDirectoryName);
    string updateDirectory = Path.Combine(dataDirectory, NarcoNetConstants.PendingUpdatesDirectoryName);
    Task<HealthCheckResult> updatesTask = CheckPendingUpdatesAsync(updateDirectory, cancellationToken);

    await Task.WhenAll(environmentTask, updatesTask);

    results["Environment"] = await environmentTask;
    results["PendingUpdates"] = await updatesTask;

    return results;
  }

  /// <summary>
  ///   Gets the overall health status from multiple check results.
  /// </summary>
  public static HealthStatus GetOverallStatus(Dictionary<string, HealthCheckResult> results)
  {
    if (results.Values.Any(r => r.Status == HealthStatus.Unhealthy))
      return HealthStatus.Unhealthy;

    if (results.Values.Any(r => r.Status == HealthStatus.Degraded))
      return HealthStatus.Degraded;

    return HealthStatus.Healthy;
  }
}
