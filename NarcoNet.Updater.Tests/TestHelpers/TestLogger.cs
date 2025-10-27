using NarcoNet.Updater.Interfaces;

namespace NarcoNet.Updater.Tests.TestHelpers;

/// <summary>
///   In-memory logger for testing that captures all log entries.
/// </summary>
public class TestLogger : ILogger
{
  private readonly object _lock = new();
  private readonly List<LogEntry> _logEntries = new();

  public IReadOnlyList<LogEntry> LogEntries
  {
    get
    {
      lock (_lock)
      {
        return _logEntries.ToList();
      }
    }
  }

  public void LogInformation(string message)
  {
    AddLogEntry(LogLevel.Information, message);
  }

  public void LogWarning(string message)
  {
    AddLogEntry(LogLevel.Warning, message);
  }

  public void LogError(string message)
  {
    AddLogEntry(LogLevel.Error, message);
  }

  public void LogException(Exception exception, string? message = null)
  {
    AddLogEntry(LogLevel.Error, message ?? exception.Message, exception);
  }

  private void AddLogEntry(LogLevel level, string message, Exception? exception = null)
  {
    lock (_lock)
    {
      _logEntries.Add(new LogEntry(level, message, exception, DateTime.UtcNow));
    }
  }

  public void Clear()
  {
    lock (_lock)
    {
      _logEntries.Clear();
    }
  }

  public bool HasLogLevel(LogLevel level)
  {
    return _logEntries.Any(e => e.Level == level);
  }

  public bool ContainsMessage(string substring)
  {
    return _logEntries.Any(e => e.Message.Contains(substring, StringComparison.OrdinalIgnoreCase));
  }

  public int GetLogCount(LogLevel level)
  {
    return _logEntries.Count(e => e.Level == level);
  }
}

public enum LogLevel
{
  Information,
  Warning,
  Error
}

public record LogEntry(LogLevel Level, string Message, Exception? Exception, DateTime Timestamp);
