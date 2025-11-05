using System.Text.Json;
using System.Text.Json.Nodes;
using NarcoNet.Server.Models;
using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.Utils;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NarcoNet.Server.Services;

/// <summary>
/// Service for loading and validating NarcoNet configuration
/// Supports both YAML (.yaml/.yml) and JSON (.json/.jsonc) formats
/// </summary>
[Injectable]
public class ConfigService
{
    private const string DefaultYamlConfig = """
# NarcoNet Configuration
# Sync paths define what files/folders are synchronized from server to client
# Use YAML for easier editing, or rename to config.json for JSON format

syncPaths:
  # Simple path format (uses defaults: enabled=true, enforced=false, restartRequired=true)
  - BepInEx/plugins
  - BepInEx/patchers
  - BepInEx/config

  # Advanced format with full control
  - name: "(Optional) Server mods"
    path: user/mods
    enabled: false           # Set to true to sync this path
    enforced: false          # If true, re-syncs files deleted/modified by client
    silent: false            # If true, updates without showing UI
    restartRequired: false   # If true, prompts client to restart after update

# Exclusions prevent specific files/patterns from being synced
# Supports glob patterns like **/*.ext or specific paths
exclusions:
  # SPT Core (never sync)
  - BepInEx/plugins/spt
  - BepInEx/patchers/spt-prepatch.dll

  # Common mod data folders
  - BepInEx/plugins/DanW-SPTQuestingBots/log
  - user/mods/SPT-Realism/ProfileBackups
  - user/mods/fika-server/types
  - user/mods/fika-server/cache
  - BepInEx/plugins/Fika.Headless.dll
  - user/mods/zzDrakiaXYZ-LiveFleaPrices/config
  - BepInEx/plugins/kmyuhkyuk-EFTApi/cache
  - user/mods/ExpandedTaskText/src/**/cache.json
  - user/mods/leaves-loot_fuckery/output
  - user/mods/zz_guiltyman-addmissingquestweaponrequirements/log.log
  - user/mods/zz_guiltyman-addmissingquestweaponrequirements/user/logs
  - user/mods/acidphantasm-progressivebotsystem/logs

  # NarcoNet internal (synced via built-in paths)
  - BepInEx/patchers/NarcoNet-Patcher.dll

  # Admin/Dev exclusions (use .nosync marker files)
  - "**/*.nosync"
  - "**/*.nosync.txt"

  # Development files
  - user/mods/**/.git
  - user/mods/**/node_modules
  - user/mods/**/*.js
  - user/mods/**/*.js.map

  # Windows metadata
  - "**/*:Zone.Identifier"
""";

    private readonly JsonUtil _jsonUtil;

    public ConfigService(JsonUtil jsonUtil)
    {
        _jsonUtil = jsonUtil;
    }

    /// <summary>
    /// Load the NarcoNet configuration from file (supports YAML and JSON)
    /// </summary>
    public async Task<NarcoNetConfig> LoadConfigAsync(string modPath)
    {
        // Check for config files in order of preference: YAML first, then JSON
        string? configPath = FindConfigFile(modPath);

        if (configPath == null)
        {
            // Create default YAML config
            configPath = Path.Combine(modPath, "config.yaml");
            await File.WriteAllTextAsync(configPath, DefaultYamlConfig);
        }

        var (rawSyncPaths, exclusions) = await LoadConfigFileAsync(configPath);

        ValidateConfig(rawSyncPaths, exclusions, configPath);

        // Build the final config with built-in sync paths
        var syncPaths = new List<SyncPath>
        {
            new()
            {
                Enabled = true,
                Enforced = true,
                Silent = true,
                RestartRequired = false,
                Path = "NarcoNet.Updater.exe",
                Name = "(Builtin) NarcoNet Updater"
            },
            new()
            {
                Enabled = true,
                Enforced = true,
                Silent = true,
                RestartRequired = true,
                Path = "BepInEx/plugins/MadManBeavis-NarcoNet",
                Name = "(Builtin) NarcoNet Plugin"
            }
        };

        syncPaths.AddRange(rawSyncPaths);

        // Sort by path length descending
        syncPaths = syncPaths.OrderByDescending(sp => sp.Path.Length).ToList();

        return new NarcoNetConfig
        {
            SyncPaths = syncPaths,
            Exclusions = exclusions
        };
    }

    /// <summary>
    /// Find config file (checks YAML first, then JSON)
    /// </summary>
    private static string? FindConfigFile(string modPath)
    {
        // Check YAML variants
        var yamlPath = Path.Combine(modPath, "config.yaml");
        if (File.Exists(yamlPath)) return yamlPath;

        var ymlPath = Path.Combine(modPath, "config.yml");
        if (File.Exists(ymlPath)) return ymlPath;

        // Check JSON variants
        var jsoncPath = Path.Combine(modPath, "config.jsonc");
        if (File.Exists(jsoncPath)) return jsoncPath;

        var jsonPath = Path.Combine(modPath, "config.json");
        if (File.Exists(jsonPath)) return jsonPath;

        return null;
    }

    /// <summary>
    /// Load config file based on extension
    /// </summary>
    private async Task<(List<SyncPath> syncPaths, List<string> exclusions)> LoadConfigFileAsync(string configPath)
    {
        var extension = Path.GetExtension(configPath).ToLowerInvariant();
        var configText = await File.ReadAllTextAsync(configPath);

        return extension switch
        {
            ".yaml" or ".yml" => LoadYamlConfig(configText),
            ".json" or ".jsonc" => LoadJsonConfig(configText),
            _ => throw new NotSupportedException($"Unsupported config format: {extension}")
        };
    }

