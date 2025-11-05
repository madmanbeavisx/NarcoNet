namespace NarcoNet.Utilities;

/// <summary>
///     Solution-wide constants for NarcoNet
/// </summary>
public static class NarcoNetConstants
{
    // Branding
    public const string ProductName = "NarcoNet";
    public const string FullProductName = "MadManBeavis - NarcoNet";
    public const string Author = "MadManBeavis";
    public const string Version = "1.0.0";

    // Plugin Information
    public const string PluginGuid = "com.madmanbeavis.narconet";
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
    public const string LogPrefix = "[MadManBeavis-NarcoNet]";
    public const string UpdaterLogPrefix = "[MadManBeavis-NarcoNet Updater]";

    // UI Messages
    public static class Messages
    {
        public const string UpdateRequired = "New shipment arrived! Update required, compadre.";
        public const string UpdateOptional = "Fresh merchandise available - update when you're ready, amigo.";
        public const string UpdateInProgress = "The courier is making deliveries...";
        public const string UpdateComplete = "Delivery complete! The goods are stocked and ready.";
        public const string UpdateFailed = "The shipment got intercepted - delivery failed!";
        public const string UpdateCancelled = "Delivery cancelled - keeping the old inventory.";

        public const string WaitingForTarkovClose = "Waiting for the client to close shop...";
        public const string CopyingFiles = "Unloading the merchandise from the truck...";
        public const string DeletingFiles = "Clearing out the old product...";

        public const string ErrorTarkovNotFound =
            "Can't find the operation base (EscapeFromTarkov.exe)! Make sure you're in the SPT territory, jefe!";

        public const string ErrorDataDirectoryNotFound =
            "The stash house (NarcoNet_Data) is missing! Run the BepInEx plugin first to set up the safe house!";

        public const string ErrorInvalidPid = "Invalid client ID provided - the courier is confused!";
    }

    // URLs (for future updates/downloads)
    public static class Urls
    {
        public const string Repository = "https://github.com/MadManBeavis/NarcoNet";
        public const string Issues = "https://github.com/MadManBeavis/NarcoNet/issues";
        public const string Documentation = "https://github.com/MadManBeavis/NarcoNet/wiki";
    }
}
