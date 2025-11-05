using System.Net.Http;

using NarcoNet.Utilities;

using SPT.Common.Http;
using SPT.Common.Utils;

namespace NarcoNet;

/// <summary>
///     Handles communication with the NarcoNet server
/// </summary>
public class ServerModule(Version pluginVersion)
{
    private async Task<string> GetJsonTask(string jsonPath)
    {
        try
        {
            using HttpClient client = new();
            client.DefaultRequestHeaders.Add("narconet-version", pluginVersion.ToString());
            client.Timeout = TimeSpan.FromMinutes(3);
            string jsonResponse = await client.GetStringAsync($"{RequestHandler.Host}{jsonPath}");
            return jsonResponse;
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"Request failed for {jsonPath}: {e.Message}");
            throw;
        }
    }

    internal async Task DownloadFile(string file, string path, SemaphoreSlim limiter, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        string downloadPath = Path.Combine(path, file);

        VFS.CreateDirectory(downloadPath.GetDirectory());

        var retryCount = 0;

        await limiter.WaitAsync(cancellationToken);
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                using (HttpClient client = new())
                {
                    if (retryCount > 0)
                    {
                        client.Timeout = TimeSpan.FromMinutes(3 * retryCount);
                    }

                    // URL-encode the file path to preserve ../ and other special characters
                    string encodedPath = Uri.EscapeDataString(file.Replace("\\", "/"));
                    using (Stream responseStream =
                           await client.GetStreamAsync($"{RequestHandler.Host}/narconet/fetch/{encodedPath}"))
                    using (FileStream filestream = new(downloadPath, FileMode.Create))
                    {
                        await responseStream.CopyToAsync(filestream, 81920, cancellationToken);
                    }

                    limiter.Release();

                    return;
                }
            }
            catch (Exception e)
            {
                if (e is TaskCanceledException && cancellationToken.IsCancellationRequested)
                {
                    throw;
                }

                retryCount++;
                await Task.Delay(1000 * retryCount, cancellationToken);
                switch (retryCount)
                {
                    case >= 1 and <= 5:
                        int retryTime = 2 * retryCount;
                        NarcoPlugin.Logger.LogDebug(
                            $"Download failed for '{file}', retrying in {retryTime} seconds (Attempt {retryCount}/5)");
                        break;
                    case > 5:
                        NarcoPlugin.Logger.LogError(
                            $"Download failed for '{file}' after {retryCount} attempts: {e}"
                        );
                        throw;
                }
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
        if (paths.Count == 0)
        {
            NarcoPlugin.Logger.LogWarning("No sync paths provided");
            return new Dictionary<string, Dictionary<string, string>>();
        }

        try
        {
            string json = await GetJsonTask($"/narconet/hashes?path={string.Join("&path=",
                paths.Select(path => Uri.EscapeDataString(path.Path.Replace(@"\", "/"))))}");

            var rawData =
                Json.Deserialize<Dictionary<string, Dictionary<string, string>>>(json);

            return rawData.ToDictionary(
                item => item.Key,
                item => new Dictionary<string, string>(item.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"Failed to get remote hashes: {e.Message}");
            throw;
        }
    }
}
