namespace NarcoNet.Server.Models;

/// <summary>
///     Represents a single file change operation
/// </summary>
public record FileChangeEntry
{
    /// <summary>
    ///     Monotonically increasing sequence number
    /// </summary>
    public required long SequenceNumber { get; init; }

    /// <summary>
    ///     Type of operation performed
    /// </summary>
    public required ChangeOperation Operation { get; init; }

    /// <summary>
    ///     File path relative to sync path
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    ///     File hash (empty for delete operations)
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    ///     Timestamp when the change was detected
    /// </summary>
    public required DateTime Timestamp { get; init; }

    /// <summary>
    ///     File size in bytes (0 for directories and deletes)
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    ///     Last modified timestamp from filesystem
    /// </summary>
    public DateTime LastModified { get; init; }
}

/// <summary>
///     Type of file change operation
/// </summary>
public enum ChangeOperation
{
    Add,
    Modify,
    Delete
}

/// <summary>
///     Persistent changelog containing all file changes
/// </summary>
public record FileChangeLog
{
    /// <summary>
    ///     Current sequence number (last assigned)
    /// </summary>
    public long CurrentSequence { get; init; }

    /// <summary>
    ///     List of all changes in chronological order
    /// </summary>
    public required List<FileChangeEntry> Changes { get; init; }

    /// <summary>
    ///     Timestamp of last changelog update
    /// </summary>
    public DateTime LastUpdated { get; init; }
}

/// <summary>
///     Snapshot of filesystem state at a point in time
/// </summary>
public record FileSystemSnapshot
{
    /// <summary>
    ///     Dictionary mapping file paths to their metadata
    ///     Key: relative file path (Windows style)
    ///     Value: File metadata
    /// </summary>
    public required Dictionary<string, FileMetadata> Files { get; init; }

    /// <summary>
    ///     Sequence number when this snapshot was taken
    /// </summary>
    public long SequenceNumber { get; init; }

    /// <summary>
    ///     Timestamp when snapshot was created
    /// </summary>
    public DateTime Timestamp { get; init; }
}

/// <summary>
///     Metadata for a single file
/// </summary>
public record FileMetadata
{
    /// <summary>
    ///     File content hash
    /// </summary>
    public required string Hash { get; init; }

    /// <summary>
    ///     File size in bytes
    /// </summary>
    public long Size { get; init; }

    /// <summary>
    ///     Last modified timestamp from filesystem
    /// </summary>
    public DateTime LastModified { get; init; }

    /// <summary>
    ///     Whether this is a directory
    /// </summary>
    public bool IsDirectory { get; init; }
}
