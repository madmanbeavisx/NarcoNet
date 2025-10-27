namespace NarcoNet.Utilities;

/// <summary>
///   Solution-wide constants for NarcoNet
/// </summary>
public static class NarcoNetConstants
{
  // Branding
  public const string ProductName = "NarcoNet";
  public const string FullProductName = "UrGrannyOnFent - NarcoNet";
  public const string Author = "UrGrannyOnFent";
  public const string Version = "1.0.0";

  // Plugin Information
  public const string PluginGuid = "com.urgrannyonfent.narconet";
  public const string PluginName = "NarcoNet";
  public const string PluginVersion = "1.0.0";

  // Directory Names
  public const string DataDirectoryName = "NarcoNet_Data";
  public const string PendingUpdatesDirectoryName = "PendingUpdates";
  public const string LogFileName = "NarcoNet.log";
  public const string RemovedFilesFileName = "RemovedFiles.json";

  // Updater
  public const string UpdaterExecutableName = "NarcoNet.Updater.exe";

  // Logging Prefixes
  public const string LogPrefix = "[UrGrannyOnFent-NarcoNet]";
  public const string UpdaterLogPrefix = "[UrGrannyOnFent-NarcoNet Updater]";

  // UI Messages
  public static class Messages
  {
    public const string UpdateRequired = "A NarcoNet update is required.";
    public const string UpdateOptional = "A NarcoNet update is available.";
    public const string UpdateInProgress = "NarcoNet is updating...";
    public const string UpdateComplete = "NarcoNet update complete!";
    public const string UpdateFailed = "NarcoNet update failed.";
    public const string UpdateCancelled = "NarcoNet update was cancelled.";

    public const string WaitingForTarkovClose = "Waiting for Tarkov to close...";
    public const string CopyingFiles = "Copying updated files...";
    public const string DeletingFiles = "Deleting removed files...";

    public const string ErrorTarkovNotFound =
      "Error: EscapeFromTarkov.exe not found. Make sure you are running from your SPT folder!";

    public const string ErrorDataDirectoryNotFound =
      "Error: NarcoNet_Data directory not found. Ensure you've run the BepInEx plugin first!";

    public const string ErrorInvalidPid = "Error: Tarkov PID argument is not a valid integer.";
  }

  // URLs (if needed for future updates/downloads)
  public static class Urls
  {
    public const string Repository = "https://github.com/UrGrannyOnFent/NarcoNet";
    public const string Issues = "https://github.com/UrGrannyOnFent/NarcoNet/issues";
    public const string Documentation = "https://github.com/UrGrannyOnFent/NarcoNet/wiki";
  }
}
