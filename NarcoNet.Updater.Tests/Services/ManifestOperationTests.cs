using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FluentAssertions;

using NarcoNet.Updater.Interfaces;
using NarcoNet.Updater.Models;
using NarcoNet.Updater.Services;
using NarcoNet.Utilities;

using Xunit;

namespace NarcoNet.Updater.Tests.Services;

public class ManifestOperationTests : IDisposable
{
    private readonly string _testDirectory;
    private readonly string _updateStagingDirectory;
    private readonly string _updateManifestPath;
    private readonly string _removedFilesManifestPath;
    private readonly ILogger _logger;

    public ManifestOperationTests()
    {
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NarcoNetTest_{Guid.NewGuid()}");
        _updateStagingDirectory = Path.Combine(_testDirectory, NarcoNetConstants.PendingUpdatesDirectoryName);
        _updateManifestPath = Path.Combine(_testDirectory, NarcoNetConstants.UpdateManifestFileName);
        _removedFilesManifestPath = Path.Combine(_testDirectory, NarcoNetConstants.RemovedFilesFileName);
        _logger = new TestLogger();

        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_updateStagingDirectory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testDirectory))
        {
            Directory.Delete(_testDirectory, true);
        }
    }

    [Fact]
    public async Task CreateDirectory_Operation_Should_Create_Directory()
    {
        // Arrange
        var manifest = new UpdateManifest
        {
            Operations =
            [
                new UpdateOperation
                {
                    Type = OperationType.CreateDirectory,
                    Destination = "TestDir/SubDir"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        string expectedDir = Path.Combine(_testDirectory, "TestDir", "SubDir");
        Directory.Exists(expectedDir).Should().BeTrue();
    }

    [Fact]
    public async Task CreateDirectory_Operation_Should_Create_Empty_Directory()
    {
        // Arrange - Simulate server having empty directories that need to be created on client
        var manifest = new UpdateManifest
        {
            Operations =
            [
                new UpdateOperation
                {
                    Type = OperationType.CreateDirectory,
                    Destination = "../BepInEx/plugins/sinai-dev-UnityExplorer/Scripts"
                },
                new UpdateOperation
                {
                    Type = OperationType.CreateDirectory,
                    Destination = "../BepInEx/plugins/DrakiaXYZ-QuestTracker/config"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        string scriptsDir = Path.Combine(_testDirectory, "..", "BepInEx", "plugins", "sinai-dev-UnityExplorer", "Scripts");
        string configDir = Path.Combine(_testDirectory, "..", "BepInEx", "plugins", "DrakiaXYZ-QuestTracker", "config");

        Directory.Exists(scriptsDir).Should().BeTrue("Scripts directory should exist");
        Directory.Exists(configDir).Should().BeTrue("config directory should exist");

        // Verify directories are actually empty
        Directory.GetFileSystemEntries(scriptsDir).Should().BeEmpty("Scripts directory should be empty");
        Directory.GetFileSystemEntries(configDir).Should().BeEmpty("config directory should be empty");
    }

    [Fact]
    public async Task CopyFile_Operation_Should_Copy_File_From_Staging()
    {
        // Arrange
        string sourceFile = Path.Combine(_updateStagingDirectory, "test.dll");
        File.WriteAllText(sourceFile, "test content");

        var manifest = new UpdateManifest
        {
            Operations =
            [
                new UpdateOperation
                {
                    Type = OperationType.CopyFile,
                    Source = "test.dll",
                    Destination = "test.dll"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        string targetFile = Path.Combine(_testDirectory, "test.dll");
        File.Exists(targetFile).Should().BeTrue();
        File.ReadAllText(targetFile).Should().Be("test content");
    }

    [Fact]
    public async Task DeleteFile_Operation_Should_Delete_File()
    {
        // Arrange
        string fileToDelete = Path.Combine(_testDirectory, "delete_me.txt");
        File.WriteAllText(fileToDelete, "delete this");

        var manifest = new UpdateManifest
        {
            Operations =
            [
                new UpdateOperation
                {
                    Type = OperationType.DeleteFile,
                    Destination = "delete_me.txt"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        File.Exists(fileToDelete).Should().BeFalse();
    }

    [Fact]
    public async Task MoveFile_Operation_Should_Move_File()
    {
        // Arrange
        string sourceFile = Path.Combine(_testDirectory, "source.txt");
        File.WriteAllText(sourceFile, "move this");

        var manifest = new UpdateManifest
        {
            Operations =
            [
                new UpdateOperation
                {
                    Type = OperationType.MoveFile,
                    Source = "source.txt",
                    Destination = "moved/destination.txt"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        File.Exists(sourceFile).Should().BeFalse("Source file should be moved");
        string targetFile = Path.Combine(_testDirectory, "moved", "destination.txt");
        File.Exists(targetFile).Should().BeTrue("Destination file should exist");
        File.ReadAllText(targetFile).Should().Be("move this");
    }

    [Fact]
    public async Task Mixed_Operations_Should_Execute_In_Order()
    {
        // Arrange - Real-world scenario: Create directories, copy files, delete old files
        string stagedFile = Path.Combine(_updateStagingDirectory, "new_plugin.dll");
        File.WriteAllText(stagedFile, "new plugin content");

        string oldFile = Path.Combine(_testDirectory, "old_plugin.dll");
        File.WriteAllText(oldFile, "old plugin content");

        var manifest = new UpdateManifest
        {
            Operations =
            [
                // First create directory
                new UpdateOperation
                {
                    Type = OperationType.CreateDirectory,
                    Destination = "BepInEx/plugins/MyPlugin"
                },
                // Then copy new file into it
                new UpdateOperation
                {
                    Type = OperationType.CopyFile,
                    Source = "new_plugin.dll",
                    Destination = "BepInEx/plugins/MyPlugin/new_plugin.dll"
                },
                // Finally delete old file
                new UpdateOperation
                {
                    Type = OperationType.DeleteFile,
                    Destination = "old_plugin.dll"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        string pluginDir = Path.Combine(_testDirectory, "BepInEx", "plugins", "MyPlugin");
        Directory.Exists(pluginDir).Should().BeTrue("Plugin directory should be created");

        string newPluginFile = Path.Combine(pluginDir, "new_plugin.dll");
        File.Exists(newPluginFile).Should().BeTrue("New plugin should be copied");
        File.ReadAllText(newPluginFile).Should().Be("new plugin content");

        File.Exists(oldFile).Should().BeFalse("Old plugin should be deleted");
    }

    [Fact]
    public async Task Manifest_Should_Be_Deleted_After_Processing()
    {
        // Arrange
        var manifest = new UpdateManifest
        {
            Operations =
            [
                new UpdateOperation
                {
                    Type = OperationType.CreateDirectory,
                    Destination = "TestDir"
                }
            ]
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        File.Exists(_updateManifestPath).Should().BeFalse("Manifest should be deleted after processing");
    }

    [Fact]
    public async Task Empty_Manifest_Should_Be_Handled_Gracefully()
    {
        // Arrange
        var manifest = new UpdateManifest
        {
            Operations = []
        };

        string manifestJson = Newtonsoft.Json.JsonConvert.SerializeObject(manifest);
        File.WriteAllText(_updateManifestPath, manifestJson);

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        Func<Task> act = async () => await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert
        await act.Should().NotThrowAsync("Empty manifest should be handled gracefully");
        File.Exists(_updateManifestPath).Should().BeFalse("Manifest should still be deleted");
    }

    [Fact]
    public async Task No_Manifest_Should_Fall_Back_To_Legacy_Mode()
    {
        // Arrange - No manifest file, but files in staging directory
        string stagedFile = Path.Combine(_updateStagingDirectory, "legacy_file.dll");
        File.WriteAllText(stagedFile, "legacy content");

        var service = new FileUpdateService(
            _logger,
            _updateStagingDirectory,
            _removedFilesManifestPath,
            _testDirectory,
            _updateManifestPath
        );

        // Act
        await service.ApplyPendingUpdatesAsync(CancellationToken.None);

        // Assert - Should fall back to legacy file-based updates
        string targetFile = Path.Combine(_testDirectory, "legacy_file.dll");
        File.Exists(targetFile).Should().BeTrue("Legacy mode should copy files from staging");
        File.ReadAllText(targetFile).Should().Be("legacy content");
    }

    private class TestLogger : ILogger
    {
        public void LogDebug(string message, Exception? exception = null) { }
        public void LogInformation(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
        public void LogException(Exception exception, string? message = null) { }
    }
}
