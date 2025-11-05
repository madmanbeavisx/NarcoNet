using NarcoNet.Updater.Core;
using NarcoNet.Updater.Services;

namespace NarcoNet.Updater;

/// <summary>
///     The main entry point for the NarcoNet Updater application.
///     Follows the Single Responsibility Principle by delegating work to specialized components.
/// </summary>
public static class ProgramEntry
{
    /// <summary>
    ///     The main entry point for the application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            // Parse and validate command-line arguments
            ApplicationConfiguration? configuration =
                ApplicationConfiguration.TryParseFromArguments(args, out string? parseError);

            if (configuration == null)
            {
                HandleInvalidArguments(parseError ?? "Unknown error", args.Contains("--silent"));
                return ApplicationCoordinator.ExitCode.InvalidArguments;
            }

            // Create and execute the application coordinator
            ApplicationCoordinator coordinator = new(configuration);
            return coordinator.Execute();
        }
        catch (Exception ex)
        {
            // Last resort error handling
            Console.WriteLine($"Fatal error: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            return ApplicationCoordinator.ExitCode.UnexpectedError;
        }
    }

    /// <summary>
    ///     Handles invalid command-line arguments by displaying usage information.
    /// </summary>
    /// <param name="errorMessage">The error message describing what went wrong.</param>
    /// <param name="isSilentMode">Whether the application is in silent mode.</param>
    private static void HandleInvalidArguments(string errorMessage, bool isSilentMode)
    {
        Console.WriteLine($"Invalid arguments: {errorMessage}");
        Console.WriteLine();
        Console.WriteLine(ApplicationConfiguration.UsageMessage);

        if (!isSilentMode)
        {
            UserInterfaceService uiService = new();
            uiService.ShowWarning(
                $"{errorMessage}\n\n{ApplicationConfiguration.UsageMessage}",
                "Invalid Arguments"
            );
        }
    }
}
