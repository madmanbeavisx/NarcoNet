using NarcoNet.Utilities;

using SPT.Common.Http;
using SPT.Common.Utils;

namespace NarcoNet;

internal class ServerModule(Version pluginVersion)
{
  private async Task<string> GetJsonTask(string jsonPath)
  {
    try
    {
      using HttpClient client = new();
      client.DefaultRequestHeaders.Add("narconet-version", $"NarcoNet/{pluginVersion.ToString()}");
      client.Timeout = TimeSpan.FromMinutes(3);
      string jsonResponse = await client.GetStringAsync($"{RequestHandler.Host}{jsonPath}");
      return jsonResponse;
    }
    catch (Exception e)
    {
      NarcoPlugin.Logger.LogError($"💥 Communication with headquarters failed for {jsonPath}: {e.Message}");
      throw;
    }
  }

  internal async Task DownloadFile(string file, string path, SemaphoreSlim limiter, CancellationToken cancellationToken)
  {
    if (cancellationToken.IsCancellationRequested) return;
    string downloadPath = Path.Combine(path, file);

    VFS.CreateDirectory(path.GetDirectory());

    int retryCount = 0;

    await limiter.WaitAsync(cancellationToken);
    while (!cancellationToken.IsCancellationRequested)
      try
      {
        using HttpClient client = new();
        if (retryCount > 0) client.Timeout = TimeSpan.FromMinutes(3 * retryCount);

        await using Stream responseStream =
          await client.GetStreamAsync($"{RequestHandler.Host}/narconet/fetch/{file}", cancellationToken);
        await using FileStream filestream = new(downloadPath, FileMode.Create);

        await responseStream.CopyToAsync(filestream, cancellationToken);

        limiter.Release();

        return;
      }
      catch (Exception e)
      {
        if (e is TaskCanceledException && cancellationToken.IsCancellationRequested) throw;
        retryCount++;
        await Task.Delay(1000 * retryCount, cancellationToken);
        switch (retryCount)
        {
          case >= 1 and <= 5:
            int retryTime = 2 * retryCount;
            NarcoPlugin.Logger.LogError(
              $"📦 Package '{file}' got intercepted! Sending another courier in {retryTime} seconds. Attempt #{retryCount}/5 ...");
            break;
          case > 5:
            NarcoPlugin.Logger.LogError(
              $"💀 Lost package '{file}' after {retryCount} attempts. The route is too hot, we need to lay low: {e}"
            );
            throw;
        }
      }
  }

  internal async Task<string> GetNarcoNetVersion()
  {
    return Json.Deserialize<string>(await GetJsonTask("/narconet/version"));
  }

  internal async Task<List<SyncPath>> GetLocalSyncPaths()
  {
    return Json.Deserialize<List<SyncPath>>(await GetJsonTask("/narconet/syncpaths"));
  }

  internal async Task<List<string>> GetListExclusions()
  {
    return Json.Deserialize<List<string>>(await GetJsonTask("/narconet/exclusions"));
  }

  internal async Task<Dictionary<string, Dictionary<string, string>>> GetRemoteHashes(List<SyncPath> paths)
  {
    if (paths == null || paths.Count == 0)
    {
      NarcoPlugin.Logger.LogWarning("⚠️ No smuggling routes provided to check inventory!");
      return new Dictionary<string, Dictionary<string, string>>();
    }

    try
    {
      string json = await GetJsonTask($"/narconet/hashes?path={string.Join("&path=",
        paths.Select(path => Uri.EscapeDataString(path.Path.Replace(@"\", "/").TrimEnd('/'))))}");

      Dictionary<string, Dictionary<string, string>>? rawData =
        Json.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

      return rawData.ToDictionary(
        item => item.Key,
        item => new Dictionary<string, string>(item.Value, StringComparer.OrdinalIgnoreCase),
        StringComparer.OrdinalIgnoreCase);
    }
    catch (Exception e)
    {
      NarcoPlugin.Logger.LogError($"💥 Failed to get the boss's inventory: {e.Message}");
      throw;
    }
  }
}
