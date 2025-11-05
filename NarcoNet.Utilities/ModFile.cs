namespace NarcoNet.Utilities;

public record ModFile(string Hash, bool Directory = false)
{
    public string Hash { get; init; } = Hash;
    public bool Directory { get; init; } = Directory;
}
