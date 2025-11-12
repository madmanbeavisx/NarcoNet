namespace NarcoNet.Models;

/// <summary>
///     Response from server containing incremental file changes
/// </summary>
public class ChangesResponse
{
    /// <summary>
    ///     Server's current sequence number
    /// </summary>
    public long CurrentSequence { get; set; }

    /// <summary>
    ///     List of changes since requested sequence
    /// </summary>
    public List<FileChangeEntry> Changes { get; set; } = [];
}

/// <summary>
///     Represents a single file change from the server
/// </summary>
public class FileChangeEntry
{
    /// <summary>
    ///     Sequence number for this change
    /// </summary>
    public long SequenceNumber { get; set; }

    /// <summary>
    ///     Type of operation (Add, Modify, Delete)
    /// </summary>
    public string Operation { get; set; } = "";

    /// <summary>
    ///     File path relative to sync path
    /// </summary>
    public string FilePath { get; set; } = "";

    /// <summary>
    ///     File hash (empty for delete operations)
    /// </summary>
    public string Hash { get; set; } = "";

    /// <summary>
    ///     Timestamp when change was detected
    /// </summary>
    public DateTime Timestamp { get; set; }

    /// <summary>
    ///     File size in bytes
    /// </summary>
    public long FileSize { get; set; }

    /// <summary>
    ///     Last modified timestamp from filesystem
    /// </summary>
    public DateTime LastModified { get; set; }
}

/// <summary>
///     Response containing current sequence number
/// </summary>
public class SequenceResponse
{
    /// <summary>
    ///     Server's current sequence number
    /// </summary>
    public long CurrentSequence { get; set; }
}

/// <summary>
///     Client-side tracking of last known sequence
/// </summary>
public class ClientSyncState
{
    /// <summary>
    ///     Last sequence number the client successfully synced
    /// </summary>
    public long LastSequence { get; set; }

    /// <summary>
    ///     Timestamp of last sync
    /// </summary>
    public DateTime LastSyncTime { get; set; }
}
