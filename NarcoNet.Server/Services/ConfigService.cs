using System.Text.Json;
using System.Text.Json.Nodes;

using JetBrains.Annotations;

using NarcoNet.Server.Models;

using SPTarkov.DI.Annotations;

using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace NarcoNet.Server.Services;

/// <summary>
/// Service for loading and validating NarcoNet configuration
/// Supports both YAML (.yaml/.yml) and JSON (.json/.jsonc) formats
/// </summary>
[Injectable]
[UsedImplicitly]
public class ConfigService
{
    private const string DefaultYamlConfig = """
                                             # ╔══════════════════════════════════════════════════════════════════════╗
                                             # ║                    NarcoNet Configuration                            ║
                                             # ║          Sync mods & plugins from SPT server to clients              ║
                                             # ╚══════════════════════════════════════════════════════════════════════╝
                                             #
                                             # SPT 4.0 Folder Structure:
                                             #   C:\SPT\                  <- Game root (EscapeFromTarkov.exe)
                                             #   ├── BepInEx\             <- Client-side mods (synced to clients)
                                             #   │   ├── plugins\         <- Client plugins
                                             #   │   ├── patchers\        <- Client patchers
                                             #   │   └── config\          <- Client configs
                                             #   └── SPT\                 <- Server directory ** WE RUN FROM HERE **
                                             #       ├── SPT.Server.exe
                                             #       └── user\mods\       <- Server-side mods
                                             #
                                             # Path Reference Rules:
                                             #   - Use "../" to reference the game folder from server folder
                                             #   - Use "user/" to reference server folder contents (no prefix)
                                             #
                                             # Examples:
                                             #   "../BepInEx/plugins"        -> C:\SPT\BepInEx\plugins
                                             #   "user/mods"                 -> C:\SPT\SPT\user\mods
                                             #   "../BepInEx/config"         -> C:\SPT\BepInEx\config

                                             syncPaths:
                                               # Client-side mods (game folder - one level up)
                                               - ../BepInEx/plugins
                                               - ../BepInEx/patchers
                                               - ../BepInEx/config

                                               # Server-side mods (in server folder)
                                               - name: "(Optional) Server mods"
                                                 path: user/mods
                                                 enabled: false           # Set to true to sync this path
                                                 enforced: false          # If true, re-syncs files deleted/modified by client
                                                 silent: false            # If true, updates without showing UI
                                                 restartRequired: false   # If true, prompts client to restart after update

                                             # Exclusions prevent specific files/patterns from being synced
                                             #
                                             # Glob Pattern Examples:
                                             #   **/*.log           - All .log files in any subdirectory
                                             #   **/cache           - All 'cache' folders anywhere
                                             #   **/node_modules    - All 'node_modules' folders (recursive)
                                             #   user/mods/*/logs   - 'logs' folder in any immediate subdirectory of user/mods
                                             #   **/*.{js,map}      - All .js and .map files anywhere
                                             #   mod-name/**        - Everything inside 'mod-name' folder
                                             #
                                             # Special Characters:
                                             #   *    - Matches any characters except /
                                             #   **   - Matches any characters including / (recursive)
                                             #   ?    - Matches exactly one character
                                             #   [abc] - Matches any character in brackets
                                             #   {a,b} - Matches any pattern in braces
                                             #
                                             # Remember: Use "../" prefix for game folder files, no prefix for server folder files
                                             exclusions:
                                               # SPT Core (never sync) - in game folder
                                               - ../BepInEx/plugins/spt
                                               - ../BepInEx/patchers/spt-prepatch.dll

                                               # Common client mod data folders - in game folder
                                               - ../BepInEx/plugins/DanW-SPTQuestingBots/log
                                               - ../BepInEx/plugins/Fika.Headless.dll
                                               - ../BepInEx/plugins/kmyuhkyuk-EFTApi/cache

                                               # Common server mod data folders - in server folder
                                               - user/mods/SPT-Realism/ProfileBackups
                                               - user/mods/fika-server/types
                                               - user/mods/fika-server/cache
                                               - user/mods/zzDrakiaXYZ-LiveFleaPrices/config
                                               - user/mods/ExpandedTaskText/src/**/cache.json
                                               - user/mods/leaves-loot_fuckery/output
                                               - user/mods/zz_guiltyman-addmissingquestweaponrequirements/log.log
                                               - user/mods/zz_guiltyman-addmissingquestweaponrequirements/user/logs
                                               - user/mods/acidphantasm-progressivebotsystem/logs

                                               # NarcoNet internal (synced via built-in paths)
                                               - ../BepInEx/patchers/MadManBeavis-NarcoNet-Patcher.dll

                                               # Admin/Dev exclusions (use .nosync marker files)
                                               - "**/*.nosync"              # Any file ending in .nosync
                                               - "**/*.nosync.txt"          # Any .nosync.txt file

                                               # Development files (recursive patterns)
                                               - user/mods/**/.git          # All .git folders in server mods
                                               - user/mods/**/node_modules  # All node_modules folders
                                               - user/mods/**/*.js          # All JavaScript files (recursive)
                                               - user/mods/**/*.js.map      # All source maps (recursive)
                                               - user/mods/**/*.ts          # All TypeScript source files
                                               - "**/src/**/*.ts"           # All TS files in any 'src' folder

                                               # Log files (pattern matching)
                                               - "**/*.log"                 # All .log files anywhere
                                               - "**/logs/**"               # All files in any 'logs' folder
                                               - "**/log/**"                # All files in any 'log' folder

                                               # Cache and temporary files
                                               - "**/cache/**"              # All files in any 'cache' folder
                                               - "**/temp/**"               # All files in any 'temp' folder
                                               - "**/*.tmp"                 # All temporary files
                                               - "**/*.cache"               # All cache files

                                               # Windows metadata
                                               - "**/*:Zone.Identifier"     # Windows download zone markers

                                             # ═══════════════════════════════════════════════════════════════════════
                                             # GLOB PATTERN QUICK REFERENCE
                                             # ═══════════════════════════════════════════════════════════════════════
                                             #
                                             # Pattern Matching Wildcards:
                                             #   *        Matches any characters EXCEPT / (single directory level)
                                             #   **       Matches any characters INCLUDING / (multiple directory levels)
                                             #   ?        Matches exactly ONE character
                                             #   [abc]    Matches any character in brackets: a, b, or c
                                             #   [a-z]    Matches any character in range: a through z
                                             #   {a,b}    Matches either pattern: a OR b
                                             #
                                             # Real-World Examples:
                                             #
                                             #   *.dll                    # Only .dll files in root (not subdirectories)
                                             #   **/*.dll                 # All .dll files recursively in any subdirectory
                                             #   user/mods/*/config.json  # config.json in immediate subdirectories only
                                             #   user/mods/**/config.json # config.json in any nested subdirectory
                                             #   **/{logs,cache}          # Any folder named 'logs' OR 'cache' anywhere
                                             #   **/*.{log,txt}           # All .log OR .txt files anywhere
                                             #   mod-??.dll               # Matches: mod-01.dll, mod-ab.dll (2 chars)
                                             #   config-[0-9].json        # Matches: config-0.json through config-9.json
                                             #   !important.dll           # Negation - INCLUDE this file (override exclusion)
                                             #
                                             # Common Use Cases:
                                             #
                                             #   Exclude all logs:             - "**/*.log"
                                             #   Exclude specific mod:         - "user/mods/problem-mod/**"
                                             #   Exclude all node_modules:     - "**/node_modules/**"
                                             #   Exclude TypeScript sources:   - "**/*.ts"
                                             #   Exclude development folders:  - "**/{.git,node_modules,src}/**"
                                             #   Exclude backup files:         - "**/*.{bak,backup,old}"
                                             #   Exclude cache anywhere:       - "**/cache/**"
                                             #
                                             # Pro Tips:
                                             #   - Use quotes around patterns with special chars: "**/*.{js,ts}"
                                             #   - Remember "../" prefix for game folder paths
                                             #   - Test patterns carefully - they match anywhere in the path
                                             #   - More specific patterns = better performance
                                             #   - Order doesn't matter - all patterns are checked
                                             # ═══════════════════════════════════════════════════════════════════════
                                             """;


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

