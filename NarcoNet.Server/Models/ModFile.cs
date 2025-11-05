namespace NarcoNet.Server.Models;

/// <summary>
///     Represents a mod file with its hash and metadata
/// </summary>
public record ModFile
{
    public required string Hash { get; init; }
    public required bool IsDirectory { get; init; }
}
