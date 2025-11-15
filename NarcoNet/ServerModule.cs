using System.Net.Http;

using NarcoNet.Models;
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
#if NARCONET_DEBUG_LOGGING
            NarcoPlugin.Logger.LogDebug($"GetJsonTask: Requesting {RequestHandler.Host}{jsonPath}");
#endif
            string jsonResponse = await client.GetStringAsync($"{RequestHandler.Host}{jsonPath}");
            return jsonResponse;
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"Request failed for {jsonPath}");
            NarcoPlugin.Logger.LogError($"  Exception Type: {e.GetType().FullName}");
            NarcoPlugin.Logger.LogError($"  Message: {(string.IsNullOrEmpty(e.Message) ? "<empty>" : e.Message)}");
            NarcoPlugin.Logger.LogError($"  URL: {RequestHandler.Host}{jsonPath}");

            if (e is HttpRequestException)
            {
#if NARCONET_DEBUG_LOGGING
                NarcoPlugin.Logger.LogDebug($"  HTTP Request Exception Details: {e}");
#endif
            }

            if (e.InnerException != null)
            {
                NarcoPlugin.Logger.LogError($"  Inner Exception: {e.InnerException.GetType().FullName}");
                NarcoPlugin.Logger.LogError($"  Inner Message: {(string.IsNullOrEmpty(e.InnerException.Message) ? "<empty>" : e.InnerException.Message)}");
            }

            NarcoPlugin.Logger.LogError($"  Stack Trace: {e.StackTrace}");
            throw;
        }
    }

    internal async Task DownloadFile(string file, string path, SemaphoreSlim limiter, CancellationToken cancellationToken, string? localPath = null)
    {
        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Use localPath if provided, otherwise use file (for backward compatibility)
        string downloadPath = Path.Combine(path, localPath ?? file);

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
#if NARCONET_DEBUG_LOGGING
                        NarcoPlugin.Logger.LogDebug($"  Exception: {e.GetType().FullName}: {(string.IsNullOrEmpty(e.Message) ? "<empty>" : e.Message)}");
#endif
                        break;
                    case > 5:
                        NarcoPlugin.Logger.LogError($"Download failed for '{file}' after {retryCount} attempts");
                        NarcoPlugin.Logger.LogError($"  Exception Type: {e.GetType().FullName}");
                        NarcoPlugin.Logger.LogError($"  Message: {(string.IsNullOrEmpty(e.Message) ? "<empty>" : e.Message)}");

                        if (e.InnerException != null)
                        {
                            NarcoPlugin.Logger.LogError($"  Inner Exception: {e.InnerException.GetType().FullName}");
                            NarcoPlugin.Logger.LogError($"  Inner Message: {(string.IsNullOrEmpty(e.InnerException.Message) ? "<empty>" : e.InnerException.Message)}");
                        }

                        NarcoPlugin.Logger.LogError($"  Stack Trace: {e.StackTrace}");
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

    internal async Task<Dictionary<string, Dictionary<string, ModFile>>> GetRemoteHashes(List<SyncPath> paths)
    {
        if (paths.Count == 0)
        {
            NarcoPlugin.Logger.LogWarning("No sync paths provided");
            return new Dictionary<string, Dictionary<string, ModFile>>();
        }

        try
        {
            string queryString = string.Join("&path=", paths.Select(path => Uri.EscapeDataString(path.Path.Replace(@"\", "/"))));
#if NARCONET_DEBUG_LOGGING
            NarcoPlugin.Logger.LogDebug($"GetRemoteHashes: Requesting hashes for {paths.Count} paths");
            NarcoPlugin.Logger.LogDebug($"  Query: /narconet/hashes?path={queryString}");
#endif
            string json = await GetJsonTask($"/narconet/hashes?path={queryString}");

#if NARCONET_DEBUG_LOGGING
            NarcoPlugin.Logger.LogDebug($"GetRemoteHashes: Received JSON response ({json.Length} bytes)");
#endif

            var rawData =
                Json.Deserialize<Dictionary<string, Dictionary<string, ModFile>>>(json);

            return rawData.ToDictionary(
                item => item.Key,
                item => new Dictionary<string, ModFile>(item.Value, StringComparer.OrdinalIgnoreCase),
                StringComparer.OrdinalIgnoreCase);
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"Failed to get remote hashes");
            NarcoPlugin.Logger.LogError($"  Exception Type: {e.GetType().FullName}");
            NarcoPlugin.Logger.LogError($"  Message: {(string.IsNullOrEmpty(e.Message) ? "<empty>" : e.Message)}");

            if (e.InnerException != null)
            {
                NarcoPlugin.Logger.LogError($"  Inner Exception: {e.InnerException.GetType().FullName}");
                NarcoPlugin.Logger.LogError($"  Inner Message: {(string.IsNullOrEmpty(e.InnerException.Message) ? "<empty>" : e.InnerException.Message)}");
            }

            NarcoPlugin.Logger.LogError($"  Stack Trace: {e.StackTrace}");
            throw;
        }
    }

    /// <summary>
    ///     Get the current sequence number from the server
    /// </summary>
    internal async Task<long> GetCurrentSequence()
    {
        try
        {
            string json = await GetJsonTask("/narconet/sequence");
            var response = Json.Deserialize<SequenceResponse>(json);
            return response.CurrentSequence;
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"Failed to get current sequence: {e.Message}");
            throw;
        }
    }

    /// <summary>
    ///     Get incremental file changes since a specific sequence number
    /// </summary>
    internal async Task<ChangesResponse> GetChangesSince(long sinceSequence)
    {
        try
        {
            string json = await GetJsonTask($"/narconet/changes?since={sinceSequence}");
            var response = Json.Deserialize<ChangesResponse>(json);
            return response;
        }
        catch (Exception e)
        {
            NarcoPlugin.Logger.LogError($"Failed to get changes since {sinceSequence}: {e.Message}");
            throw;
        }
    }
}
