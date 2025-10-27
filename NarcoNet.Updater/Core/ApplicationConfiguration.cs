using NarcoNet.Utilities;

namespace NarcoNet.Updater.Core;

/// <summary>
///   Encapsulates the configuration for the updater application.
///   Implements the Value Object pattern for immutable configuration data.
/// </summary>
public sealed class ApplicationConfiguration
{
  /// <summary>
  ///   Initializes a new instance of the <see cref="ApplicationConfiguration" /> class.
  /// </summary>
  /// <param name="targetProcessId">The target process ID.</param>
  /// <param name="isSilentMode">Whether to run in silent mode.</param>
  /// <exception cref="ArgumentException">Thrown when targetProcessId is invalid.</exception>
  public ApplicationConfiguration(int targetProcessId, bool isSilentMode)
  {
    if (targetProcessId <= 0)
      throw new ArgumentException("Target process ID must be positive.", nameof(targetProcessId));

    TargetProcessId = targetProcessId;
    IsSilentMode = isSilentMode;
  }

  /// <summary>
  ///   Gets the target process ID to monitor.
  /// </summary>
  public int TargetProcessId { get; }

  /// <summary>
  ///   Gets a value indicating whether the updater should run in silent mode (no UI).
  /// </summary>
  public bool IsSilentMode { get; }

  /// <summary>
  ///   Gets the usage message for command-line arguments.
  /// </summary>
  public static string UsageMessage =>
    $"Usage: {NarcoNetConstants.UpdaterExecutableName} [--silent] <Process ID>\n\n" +
    "Arguments:\n" +
    "  <Process ID>    The process ID to monitor before applying updates\n" +
    "\n" +
    "Options:\n" +
    "  --silent        Run in silent mode (no UI, console output only)";

  /// <summary>
  ///   Creates a configuration from command-line arguments.
  /// </summary>
  /// <param name="args">The command-line arguments.</param>
  /// <param name="error">Error message if parsing fails.</param>
  /// <returns>The parsed configuration, or null if parsing failed.</returns>
  public static ApplicationConfiguration? TryParseFromArguments(string[] args, out string? error)
  {
    error = null;

    if (args == null || args.Length == 0)
    {
      error = "No arguments provided.";
      return null;
    }

    // Separate options from positional arguments
    List<string> options = args.Where(arg => arg.StartsWith("--", StringComparison.Ordinal)).ToList();
    List<string> positionalArgs = args.Except(options).ToList();

    if (positionalArgs.Count == 0)
    {
      error = "Missing required process ID argument.";
      return null;
    }

    // Parse silent mode flag
    bool isSilentMode = options.Contains("--silent", StringComparer.OrdinalIgnoreCase);

    // Parse process ID
    string processIdArgument = positionalArgs.Last();

    if (!int.TryParse(processIdArgument, out int processId))
    {
      error = $"Invalid process ID: '{processIdArgument}'. Must be a valid integer.";
      return null;
    }

    if (processId <= 0)
    {
      error = $"Invalid process ID: {processId}. Must be a positive integer.";
      return null;
    }

    return new ApplicationConfiguration(processId, isSilentMode);
  }

  /// <inheritdoc />
  public override string ToString()
  {
    return $"ProcessId={TargetProcessId}, SilentMode={IsSilentMode}";
  }
}
