using NarcoNet.Utilities;

using Newtonsoft.Json.Linq;

using SPT.Common.Utils;

namespace NarcoNet;

public class Migrator(string baseDir)
{
    private string NarcoNetDir => Path.Combine(baseDir, "NarcoNet_Data");
    private string VersionPath => Path.Combine(NarcoNetDir, "Version.txt");
    private string PreviousSyncPath => Path.Combine(NarcoNetDir, "PreviousSync.json");
    private string NarcoNetPath => Path.Combine(baseDir, ".narconet");

    private List<string> CleanupFiles =>
    [
        NarcoNetPath, Path.Combine(baseDir, @"BepInEx\patchers\MadManBeavis-NarcoNet-Patcher.dll")
    ];

    private Version DetectPreviousVersion()
    {
        try
        {
            if (Directory.Exists(NarcoNetDir) && File.Exists(VersionPath))
            {
                return Version.Parse(File.ReadAllText(VersionPath));
            }

            if (File.Exists(NarcoNetPath))
            {
                JObject persist = JObject.Parse(File.ReadAllText(NarcoNetPath));
                if (persist.ContainsKey("version") && persist["version"] != null)
                {
                    return (persist["version"] ?? "0.0.0").Value<int>() switch
                    {
                        7 => Version.Parse("0.7.0"),
                        _ => Version.Parse("0.0.0")
                    };
                }
            }
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogWarning("Unable to read previous version data, cleaning up...");
            NarcoPlugin.Logger.LogWarning(e);
        }

        return Version.Parse("0.0.0");
    }

    private void Cleanup(Version pluginVersion)
    {
        if (Directory.Exists(NarcoNetDir))
        {
            Directory.Delete(NarcoNetDir, true);
        }

        foreach (string file in CleanupFiles.Where(File.Exists))
        {
            File.Delete(file);
        }

        Directory.CreateDirectory(NarcoNetDir);
        File.WriteAllText(VersionPath, pluginVersion.ToString());
    }

    public void TryMigrate(Version pluginVersion, List<SyncPath> syncPaths)
    {
        Version oldVersion = DetectPreviousVersion();

        if (oldVersion == Version.Parse("0.0.0"))
        {
            Cleanup(pluginVersion);
            return;
        }

        if (oldVersion < Version.Parse("0.8.0"))
        {
            JObject persist = JObject.Parse(File.ReadAllText(NarcoNetPath));

            if (!persist.ContainsKey("previousSync") || persist["previousSync"] == null)
            {
                Cleanup(pluginVersion);
                return;
            }

            JObject oldPreviousSync = (JObject)persist["previousSync"]!;
            JObject newPreviousSync = new();

            foreach (SyncPath syncPath in syncPaths)
            {
                newPreviousSync.Add(syncPath.Path, new JObject());
            }

            foreach (JProperty property in oldPreviousSync.Properties())
            {
                SyncPath? syncPath = syncPaths.Find(s => property.Name.StartsWith($"{s.Path}\\"));
                if (syncPath == null)
                {
                    NarcoPlugin.Logger.LogDebug(
                        $"Sync path '{property.Name}' no longer exists, discarding...");
                    continue;
                }

                JObject modFile = (JObject)property.Value;
                if (!modFile.ContainsKey("crc"))
                {
                    NarcoPlugin.Logger.LogWarning($"Sync record for '{property.Name}' is malformed");
                    continue;
                }

                (newPreviousSync.Property(syncPath.Path)!.Value as JObject)!.Add(property.Name,
                    new JObject { ["crc"] = modFile["crc"]!.Value<uint>() });
            }

            if (!Directory.Exists(NarcoNetDir))
            {
                Directory.CreateDirectory(NarcoNetDir);
            }

            File.WriteAllText(PreviousSyncPath, Json.Serialize(newPreviousSync));
            File.WriteAllText(VersionPath, pluginVersion.ToString());

            foreach (string file in CleanupFiles.Where(File.Exists))
            {
                File.Delete(file);
            }
        }

        if (oldVersion < Version.Parse("0.9.0"))
        {
            JObject previousSync = JObject.Parse(File.ReadAllText(PreviousSyncPath));

            foreach (JProperty property in previousSync.Properties())
            {
                foreach (JProperty file in (property.Value as JObject)!.Properties())
                {
                    JObject fileObject = (file.Value as JObject)!;

                    fileObject.Property("nosync")?.Remove();
                    fileObject.Property("crc")?.Remove();
                    fileObject.Add("hash", "");
                    fileObject.Add("directory", false);
                }
            }

            File.WriteAllText(PreviousSyncPath, Json.Serialize(previousSync));
            File.WriteAllText(VersionPath, pluginVersion.ToString());
        }
        else if (oldVersion.Minor == pluginVersion.Minor && oldVersion != pluginVersion)
        {
            NarcoPlugin.Logger.LogWarning(
                "Previous version mismatch detected - may cause issues");
        }
    }
}
