namespace NarcoNet.Utilities;

public record SyncPath(
    string Path,
    string Name = "",
    bool Enabled = true,
    bool Enforced = false,
    bool Silent = false,
    bool RestartRequired = true)
{
    public string Name { get; init; } = string.IsNullOrEmpty(Name) ? Path : Name;
}