        (List<SyncPath> rawSyncPaths, List<string> exclusions) = await LoadConfigFileAsync(configPath);

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
                Path = "../BepInEx/plugins/MadManBeavis-NarcoNet",
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
        string yamlPath = Path.Combine(modPath, "config.yaml");
        if (File.Exists(yamlPath)) return yamlPath;

        string ymlPath = Path.Combine(modPath, "config.yml");
        if (File.Exists(ymlPath)) return ymlPath;

        // Check JSON variants
        string jsoncPath = Path.Combine(modPath, "config.jsonc");
        if (File.Exists(jsoncPath)) return jsoncPath;

        string jsonPath = Path.Combine(modPath, "config.json");
        if (File.Exists(jsonPath)) return jsonPath;

        return null;
    }

    /// <summary>
    /// Load config file based on extension
    /// </summary>
    private async Task<(List<SyncPath> syncPaths, List<string> exclusions)> LoadConfigFileAsync(string configPath)
    {
        string extension = Path.GetExtension(configPath).ToLowerInvariant();
        string configText = await File.ReadAllTextAsync(configPath);

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
        IDeserializer deserializer = new DeserializerBuilder()
            .WithNamingConvention(CamelCaseNamingConvention.Instance)
            .Build();

        YamlConfig config = deserializer.Deserialize<YamlConfig>(yamlContent);

        var syncPaths = new List<SyncPath>();
        if (config.SyncPaths != null)
        {
            foreach (object item in config.SyncPaths)
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
                    string? path = dict.TryGetValue("path", out object? p)
                        ? p.ToString()
                        : throw new InvalidOperationException("Missing 'path' in syncPath object");

                    syncPaths.Add(new SyncPath
                    {
                        Path = path!,
                        Name = dict.TryGetValue("name", out object? n) ? n.ToString() ?? path! : path!,
                        Enabled = !dict.TryGetValue("enabled", out object? e) || e is not bool enabled || enabled,
                        Enforced = dict.TryGetValue("enforced", out object? enf) && enf is bool and true,
                        Silent = dict.TryGetValue("silent", out object? s) && s is bool and true,
                        RestartRequired = !dict.TryGetValue("restartRequired", out object? r) || r is not bool restart || restart
                    });
                }
            }
        }

        List<string> exclusions = config.Exclusions ?? [];

        return (syncPaths, exclusions);
    }

    /// <summary>
    /// Load JSON configuration
    /// </summary>
    private (List<SyncPath>, List<string>) LoadJsonConfig(string jsonContent)
    {
        JsonNode? jsonNode = JsonNode.Parse(jsonContent);
        JsonArray? syncPathsNode = jsonNode?["syncPaths"]?.AsArray();
        JsonArray? exclusionsNode = jsonNode?["exclusions"]?.AsArray();

        var rawSyncPaths = new List<SyncPath>();
        if (syncPathsNode != null)
        {
            foreach (JsonNode? node in syncPathsNode)
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
                    string path = obj["path"]?.GetValue<string>() ?? throw new InvalidOperationException("Missing 'path' in syncPath object");
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
            foreach (JsonNode? node in exclusionsNode)
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

        // Get the SPT root directory (parent of server directory for SPT 4.0)
        string serverDir = Directory.GetCurrentDirectory();
        string sptRoot = Path.GetFullPath(Path.Combine(serverDir, ".."));

        foreach (string path in syncPaths.Select(syncPath => syncPath.Path))
        {
            if (string.IsNullOrEmpty(path))
                throw new InvalidOperationException($"NarcoNet: '{configPath}' 'syncPaths' is missing 'path'. Please verify your config is correct and try again.");

            if (Path.IsPathRooted(path))
                throw new InvalidOperationException($"NarcoNet: SyncPaths must be relative paths. Invalid path '{path}'");

            // Resolve the full path
            string fullPath = Path.GetFullPath(Path.Combine(serverDir, path));

            // Check that the resolved path is within SPT root (allows "../" to reach game folder)
            string relativePath = Path.GetRelativePath(sptRoot, fullPath);
            if (relativePath.StartsWith("..") || Path.IsPathRooted(relativePath))
                throw new InvalidOperationException($"NarcoNet: SyncPaths must stay within SPT installation folder. Invalid path '{path}' resolves outside SPT root.");

            if (!uniquePaths.Add(path))
                throw new InvalidOperationException($"NarcoNet: SyncPaths must be unique. Duplicate path '{path}'");

            if (exclusions.Contains(path))
                throw new InvalidOperationException($"NarcoNet: '{path}' has been added as a sync path and is also in the 'exclusions' array.");
        }
    }

    /// <summary>
    /// YAML config structure for deserialization
    /// </summary>
    [UsedImplicitly]
    private class YamlConfig
    {
        // ReSharper disable UnusedAutoPropertyAccessor.Local
        public List<object>? SyncPaths { get; set; }
        public List<string>? Exclusions { get; set; }
        // ReSharper restore UnusedAutoPropertyAccessor.Local
    }
}