    /// <summary>
    /// Load YAML configuration
    /// </summary>
    private (List<SyncPath>, List<string>) LoadYamlConfig(string yamlContent)
    {
        var deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        var config = deserializer.Deserialize<YamlConfig>(yamlContent);

        var syncPaths = new List<SyncPath>();
        if (config.SyncPaths != null)
        {
            foreach (var item in config.SyncPaths)
            {
                if (item is string pathStr)
                {
                    // Simple string format
                    syncPaths.Add(new SyncPath
                    {
                        Path = pathStr,
                        Name = pathStr,
                        Enabled = true,
                        Enforced = false,
                        Silent = false,
                        RestartRequired = true
                    });
                }
                else if (item is Dictionary<object, object> dict)
                {
                    // Object format
                    var path = dict.TryGetValue("path", out var p) ? p?.ToString()
                        : throw new InvalidOperationException("Missing 'path' in syncPath object");

                    syncPaths.Add(new SyncPath
                    {
                        Path = path!,
                        Name = dict.TryGetValue("name", out var n) ? n?.ToString() ?? path! : path!,
                        Enabled = dict.TryGetValue("enabled", out var e) && e is bool enabled ? enabled : true,
                        Enforced = dict.TryGetValue("enforced", out var enf) && enf is bool enforced ? enforced : false,
                        Silent = dict.TryGetValue("silent", out var s) && s is bool silent ? silent : false,
                        RestartRequired = dict.TryGetValue("restartRequired", out var r) && r is bool restart ? restart : true
                    });
                }
            }
        }

        var exclusions = config.Exclusions ?? [];

        return (syncPaths, exclusions);
    }

    /// <summary>
    /// Load JSON configuration
    /// </summary>
    private (List<SyncPath>, List<string>) LoadJsonConfig(string jsonContent)
    {
        var jsonNode = JsonNode.Parse(jsonContent);
        var syncPathsNode = jsonNode?["syncPaths"]?.AsArray();
        var exclusionsNode = jsonNode?["exclusions"]?.AsArray();

        var rawSyncPaths = new List<SyncPath>();
        if (syncPathsNode != null)
        {
            foreach (var node in syncPathsNode)
            {
                if (node is JsonValue && node.GetValueKind() == JsonValueKind.String)
                {
                    // String path
                    var pathStr = node.GetValue<string>();
                    rawSyncPaths.Add(new SyncPath
                    {
                        Path = pathStr,
                        Name = pathStr,
                        Enabled = true,
                        Enforced = false,
                        Silent = false,
                        RestartRequired = true
                    });
                }
                else if (node is JsonObject obj)
                {
                    // Object path
                    var path = obj["path"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing 'path' in syncPath object");
                    rawSyncPaths.Add(new SyncPath
                    {
                        Path = path,
                        Name = obj["name"]?.GetValue<string>() ?? path,
                        Enabled = obj["enabled"]?.GetValue<bool>() ?? true,
                        Enforced = obj["enforced"]?.GetValue<bool>() ?? false,
                        Silent = obj["silent"]?.GetValue<bool>() ?? false,
                        RestartRequired = obj["restartRequired"]?.GetValue<bool>() ?? true
                    });
                }
            }
        }

        var exclusions = new List<string>();
        if (exclusionsNode != null)
        {
            foreach (var node in exclusionsNode)
            {
                if (node is JsonValue)
                {
                    exclusions.Add(node.GetValue<string>());
                }
            }
        }

        return (rawSyncPaths, exclusions);
    }

    /// <summary>
    /// Validate the configuration
    /// </summary>
    private void ValidateConfig(List<SyncPath> syncPaths, List<string> exclusions, string configPath)
    {
        if (syncPaths == null)
            throw new InvalidOperationException($"NarcoNet: '{configPath}' 'syncPaths' is not an array. Please verify your config is correct and try again.");

        if (exclusions == null)
            throw new InvalidOperationException($"NarcoNet: '{configPath}' 'exclusions' is not an array. Please verify your config is correct and try again.");

        var uniquePaths = new HashSet<string>();
        foreach (var syncPath in syncPaths)
        {
            var path = syncPath.Path;
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException($"NarcoNet: '{configPath}' 'syncPaths' is missing 'path'. Please verify your config is correct and try again.");

            if (Path.IsPathRooted(path))
                throw new InvalidOperationException($"NarcoNet: SyncPaths must be relative to SPT server root. Invalid path '{path}'");

            var fullPath = Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), path));
            var relativePath = Path.GetRelativePath(Directory.GetCurrentDirectory(), fullPath);
            if (relativePath.StartsWith(".."))
                throw new InvalidOperationException($"NarcoNet: SyncPaths must be within SPT server root. Invalid path '{path}'");

            if (!uniquePaths.Add(path))
                throw new InvalidOperationException($"NarcoNet: SyncPaths must be unique. Duplicate path '{path}'");

            if (exclusions.Contains(path))
                throw new InvalidOperationException($"NarcoNet: '{path}' has been added as a sync path and is also in the 'exclusions' array.");
        }
    }

    /// <summary>
    /// YAML config structure for deserialization
    /// </summary>
    private class YamlConfig
    {
        public List<object>? SyncPaths { get; set; }
        public List<string>? Exclusions { get; set; }
    }
}
