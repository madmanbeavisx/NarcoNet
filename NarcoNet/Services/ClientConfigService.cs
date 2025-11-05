using BepInEx.Bootstrap;
using BepInEx.Configuration;
using NarcoNet.Utilities;

namespace NarcoNet.Services;

/// <summary>
///     Manages client configuration settings
/// </summary>
public class ClientConfigService : IClientConfigService
{
    private static readonly List<string> HeadlessDefaultExclusions =
    [
        "../BepInEx/plugins/AmandsGraphics.dll",
        "../BepInEx/plugins/AmandsSense.dll",
        "../BepInEx/plugins/Sense",
        "../BepInEx/plugins/MoreCheckmarks",
        "../BepInEx/plugins/kmyuhkyuk-EFTApi",
        "../BepInEx/plugins/DynamicMaps",
        "../BepInEx/plugins/LootValue",
        "../BepInEx/plugins/CactusPie.RamCleanerInterval.dll",
        "../BepInEx/plugins/TYR_DeClutterer.dll"
    ];

    private ConfigEntry<bool>? _deleteRemovedFiles;
    private Dictionary<string, ConfigEntry<bool>>? _syncPathToggles;
    private List<SyncPath>? _syncPaths;

    /// <inheritdoc/>
    public ConfigEntry<bool> DeleteRemovedFiles =>
        _deleteRemovedFiles ?? throw new InvalidOperationException("Configuration not initialized");

    /// <inheritdoc/>
    public Dictionary<string, ConfigEntry<bool>> SyncPathToggles =>
        _syncPathToggles ?? throw new InvalidOperationException("Configuration not initialized");

    /// <inheritdoc/>
    public List<SyncPath> EnabledSyncPaths
    {
        get
        {
            if (_syncPaths == null)
            {
                throw new InvalidOperationException("Configuration not initialized");
            }

            if (_syncPathToggles == null)
            {
                return [];
            }

            return _syncPaths
                .Where(syncPath => _syncPathToggles[syncPath.Path].Value || syncPath.Enforced)
                .ToList();
        }
    }

    /// <inheritdoc/>
    public void Initialize(ConfigFile config, List<SyncPath> syncPaths)
    {
        _syncPaths = syncPaths;

        _deleteRemovedFiles = config.Bind(
            "General",
            "Delete Removed Files",
            true,
            "Should the mod delete files that have been removed from the server?"
        );

        _syncPathToggles = syncPaths
            .Select(syncPath => new KeyValuePair<string, ConfigEntry<bool>>(
                syncPath.Path,
                config.Bind(
                    "Synced Paths",
                    syncPath.Name.Replace("\\", "/"),
                    syncPath.Enabled,
                    new ConfigDescription(
                        $"Should the mod attempt to sync files from {syncPath.Path.Replace("\\", "/")}",
                        null,
                        new ConfigurationManagerAttributes { ReadOnly = syncPath.Enforced }
                    )
                )
            ))
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
    }

    /// <inheritdoc/>
    public bool IsHeadless()
    {
        return Chainloader.PluginInfos.ContainsKey("com.fika.headless");
    }

    /// <inheritdoc/>
    public List<string> GetHeadlessDefaultExclusions()
    {
        return HeadlessDefaultExclusions;
    }
}
