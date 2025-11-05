namespace NarcoNet.Server.Models;

/// <summary>
///     Configuration for NarcoNet mod synchronization
/// </summary>
public record NarcoNetConfig
{
    public required List<SyncPath> SyncPaths { get; init; }
    public required List<string> Exclusions { get; init; }
}
