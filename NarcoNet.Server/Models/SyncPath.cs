namespace NarcoNet.Server.Models;

/// <summary>
///     Represents a path to be synchronized between server and client
/// </summary>
public record SyncPath
{
    public string? Name { get; init; }
    public required string Path { get; init; }
    public bool Enabled { get; init; } = true;
    public bool Enforced { get; init; } = false;
    public bool Silent { get; init; } = false;
    public bool RestartRequired { get; init; } = true;
}
