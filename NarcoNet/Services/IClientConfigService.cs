using BepInEx.Configuration;
using NarcoNet.Utilities;

namespace NarcoNet.Services;

/// <summary>
///     Service interface for managing client configuration
/// </summary>
public interface IClientConfigService
{
    /// <summary>
    ///     Gets the configuration entry for deleting removed files
    /// </summary>
    ConfigEntry<bool> DeleteRemovedFiles { get; }

    /// <summary>
    ///     Gets the sync path toggle configurations
    /// </summary>
    Dictionary<string, ConfigEntry<bool>> SyncPathToggles { get; }

    /// <summary>
    ///     Gets the list of enabled sync paths based on configuration
    /// </summary>
    List<SyncPath> EnabledSyncPaths { get; }

    /// <summary>
    ///     Initializes configuration from server sync paths
    /// </summary>
    void Initialize(ConfigFile config, List<SyncPath> syncPaths);

    /// <summary>
    ///     Checks if the client is running in headless mode
    /// </summary>
    bool IsHeadless();

    /// <summary>
    ///     Gets the default headless exclusions
    /// </summary>
    List<string> GetHeadlessDefaultExclusions();
}
