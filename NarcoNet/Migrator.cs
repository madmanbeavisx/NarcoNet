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
    NarcoNetPath, Path.Combine(baseDir, @"BepInEx\patchers\UrGrannyOnFents-NarcoNet-Patcher.dll")
  ];

  private Version DetectPreviousVersion()
  {
    try
    {
      if (Directory.Exists(NarcoNetDir) && File.Exists(VersionPath))
        return Version.Parse(File.ReadAllText(VersionPath));

      if (File.Exists(NarcoNetPath))
      {
        JObject persist = JObject.Parse(File.ReadAllText(NarcoNetPath));
        if (persist.ContainsKey("version") && persist["version"] != null)
          return (persist["version"] ?? "0.0.0").Value<int>() switch
          {
            7 => Version.Parse("0.7.0"),
            _ => Version.Parse("0.0.0")
          };
      }
    }
    catch (Exception e)
    {
      NarcoPlugin.Logger.LogWarning("⚠️ Can't read the old ledger. Burning the evidence and starting fresh...");
      NarcoPlugin.Logger.LogWarning(e);
    }

    return Version.Parse("0.0.0");
  }

  private void Cleanup(Version pluginVersion)
  {
    if (Directory.Exists(NarcoNetDir))
      Directory.Delete(NarcoNetDir, true);

    foreach (string file in CleanupFiles.Where(File.Exists))
      File.Delete(file);

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
        newPreviousSync.Add(syncPath.Path, new JObject());

      foreach (JProperty property in oldPreviousSync.Properties())
      {
        SyncPath? syncPath = syncPaths.Find(s => property.Name.StartsWith($"{s.Path}\\"));
        if (syncPath == null)
        {
          NarcoPlugin.Logger.LogWarning(
            $"⚠️ Old route '{property.Name}' is no longer in the smuggling network. Discarding...");
          continue;
        }

        JObject modFile = (JObject)property.Value;
        if (!modFile.ContainsKey("crc"))
        {
          NarcoPlugin.Logger.LogWarning($"⚠️ Shipment record for '{property.Name}' is damaged. Can't verify the merchandise.");
          continue;
        }

        (newPreviousSync.Property(syncPath.Path)!.Value as JObject)!.Add(property.Name,
          new JObject { ["crc"] = modFile["crc"]!.Value<uint>() });
      }

      if (!Directory.Exists(NarcoNetDir))
        Directory.CreateDirectory(NarcoNetDir);

      File.WriteAllText(PreviousSyncPath, Json.Serialize(newPreviousSync));
      File.WriteAllText(VersionPath, pluginVersion.ToString());

      foreach (string file in CleanupFiles.Where(File.Exists))
        File.Delete(file);
    }

    if (oldVersion < Version.Parse("0.9.0"))
    {
      JObject previousSync = JObject.Parse(File.ReadAllText(PreviousSyncPath));

      foreach (JProperty property in previousSync.Properties())
      foreach (JProperty file in (property.Value as JObject)!.Properties())
      {
        JObject fileObject = (file.Value as JObject)!;

        fileObject.Property("nosync")?.Remove();
        fileObject.Property("crc")?.Remove();
        fileObject.Add("hash", "");
        fileObject.Add("directory", false);
      }

      File.WriteAllText(PreviousSyncPath, Json.Serialize(previousSync));
      File.WriteAllText(VersionPath, pluginVersion.ToString());
    }
    else if (oldVersion.Minor == pluginVersion.Minor && oldVersion != pluginVersion)
    {
      NarcoPlugin.Logger.LogWarning(
        "⚠️ The last operation used different procedures. Could cause complications, but we're pushing forward...");
    }
  }
}
