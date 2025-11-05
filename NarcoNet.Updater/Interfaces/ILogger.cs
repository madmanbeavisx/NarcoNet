namespace NarcoNet.Updater.Interfaces;

/// <summary>
///     Defines the contract for logging operations in the updater application.
/// </summary>
public interface ILogger
{
    /// <summary>
    ///     Logs an informational message.
    /// </summary>
    /// <param name="message">The message to log.</param>
    void LogInformation(string message);

    /// <summary>
    ///     Logs a warning message.
    /// </summary>
    /// <param name="message">The warning message to log.</param>
    void LogWarning(string message);

    /// <summary>
    ///     Logs an error message.
    /// </summary>
    /// <param name="message">The error message to log.</param>
    void LogError(string message);

    /// <summary>
    ///     Logs an exception with an optional message.
    /// </summary>
    /// <param name="exception">The exception to log.</param>
    /// <param name="message">Optional message to accompany the exception.</param>
    void LogException(Exception exception, string? message = null);

    /// <summary>
    /// Logs a debug message for tracing or diagnostic purposes.
    /// </summary>
    /// <param name="message">The debug message to log.</param>
    /// <param name="exception">Optional exception to include additional context.</param>
    void LogDebug(string message, Exception? exception = null);
}
