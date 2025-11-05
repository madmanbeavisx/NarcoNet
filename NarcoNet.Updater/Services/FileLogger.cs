using NarcoNet.Updater.Interfaces;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.Services;

/// <summary>
///     Provides file-based logging functionality with console output.
///     Implements the Singleton pattern to ensure single instance access.
/// </summary>
public sealed class FileLogger : ILogger
{
    private static readonly Lazy<FileLogger> LazyInstance = new(() => new FileLogger());
    private readonly object _lockObject = new();
    private readonly string _logFilePath;

    /// <summary>
    ///     Private constructor to enforce singleton pattern.
    /// </summary>
    private FileLogger()
    {
        string dataDirectory = Path.Combine(Directory.GetCurrentDirectory(), NarcoNetConstants.DataDirectoryName);
        _logFilePath = Path.Combine(dataDirectory, NarcoNetConstants.LogFileName);

        EnsureLogFileExists();
    }

    private FileLogger(string logFilePath)
    {
        _logFilePath = logFilePath;
        EnsureLogFileExists();
    }

    /// <summary>
    ///     Gets the singleton instance of the FileLogger.
    /// </summary>
    public static FileLogger Instance => LazyInstance.Value;

    /// <inheritdoc />
    public void LogInformation(string message)
    {
        WriteLog("INFO", message);
    }

    /// <inheritdoc />
    public void LogWarning(string message)
    {
        WriteLog("WARN", message);
    }

    /// <inheritdoc />
    public void LogError(string message)
    {
        WriteLog("ERROR", message);
    }

    /// <inheritdoc />
    public void LogDebug(string message, Exception? exception = null)
    {
        throw new NotImplementedException();
    }
    
    /// <inheritdoc />
    public void LogException(Exception exception, string? message = null)
    {
        string exceptionMessage = message != null
            ? $"{message} - Exception: {exception.Message}"
            : $"Exception: {exception.Message}";

        WriteLog("ERROR", exceptionMessage);
        WriteLog("ERROR", $"Stack Trace: {exception.StackTrace}");
    }
    
    /// <summary>
    ///     Creates a new logger instance with a custom log file path.
    /// </summary>
    /// <param name="logFilePath">The path to the log file.</param>
    /// <returns>A new FileLogger instance.</returns>
    public static FileLogger Create(string logFilePath)
    {
        return new FileLogger(logFilePath);
    }

    /// <summary>
    ///     Writes a log entry with the specified level and message.
    /// </summary>
    /// <param name="level">The log level (INFO, WARN, ERROR).</param>
    /// <param name="message">The message to log.</param>
    private void WriteLog(string level, string message)
    {
        string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        string formattedMessage = $"[{timestamp}] [{level}] {NarcoNetConstants.UpdaterLogPrefix}: {message}";

        // Write to console
        Console.WriteLine(formattedMessage);

        // Write to file (thread-safe)
        lock (_lockObject)
        {
            try
            {
                File.AppendAllText(_logFilePath, formattedMessage + Environment.NewLine);
            }
            catch (Exception ex)
            {
                // If we can't write to the log file, at least output to console
                Console.WriteLine($"Failed to write to log file: {ex.Message}");
            }
        }
    }

    /// <summary>
    ///     Ensures the log file and its directory exist.
    /// </summary>
    private void EnsureLogFileExists()
    {
        try
        {
            string? directory = Path.GetDirectoryName(_logFilePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }

            File.Create(_logFilePath).Dispose();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Warning: Could not initialize log file: {ex.Message}");
        }
    }
}
