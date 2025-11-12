using System.Text.Json;

using Microsoft.Extensions.Logging;

using NarcoNet.Server.Models;
using NarcoNet.Server.Utilities;
using NarcoNet.Utilities;

using SPTarkov.DI.Annotations;

namespace NarcoNet.Server.Services;

/// <summary>
///     Service for managing file change logs and snapshots
/// </summary>
[Injectable]
public class ChangeLogService
{
    private readonly ILogger<ChangeLogService> _logger;
    private readonly string _changeLogPath;
    private readonly string _snapshotPath;
    private FileChangeLog? _changeLog;
    private FileSystemSnapshot? _lastSnapshot;

    public ChangeLogService(ILogger<ChangeLogService> logger)
    {
        _logger = logger;
        string narcoNetDataDir = Path.Combine(Directory.GetCurrentDirectory(), "user", "narconet_data");
        Directory.CreateDirectory(narcoNetDataDir);
        
        _changeLogPath = Path.Combine(narcoNetDataDir, "changelog.json");
        _snapshotPath = Path.Combine(narcoNetDataDir, "snapshot.json");
    }

    /// <summary>
    ///     Load the changelog from disk, or create a new one if it doesn't exist
    /// </summary>
    public async Task<FileChangeLog> LoadChangeLogAsync(CancellationToken cancellationToken = default)
    {
        if (_changeLog != null)
        {
            return _changeLog;
        }

        if (!File.Exists(_changeLogPath))
        {
            _logger.LogInformation("No existing changelog found, creating new one");
            _changeLog = new FileChangeLog
            {
                CurrentSequence = 0,
                Changes = [],
                LastUpdated = DateTime.UtcNow
            };
            return _changeLog;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_changeLogPath, cancellationToken);
            _changeLog = JsonSerializer.Deserialize<FileChangeLog>(json) ?? new FileChangeLog
            {
                CurrentSequence = 0,
                Changes = [],
                LastUpdated = DateTime.UtcNow
            };
            
            _logger.LogInformation("Loaded changelog with {Count} entries, current sequence: {Sequence}", 
                _changeLog.Changes.Count, _changeLog.CurrentSequence);
            
            return _changeLog;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load changelog, creating new one");
            _changeLog = new FileChangeLog
            {
                CurrentSequence = 0,
                Changes = [],
                LastUpdated = DateTime.UtcNow
            };
            return _changeLog;
        }
    }

    /// <summary>
    ///     Load the last filesystem snapshot from disk
    /// </summary>
    public async Task<FileSystemSnapshot?> LoadSnapshotAsync(CancellationToken cancellationToken = default)
    {
        if (_lastSnapshot != null)
        {
            return _lastSnapshot;
        }

        if (!File.Exists(_snapshotPath))
        {
            _logger.LogInformation("No existing snapshot found");
            return null;
        }

        try
        {
            string json = await File.ReadAllTextAsync(_snapshotPath, cancellationToken);
            _lastSnapshot = JsonSerializer.Deserialize<FileSystemSnapshot>(json);
            
            if (_lastSnapshot != null)
            {
                _logger.LogInformation("Loaded snapshot with {Count} files at sequence {Sequence}", 
                    _lastSnapshot.Files.Count, _lastSnapshot.SequenceNumber);
            }
            
            return _lastSnapshot;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load snapshot");
            return null;
        }
    }

    /// <summary>
    ///     Save the changelog to disk
    /// </summary>
    public async Task SaveChangeLogAsync(FileChangeLog changeLog, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(changeLog, options);
            await File.WriteAllTextAsync(_changeLogPath, json, cancellationToken);
            _changeLog = changeLog;
            _logger.LogDebug("Saved changelog with {Count} entries", changeLog.Changes.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save changelog");
            throw;
        }
    }

    /// <summary>
    ///     Save the filesystem snapshot to disk
    /// </summary>
    public async Task SaveSnapshotAsync(FileSystemSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        try
        {
            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(snapshot, options);
            await File.WriteAllTextAsync(_snapshotPath, json, cancellationToken);
            _lastSnapshot = snapshot;
            _logger.LogDebug("Saved snapshot with {Count} files at sequence {Sequence}", 
                snapshot.Files.Count, snapshot.SequenceNumber);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save snapshot");
            throw;
        }
    }

    /// <summary>
    ///     Get changes since a specific sequence number
    /// </summary>
    public List<FileChangeEntry> GetChangesSince(long sequenceNumber)
    {
        if (_changeLog == null)
        {
            return [];
        }

        return _changeLog.Changes
            .Where(c => c.SequenceNumber > sequenceNumber)
            .OrderBy(c => c.SequenceNumber)
            .ToList();
    }

    /// <summary>
    ///     Append new changes to the changelog
    /// </summary>
    public async Task AppendChangesAsync(
        List<FileChangeEntry> newChanges, 
        CancellationToken cancellationToken = default)
    {
        FileChangeLog changeLog = await LoadChangeLogAsync(cancellationToken);
        
        var updatedChanges = new List<FileChangeEntry>(changeLog.Changes);
        updatedChanges.AddRange(newChanges);
        
        long maxSequence = newChanges.Count > 0 
            ? newChanges.Max(c => c.SequenceNumber) 
            : changeLog.CurrentSequence;

        FileChangeLog updated = changeLog with
        {
            Changes = updatedChanges,
            CurrentSequence = maxSequence,
            LastUpdated = DateTime.UtcNow
        };

        await SaveChangeLogAsync(updated, cancellationToken);
    }

    /// <summary>
    ///     Prune old changelog entries (keep last N days)
    /// </summary>
    public async Task PruneOldEntriesAsync(int keepDays = 30, CancellationToken cancellationToken = default)
    {
        FileChangeLog changeLog = await LoadChangeLogAsync(cancellationToken);
        
        DateTime cutoff = DateTime.UtcNow.AddDays(-keepDays);
        List<FileChangeEntry> recentChanges = changeLog.Changes
            .Where(c => c.Timestamp >= cutoff)
            .ToList();

        if (recentChanges.Count < changeLog.Changes.Count)
        {
            _logger.LogInformation("Pruned {Count} old changelog entries", 
                changeLog.Changes.Count - recentChanges.Count);

            FileChangeLog updated = changeLog with
            {
                Changes = recentChanges,
                LastUpdated = DateTime.UtcNow
            };

            await SaveChangeLogAsync(updated, cancellationToken);
        }
    }

    /// <summary>
    ///     Get the current sequence number
    /// </summary>
    public async Task<long> GetCurrentSequenceAsync(CancellationToken cancellationToken = default)
    {
        FileChangeLog changeLog = await LoadChangeLogAsync(cancellationToken);
        return changeLog.CurrentSequence;
    }
}
