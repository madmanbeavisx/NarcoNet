namespace NarcoNet.Utilities;

public abstract record SyncPath(
  string Path,
  string Name = "",
  bool Enabled = true,
  bool Enforced = false,
  bool Silent = false,
  bool RestartRequired = true)
{
  public string Path { get; init; } = Path;
  public string Name { get; init; } = string.IsNullOrEmpty(Name) ? Path : Name;
  public bool Enabled { get; init; } = Enabled;
  public bool Enforced { get; init; } = Enforced;
  public bool Silent { get; init; } = Silent;
  public bool RestartRequired { get; init; } = RestartRequired;
}
