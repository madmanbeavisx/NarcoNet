using System.Text.Json;

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;

using NarcoNet.Server.Models;
using NarcoNet.Server.Utilities;
using NarcoNet.Utilities;

using SPTarkov.DI.Annotations;
using SPTarkov.Server.Core.DI;
using SPTarkov.Server.Core.Models.Common;
using SPTarkov.Server.Core.Servers.Http;

namespace NarcoNet.Server.Services;

/// <summary>
///     HTTP listener for NarcoNet mod synchronization endpoints
/// </summary>
[Injectable(InjectionType = InjectionType.Singleton, TypePriority = OnLoadOrder.PreSptModLoader+1)]
public class NarcoNetHttpListener(
    ILogger<NarcoNetHttpListener> logger,
    SyncService syncService,
    MimeTypeHelper mimeTypeHelper,
    ChangeLogService changeLogService)
    : IHttpListener
{
    // Fallback data for older client versions
    private static readonly Dictionary<string, object> FallbackSyncPaths = new()
    {
        { "undefined", new[] { "BepInEx\\plugins\\NarcoNet.dll", "NarcoNet.Updater.exe" } },
        {
            "0.8.0", new[]
            {
                new
                {
                    enabled = true,
                    enforced = true,
                    path = "BepInEx\\plugins\\NarcoNet.dll",
                    restartRequired = true,
                    silent = false
                },
                new
                {
                    enabled = true,
                    enforced = true,
                    path = "NarcoNet.Updater.exe",
                    restartRequired = false,
                    silent = false
                }
            }
        }
    };

    private static readonly Dictionary<string, object> FallbackHashes = new()
    {
        {
            "undefined", new Dictionary<string, object>
            {
                { "BepInEx\\plugins\\NarcoNet.dll", new { crc = 999999999 } },
                { "NarcoNet.Updater.exe", new { crc = 999999999 } }
            }
        },
        {
            "0.8.0", new Dictionary<string, object>
            {
                {
                    "BepInEx\\plugins\\NarcoNet.dll", new Dictionary<string, object>
                    {
                        { "BepInEx\\plugins\\NarcoNet.dll", new { crc = 999999999, nosync = false } }
                    }
                },
                {
                    "NarcoNet.Updater.exe", new Dictionary<string, object>
                    {
                        { "NarcoNet.Updater.exe", new { crc = 999999999, nosync = false } }
                    }
                }
            }
        }
    };

    private NarcoNetConfig? _config;
    private bool _isInitialized;
    private string? _modVersion;

    public bool CanHandle(MongoId sessionId, HttpContext context)
    {
        return context.Request.Path.StartsWithSegments("/narconet");
    }

    public async Task Handle(MongoId sessionId, HttpContext context)
    {
        if (!_isInitialized || _config == null)
        {
            var errorMsg = $"NarcoNet: Not initialized (_isInitialized={_isInitialized}, _config={(_config == null ? "null" : "not null")})";
            logger.LogWarning(errorMsg);
            context.Response.StatusCode = 500;

            byte[] errorBytes = System.Text.Encoding.UTF8.GetBytes(errorMsg);
            context.Response.ContentType = "text/plain";
            await context.Response.Body.WriteAsync(errorBytes);
            await context.Response.StartAsync();
            await context.Response.CompleteAsync();
            return;
        }

        try
        {
            string path = context.Request.Path.Value ?? "";
#if NARCONET_DEBUG_LOGGING
            logger.LogDebug($"NarcoNet request received: {context.Request.Method} {path}");
#endif

            if (path == "/narconet/version")
            {
                await HandleGetVersion(context);
            }
            else if (path == "/narconet/syncpaths")
            {
                await HandleGetSyncPaths(context);
            }
            else if (path == "/narconet/exclusions")
            {
                await HandleGetExclusions(context);
            }
            else if (path == "/narconet/hashes")
            {
                await HandleGetHashes(context);
            }
            else if (path == "/narconet/changes")
            {
                await HandleGetChanges(context);
            }
            else if (path == "/narconet/sequence")
            {
                await HandleGetCurrentSequence(context);
            }
            else if (path == "/narconet/recheck")
            {
                await HandleRecheck(context);
            }
            else if (path.StartsWith("/narconet/fetch/"))
            {
                await HandleFetchModFile(context);
            }
            else
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync("NarcoNet: Unknown route");
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling request [{Method} {Path}]", context.Request.Method, context.Request.Path);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"NarcoNet: Error handling [{context.Request.Method} {context.Request.Path}]:\n{ex}");
        }
    }

    public void Initialize(NarcoNetConfig config, string modVersion)
    {
        logger.LogDebug("HttpListener.Initialize() called with version {Version}", modVersion);
        _config = config;
        _modVersion = modVersion;
        _isInitialized = true;
        logger.LogDebug("HttpListener initialized successfully (_isInitialized={IsInit})", _isInitialized);
    }

    private async Task HandleGetVersion(HttpContext context)
    {
        string json = JsonSerializer.Serialize(_modVersion);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(jsonBytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private async Task HandleGetSyncPaths(HttpContext context)
    {
        string? version = context.Request.Headers["narconet-version"].FirstOrDefault();

        string json;
        if (!string.IsNullOrEmpty(version) && FallbackSyncPaths.ContainsKey(version))
        {
            json = JsonSerializer.Serialize(FallbackSyncPaths[version]);
        }
        else
        {
            var syncPaths = _config!.SyncPaths.Select(sp => new
            {
                sp.Name,
                Path = PathHelper.ToWindowsPath(sp.Path),
                sp.Enabled,
                sp.Enforced,
                sp.Silent,
                sp.RestartRequired
            }).ToList();
            json = JsonSerializer.Serialize(syncPaths);
        }

        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(jsonBytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private async Task HandleGetExclusions(HttpContext context)
    {
        string json = JsonSerializer.Serialize(_config!.Exclusions);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(jsonBytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private async Task HandleGetHashes(HttpContext context)
    {
        string? version = context.Request.Headers["narconet-version"].FirstOrDefault();
#if NARCONET_DEBUG_LOGGING
        logger.LogDebug($"HandleGetHashes: Client version: {version ?? "unknown"}");
#endif

        string json;
        if (!string.IsNullOrEmpty(version) && FallbackHashes.ContainsKey(version))
        {
            json = JsonSerializer.Serialize(FallbackHashes[version]);
#if NARCONET_DEBUG_LOGGING
            logger.LogDebug($"Using fallback hashes for version {version}");
#endif
        }
        else
        {
            StringValues pathsParam = context.Request.Query["path"];

            // Only hash enabled or enforced sync paths
            List<SyncPath> pathsToHash;
            if (pathsParam.Count > 0)
            {
                // Client requested specific paths - only hash those (if enabled or enforced)
                List<string?> requestedPaths = pathsParam.ToList();
                logger.LogDebug("Client requested {Count} specific paths", requestedPaths.Count);
#if NARCONET_DEBUG_LOGGING
                foreach (var reqPath in requestedPaths)
                {
                    logger.LogDebug($"  - {reqPath}");
                }
#endif
                pathsToHash = _config!.SyncPaths
                    .Where(sp => (sp.Enabled || sp.Enforced) && requestedPaths.Contains(sp.Path))
                    .ToList();
                logger.LogDebug("Hashing {Count} enabled/enforced paths", pathsToHash.Count);
            }
            else
            {
                // No specific paths requested - hash all enabled/enforced paths
                logger.LogDebug("Hashing all enabled/enforced sync paths");
                pathsToHash = _config!.SyncPaths
                    .Where(sp => sp.Enabled || sp.Enforced)
                    .ToList();
            }

            Dictionary<string, Dictionary<string, ModFile>> hashResults = await syncService.HashModFilesAsync(pathsToHash, _config, context.RequestAborted);

            // Log total file counts per path
            foreach (var pathHash in hashResults)
            {
                logger.LogDebug("Path '{Path}' has {Count} files",
                    pathHash.Key, pathHash.Value.Count);
            }

            json = JsonSerializer.Serialize(hashResults);
        }

        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);
        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(jsonBytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private async Task HandleGetChanges(HttpContext context)
    {
        // Get sequence number from query parameter
        if (!context.Request.Query.TryGetValue("since", out StringValues sinceParam) || 
            !long.TryParse(sinceParam.FirstOrDefault(), out long sinceSequence))
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync("NarcoNet: Missing or invalid 'since' query parameter");
            return;
        }

        logger.LogDebug("Client requested changes since sequence {Sequence}", sinceSequence);

        // Get changes from changelog service
        List<FileChangeEntry> changes = changeLogService.GetChangesSince(sinceSequence);
        long currentSequence = await changeLogService.GetCurrentSequenceAsync(context.RequestAborted);

        var response = new
        {
            CurrentSequence = currentSequence,
            Changes = changes.Select(c => new
            {
                c.SequenceNumber,
                Operation = c.Operation.ToString(),
                c.FilePath,
                c.Hash,
                c.Timestamp,
                c.FileSize,
                c.LastModified
            }).ToList()
        };

        string json = JsonSerializer.Serialize(response);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(jsonBytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();

        logger.LogDebug("Returned {Count} changes to client (current sequence: {Current})", 
            changes.Count, currentSequence);
    }

    private async Task HandleGetCurrentSequence(HttpContext context)
    {
        long currentSequence = await changeLogService.GetCurrentSequenceAsync(context.RequestAborted);

        var response = new
        {
            CurrentSequence = currentSequence
        };

        string json = JsonSerializer.Serialize(response);
        byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

        context.Response.ContentType = "application/json";
        context.Response.StatusCode = 200;
        await context.Response.Body.WriteAsync(jsonBytes);
        await context.Response.StartAsync();
        await context.Response.CompleteAsync();
    }

    private async Task HandleFetchModFile(HttpContext context)
    {
        string pathSegment = context.Request.Path.Value?.Replace("/narconet/fetch/", "") ?? "";
        string filePath = Uri.UnescapeDataString(pathSegment);
        string clientIp = context.Connection.RemoteIpAddress?.ToString() ?? "unknown";

        try
        {
            string sanitizedPath = syncService.SanitizeDownloadPath(filePath, _config!.SyncPaths);

            if (!File.Exists(sanitizedPath))
            {
                context.Response.StatusCode = 404;
                await context.Response.WriteAsync($"Attempt to access non-existent path {filePath}");
                return;
            }

            FileInfo fileInfo = new(sanitizedPath);
            string extension = Path.GetExtension(filePath);
            string mimeType = mimeTypeHelper.GetMimeType(extension) ?? "application/octet-stream";

            // Log the download
            logger.LogInformation("Serving file '{FilePath}' ({FileSize} bytes) to {ClientIp}",
                filePath, fileInfo.Length, clientIp);

            context.Response.Headers["Accept-Ranges"] = "bytes";
            context.Response.ContentType = mimeType;
            context.Response.ContentLength = fileInfo.Length;
            context.Response.StatusCode = 200;

            await using FileStream fileStream = new(sanitizedPath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true);
            await fileStream.CopyToAsync(context.Response.Body, context.RequestAborted);
        }
        catch (UnauthorizedAccessException ex)
        {
            context.Response.StatusCode = 400;
            await context.Response.WriteAsync(ex.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error reading file '{FilePath}'", filePath);
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"NarcoNet: Error reading '{filePath}'\n{ex}");
        }
    }

    private async Task HandleRecheck(HttpContext context)
    {
        try
        {
            // Get sequence before recheck
            long beforeSequence = await changeLogService.GetCurrentSequenceAsync(context.RequestAborted);

            // Perform an on-demand recheck (does not use the startup-specific method)
            List<FileChangeEntry> changes = await syncService.RecheckAsync(_config!.SyncPaths, _config, context.RequestAborted);

            // Get sequence after recheck
            long afterSequence = await changeLogService.GetCurrentSequenceAsync(context.RequestAborted);

            var response = new
            {
                BeforeSequence = beforeSequence,
                AfterSequence = afterSequence,
                Changes = changes.Select(c => new
                {
                    c.SequenceNumber,
                    Operation = c.Operation.ToString(),
                    c.FilePath,
                    c.Hash,
                    c.Timestamp,
                    c.FileSize,
                    c.LastModified
                }).ToList()
            };

            string json = JsonSerializer.Serialize(response);
            byte[] jsonBytes = System.Text.Encoding.UTF8.GetBytes(json);

            context.Response.ContentType = "application/json";
            context.Response.StatusCode = 200;
            await context.Response.Body.WriteAsync(jsonBytes);
            await context.Response.StartAsync();
            await context.Response.CompleteAsync();
        }
        catch (OperationCanceledException)
        {
            context.Response.StatusCode = 499; // Client Closed Request (non-standard)
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling recheck request");
            context.Response.StatusCode = 500;
            await context.Response.WriteAsync($"NarcoNet: Error handling recheck: {ex}");
        }
    }
}