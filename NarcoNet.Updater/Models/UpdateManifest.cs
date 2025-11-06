namespace NarcoNet.Updater.Models;

/// <summary>
/// Represents a manifest of operations to be performed by the updater
/// </summary>
public class UpdateManifest
{
    /// <summary>
    /// List of operations to perform
    /// </summary>
    public List<UpdateOperation> Operations { get; set; } = [];

    /// <summary>
    /// Remote sync data to write to PreviousSync.json after successful update
    /// </summary>
    public Dictionary<string, Dictionary<string, object>>? RemoteSyncData { get; set; }
}

/// <summary>
/// Represents a single operation to be performed
/// </summary>
public class UpdateOperation
{
    /// <summary>
    /// Type of operation to perform
    /// </summary>
    public OperationType Type { get; set; }

    /// <summary>
    /// Source path (relative to pending updates directory)
    /// </summary>
    public string? Source { get; set; }

    /// <summary>
    /// Destination path (relative to target directory)
    /// </summary>
    public string? Destination { get; set; }

    /// <summary>
    /// Additional parameters for the operation (used for future extensibility)
    /// </summary>
    public Dictionary<string, string>? Parameters { get; set; }
}

/// <summary>
/// Types of operations the updater can perform
/// </summary>
public enum OperationType
{
    /// <summary>
    /// Copy a file from source to destination
    /// </summary>
    CopyFile,

    /// <summary>
    /// Create a directory at destination
    /// </summary>
    CreateDirectory,

    /// <summary>
    /// Delete a file at destination
    /// </summary>
    DeleteFile,

    /// <summary>
    /// Move a file from source to destination
    /// </summary>
    MoveFile,

    /// <summary>
    /// Extract a compressed archive (future use)
    /// </summary>
    ExtractArchive,

    /// <summary>
    /// Decrypt a file (future use)
    /// </summary>
    DecryptFile
}
