using BepInEx.Logging;
using NarcoNet.Services;
using NarcoNet.Utilities;

namespace NarcoNet.Tests.Services;

using SyncPathFileList = Dictionary<string, List<string>>;
using SyncPathModFiles = Dictionary<string, Dictionary<string, ModFile>>;

/// <summary>
///     Unit tests for ClientSyncService
/// </summary>
public class ClientSyncServiceTests
{
    private readonly ManualLogSource _logger;
    private readonly ServerModule _server;

    public ClientSyncServiceTests()
    {
        _logger = new ManualLogSource("TestLogger");
        _server = new ServerModule(new Version("1.0.0"));
    }

    [Fact]
    public void AnalyzeModFiles_WithDifferences_OutputsCorrectChanges()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var localFiles = new SyncPathModFiles
        {
            ["path1"] = new Dictionary<string, ModFile>
            {
                ["file1.dll"] = new ModFile("hash1", false)
            }
        };
        var remoteFiles = new SyncPathModFiles
        {
            ["path1"] = new Dictionary<string, ModFile>
            {
                ["file1.dll"] = new ModFile("hash2", false), // Updated
                ["file2.dll"] = new ModFile("hash3", false)  // Added
            }
        };
        var previousSync = new SyncPathModFiles();
        var syncPaths = new List<SyncPath>
        {
            new("path1", "Path 1", true, false, false, true)
        };

        // Act
        service.AnalyzeModFiles(
            localFiles,
            remoteFiles,
            previousSync,
            syncPaths,
            out var addedFiles,
            out var updatedFiles,
            out var removedFiles,
            out var createdDirectories
        );

        // Assert
        Assert.Single(addedFiles["path1"]); // file2.dll added
        Assert.Single(updatedFiles["path1"]); // file1.dll updated
        Assert.Empty(removedFiles["path1"]);
        Assert.Empty(createdDirectories["path1"]); // no new directories
    }

    [Fact]
    public void GetUpdateCount_ReturnsCorrectTotal()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var addedFiles = new SyncPathFileList { ["path1"] = new List<string> { "file1", "file2" } };
        var updatedFiles = new SyncPathFileList { ["path1"] = new List<string> { "file3" } };
        var removedFiles = new SyncPathFileList { ["path1"] = new List<string> { "file4" } };
        var createdDirs = new SyncPathFileList { ["path1"] = new List<string>() };
        var syncPaths = new List<SyncPath> { new("path1", "Path 1", true, false, false, true) };

        // Act
        var count = service.GetUpdateCount(addedFiles, updatedFiles, removedFiles, createdDirs, syncPaths, true);

        // Assert
        Assert.Equal(4, count); // 2 added + 1 updated + 1 removed
    }

    [Fact]
    public void IsSilentMode_ReturnsTrueForHeadless()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var addedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var updatedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var removedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var createdDirs = new SyncPathFileList { ["path1"] = new List<string>() };
        var syncPaths = new List<SyncPath> { new("path1", "Path 1", true, false, false, true) };

        // Act
        var result = service.IsSilentMode(addedFiles, updatedFiles, removedFiles, createdDirs, syncPaths, false, true);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsSilentMode_ReturnsTrueWhenAllPathsSilent()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var addedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var updatedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var removedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var createdDirs = new SyncPathFileList { ["path1"] = new List<string>() };
        var syncPaths = new List<SyncPath> { new("path1", "Path 1", true, false, true, true) }; // Silent = true

        // Act
        var result = service.IsSilentMode(addedFiles, updatedFiles, removedFiles, createdDirs, syncPaths, false, false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRestartRequired_ReturnsTrueWhenRestartNeeded()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var addedFiles = new SyncPathFileList { ["path1"] = new List<string> { "file1" } };
        var updatedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var removedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var createdDirs = new SyncPathFileList { ["path1"] = new List<string>() };
        var syncPaths = new List<SyncPath> { new("path1", "Path 1", true, false, false, true) }; // RestartRequired = true

        // Act
        var result = service.IsRestartRequired(addedFiles, updatedFiles, removedFiles, createdDirs, syncPaths, false);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsRestartRequired_ReturnsFalseWhenNoRestartNeeded()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var addedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var updatedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var removedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var createdDirs = new SyncPathFileList { ["path1"] = new List<string>() };
        var syncPaths = new List<SyncPath> { new("path1", "Path 1", true, false, false, false) }; // RestartRequired = false

        // Act
        var result = service.IsRestartRequired(addedFiles, updatedFiles, removedFiles, createdDirs, syncPaths, false);

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void GetUpdateCount_IncludesCreatedDirectories()
    {
        // Arrange
        var service = new ClientSyncService(_logger, _server);
        var addedFiles = new SyncPathFileList { ["path1"] = new List<string> { "file1" } };
        var updatedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var removedFiles = new SyncPathFileList { ["path1"] = new List<string>() };
        var createdDirs = new SyncPathFileList { ["path1"] = new List<string> { "newdir" } };
        var syncPaths = new List<SyncPath> { new("path1", "Path 1", true, false, false, true) };

        // Act
        var count = service.GetUpdateCount(addedFiles, updatedFiles, removedFiles, createdDirs, syncPaths, false);

        // Assert
        Assert.Equal(2, count); // 1 file + 1 directory
    }
}
