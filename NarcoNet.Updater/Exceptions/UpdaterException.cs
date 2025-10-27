using System.Runtime.Serialization;

namespace NarcoNet.Updater.Exceptions;

/// <summary>
///   Base exception for all updater-related errors.
///   Provides a custom exception hierarchy for better error handling.
/// </summary>
[Serializable]
public class UpdaterException : Exception
{
  public UpdaterException()
  {
    ErrorCode = "UPDATER_ERROR";
  }

  public UpdaterException(string message) : base(message)
  {
    ErrorCode = "UPDATER_ERROR";
  }

  public UpdaterException(string message, Exception innerException) : base(message, innerException)
  {
    ErrorCode = "UPDATER_ERROR";
  }

  public UpdaterException(string message, string errorCode) : base(message)
  {
    ErrorCode = errorCode;
  }

  public UpdaterException(string message, string errorCode, Exception innerException) : base(message, innerException)
  {
    ErrorCode = errorCode;
  }

  protected UpdaterException(SerializationInfo info, StreamingContext context) : base(info, context)
  {
    ErrorCode = info.GetString(nameof(ErrorCode)) ?? "UPDATER_ERROR";
  }

  /// <summary>
  ///   Gets the error code associated with this exception.
  /// </summary>
  public string ErrorCode { get; }

  public override void GetObjectData(SerializationInfo info, StreamingContext context)
  {
    base.GetObjectData(info, context);
    info.AddValue(nameof(ErrorCode), ErrorCode);
  }
}

/// <summary>
///   Exception thrown when environment validation fails.
/// </summary>
[Serializable]
public class EnvironmentValidationException : UpdaterException
{
  public EnvironmentValidationException(string message)
    : base(message, "ENV_VALIDATION_FAILED")
  {
  }

  public EnvironmentValidationException(string message, Exception innerException)
    : base(message, "ENV_VALIDATION_FAILED", innerException)
  {
  }

  protected EnvironmentValidationException(SerializationInfo info, StreamingContext context)
    : base(info, context)
  {
  }
}

/// <summary>
///   Exception thrown when file operations fail.
/// </summary>
[Serializable]
public class FileOperationException : UpdaterException
{
  public FileOperationException(string message, string filePath)
    : base(message, "FILE_OPERATION_FAILED")
  {
    FilePath = filePath;
  }

  public FileOperationException(string message, string filePath, Exception innerException)
    : base(message, "FILE_OPERATION_FAILED", innerException)
  {
    FilePath = filePath;
  }

  protected FileOperationException(SerializationInfo info, StreamingContext context)
    : base(info, context)
  {
    FilePath = info.GetString(nameof(FilePath));
  }

  /// <summary>
  ///   Gets the file path associated with the error.
  /// </summary>
  public string? FilePath { get; }

  public override void GetObjectData(SerializationInfo info, StreamingContext context)
  {
    base.GetObjectData(info, context);
    info.AddValue(nameof(FilePath), FilePath);
  }
}

/// <summary>
///   Exception thrown when process monitoring fails.
/// </summary>
[Serializable]
public class ProcessMonitoringException : UpdaterException
{
  public ProcessMonitoringException(string message, int processId)
    : base(message, "PROCESS_MONITORING_FAILED")
  {
    ProcessId = processId;
  }

  public ProcessMonitoringException(string message, int processId, Exception innerException)
    : base(message, "PROCESS_MONITORING_FAILED", innerException)
  {
    ProcessId = processId;
  }

  protected ProcessMonitoringException(SerializationInfo info, StreamingContext context)
    : base(info, context)
  {
    ProcessId = info.GetInt32(nameof(ProcessId));
  }

  /// <summary>
  ///   Gets the process ID associated with the error.
  /// </summary>
  public int ProcessId { get; }

  public override void GetObjectData(SerializationInfo info, StreamingContext context)
  {
    base.GetObjectData(info, context);
    info.AddValue(nameof(ProcessId), ProcessId);
  }
}

/// <summary>
///   Exception thrown when configuration is invalid.
/// </summary>
[Serializable]
public class ConfigurationException : UpdaterException
{
  public ConfigurationException(string message, string configurationKey)
    : base(message, "CONFIGURATION_INVALID")
  {
    ConfigurationKey = configurationKey;
  }

  public ConfigurationException(string message, string configurationKey, Exception innerException)
    : base(message, "CONFIGURATION_INVALID", innerException)
  {
    ConfigurationKey = configurationKey;
  }

  protected ConfigurationException(SerializationInfo info, StreamingContext context)
    : base(info, context)
  {
    ConfigurationKey = info.GetString(nameof(ConfigurationKey));
  }

  /// <summary>
  ///   Gets the configuration key that caused the error.
  /// </summary>
  public string? ConfigurationKey { get; }

  public override void GetObjectData(SerializationInfo info, StreamingContext context)
  {
    base.GetObjectData(info, context);
    info.AddValue(nameof(ConfigurationKey), ConfigurationKey);
  }
}
