using JetBrains.Annotations;

using Microsoft.Extensions.Logging;

using NarcoNet.Server.Models;
using NarcoNet.Server.Services;
using NarcoNet.Utilities;

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.External;
using SPTarkov.Server.Core.Models.Spt.Mod;

using Range = SemanticVersioning.Range;
using Version = SemanticVersioning.Version;
#pragma warning disable CS8764 // Nullability of return type doesn't match overridden member (possibly because of nullability attributes).

#pragma warning disable IDE0160
namespace NarcoNet.Server;
#pragma warning restore IDE0160

/// <summary>
///     Metadata for the NarcoNet server mod
/// </summary>
[UsedImplicitly]
public record ModMetadata : AbstractModMetadata
{
    public override string ModGuid { get; init; } = "com.madmanbeavis.narconet.server";
    public override string Name { get; init; } = "NarcoNet";
    public override string Author { get; init; } = "MadManBeavis";
    public override List<string>? Contributors { get; init; }
    public override Version Version { get; init; } = new(NarcoNetVersion.Version);
    public override Range SptVersion { get; init; } = new("~4.0.0");
    public override List<string>? Incompatibilities { get; init; }
    public override Dictionary<string, Range>? ModDependencies { get; init; }
    public override string? Url { get; init; }
    public override bool? IsBundleMod { get; init; }
    public override string? License { get; init; } = "MIT";
}

/// <summary>
///     Main NarcoNet server mod class
/// </summary>
[Injectable(TypePriority = OnLoadOrder.PreSptModLoader + 2), UsedImplicitly]
public class NarcoNetServer(
    ILogger<NarcoNetServer> logger,
    ConfigService configService,
    NarcoNetHttpListener httpListener)
    : IPreSptLoadModAsync
{
    private static bool _loadFailed;

    public async Task PreSptLoadAsync()
    {
        try
        {
            // Get mod path
            string? modPath = GetModPath();
            if (string.IsNullOrEmpty(modPath))
            {
                _loadFailed = true;
                logger.LogError("Failed to find mod directory");
                return;
            }

            // Load configuration
            NarcoNetConfig config = await configService.LoadConfigAsync(modPath);

            // Check for files that will be synced to clients
            string updaterPath = Path.Combine(Directory.GetCurrentDirectory(), "NarcoNet.Updater.exe");
            string clientPluginDir = Path.Combine(Directory.GetCurrentDirectory(), @"..\", "BepInEx", "plugins", "MadManBeavis-NarcoNet");

            logger.LogDebug(!File.Exists(updaterPath)
                ? "NarcoNet.Updater.exe not found in SPT root - client updates disabled"
                : "NarcoNet.Updater.exe found - client updates enabled");

            logger.LogDebug(!Directory.Exists(clientPluginDir)
                ? "BepInEx/plugins/MadManBeavis-NarcoNet directory not found - client plugin sync disabled"
                : "BepInEx/plugins/MadManBeavis-NarcoNet directory found - client plugin sync enabled");

            // Initialize HTTP listener (only if load succeeded)
            if (!_loadFailed)
            {
                httpListener.Initialize(config, NarcoNetVersion.Version);
                logger.LogInformation("NarcoNet server mod loaded successfully");
            }
        }
        catch (Exception ex)
        {
            _loadFailed = true;
            logger.LogError(ex, "Failed to load NarcoNet server mod");
            throw;
        }
    }

    private string? GetModPath()
    {
        try
        {
            // Try to find the mod directory
            string modsPath = Path.Combine(Directory.GetCurrentDirectory(), "user", "mods");
            if (!Directory.Exists(modsPath))
            {
                return null;
            }

            string[] modDirectories = Directory.GetDirectories(modsPath, "*narconet*", SearchOption.TopDirectoryOnly);
            return modDirectories.Length > 0 ? modDirectories[0] : null;
        }
        catch
        {
            return null;
        }
    }
}
