using NarcoNet.Services;
using NarcoNet.Utilities;

namespace NarcoNet.Tests.Services;

/// <summary>
///     Unit tests for ClientInitializationService
/// </summary>
public class ClientInitializationServiceTests
{
    [Fact]
    public void ValidateSyncPaths_ReturnsNull_WhenPathsAreValid()
    {
        // Arrange
        var service = new ClientInitializationService();
        var syncPaths = new List<SyncPath>
        {
            new("BepInEx/plugins", "Test Path", true, false, false, false)
        };
        var serverRoot = Directory.GetCurrentDirectory();

        // Act
        var result = service.ValidateSyncPaths(syncPaths, serverRoot);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void ValidateSyncPaths_ReturnsError_WhenPathIsRooted()
    {
        // Arrange
        var service = new ClientInitializationService();
        var syncPaths = new List<SyncPath>
        {
            new("C:\\absolute\\path", "Test Path", true, false, false, false)
        };
        var serverRoot = Directory.GetCurrentDirectory();

        // Act
        var result = service.ValidateSyncPaths(syncPaths, serverRoot);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("relative to SPT server root", result);
    }

    [Fact]
    public void ValidateSyncPaths_AllowsPathsWithParentDirectory()
    {
        // Arrange
        var service = new ClientInitializationService();
        var syncPaths = new List<SyncPath>
        {
            new("..\\BepInEx\\plugins\\test", "Test Path", true, false, false, false)
        };
        var serverRoot = Directory.GetCurrentDirectory();

        // Act
        var result = service.ValidateSyncPaths(syncPaths, serverRoot);

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public void BuildRemoteModFiles_IncludesAllFilesForEnforcedPaths()
    {
        // Arrange
        var service = new ClientInitializationService();
        var syncPaths = new List<SyncPath>
        {
            new("path1", "Test Path", true, false, false, false) // Enforced = true
        };
        var remoteHashes = new Dictionary<string, Dictionary<string, string>>
        {
            ["path1"] = new Dictionary<string, string>
            {
                ["file1.dll"] = "hash1",
                ["file2.dll"] = "hash2"
            }
        };
        var localExclusions = new List<string>();

        // Act
        var result = service.BuildRemoteModFiles(syncPaths, remoteHashes, localExclusions);

        // Assert
        Assert.Single(result);
        Assert.Equal(2, result["path1"].Count);
    }

    [Fact]
    public void BuildRemoteModFiles_HandlesEmptyRemoteHashes()
    {
        // Arrange
        var service = new ClientInitializationService();
        var syncPaths = new List<SyncPath>
        {
            new("path1", "Test Path", true, false, false, false)
        };
        var remoteHashes = new Dictionary<string, Dictionary<string, string>>();
        var localExclusions = new List<string>();

        // Act
        var result = service.BuildRemoteModFiles(syncPaths, remoteHashes, localExclusions);

        // Assert
        Assert.Single(result);
        Assert.True(result.ContainsKey("path1"));
        Assert.Empty(result["path1"]);
    }

    [Fact]
    public void BuildRemoteModFiles_IdentifiesDirectories()
    {
        // Arrange
        var service = new ClientInitializationService();
        var syncPaths = new List<SyncPath>
        {
            new("path1", "Test Path", true, false, false, false)
        };
        var remoteHashes = new Dictionary<string, Dictionary<string, string>>
        {
            ["path1"] = new Dictionary<string, string>
            {
                ["file.dll"] = "hash1",
                ["dir\\"] = "dirHash"
            }
        };
        var localExclusions = new List<string>();

        // Act
        var result = service.BuildRemoteModFiles(syncPaths, remoteHashes, localExclusions);

        // Assert
        Assert.Equal(2, result["path1"].Count);
        Assert.False(result["path1"]["file.dll"].Directory);
        Assert.True(result["path1"]["dir\\"].Directory);
    }
}
