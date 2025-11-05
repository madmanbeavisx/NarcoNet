using NarcoNet.Updater.Core;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.Tests.Core;

public class ApplicationCoordinatorTests : IDisposable
{
    private readonly string _dataDirectory;
    private readonly string _testDirectory;
    private readonly string _updateDirectory;

    public ApplicationCoordinatorTests()
    {
        // Create temporary test directory structure
        _testDirectory = Path.Combine(Path.GetTempPath(), $"NarcoNetTest_{Guid.NewGuid()}");
        _dataDirectory = Path.Combine(_testDirectory, NarcoNetConstants.DataDirectoryName);
        _updateDirectory = Path.Combine(_dataDirectory, NarcoNetConstants.PendingUpdatesDirectoryName);

        Directory.CreateDirectory(_testDirectory);
        Directory.CreateDirectory(_dataDirectory);
        Directory.CreateDirectory(_updateDirectory);

        // Create EscapeFromTarkov.exe to simulate valid environment
        File.WriteAllText(Path.Combine(_testDirectory, "EscapeFromTarkov.exe"), "dummy");

        // Change to test directory
        Directory.SetCurrentDirectory(_testDirectory);
    }

    public void Dispose()
    {
        // Cleanup test directory
        try
        {
            if (Directory.Exists(_testDirectory))
            {
                Directory.Delete(_testDirectory, true);
            }
        }
        catch
        {
            // Ignore cleanup errors
        }
    }

    [Fact]
    public void Constructor_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ApplicationCoordinator(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("configuration");
    }

    [Fact]
    public void Constructor_WithValidConfiguration_CreatesCoordinator()
    {
        // Arrange
        ApplicationConfiguration config = new(1234, false);

        // Act
        ApplicationCoordinator coordinator = new(config);

        // Assert
        coordinator.Should().NotBeNull();
    }

    [Fact]
    public void Execute_WhenTarkovExeNotFound_ReturnsEnvironmentValidationFailed()
    {
        // Arrange
        File.Delete(Path.Combine(_testDirectory, "EscapeFromTarkov.exe"));
        ApplicationConfiguration config = new(1234, true);
        ApplicationCoordinator coordinator = new(config);

        // Act
        int exitCode = coordinator.Execute();

        // Assert
        exitCode.Should().Be(ApplicationCoordinator.ExitCode.EnvironmentValidationFailed);
    }

    [Fact]
    public void Execute_WhenDataDirectoryNotFound_ReturnsEnvironmentValidationFailed()
    {
        // Arrange
        Directory.Delete(_dataDirectory, true);
        ApplicationConfiguration config = new(1234, true);
        ApplicationCoordinator coordinator = new(config);

        // Act
        int exitCode = coordinator.Execute();

        // Assert
        exitCode.Should().Be(ApplicationCoordinator.ExitCode.EnvironmentValidationFailed);
    }

    [Fact]
    public void Execute_WhenNoPendingUpdates_ReturnsSuccess()
    {
        // Arrange
        // Empty update directory means no pending updates
        ApplicationConfiguration config = new(1234, true);
        ApplicationCoordinator coordinator = new(config);

        // Act
        int exitCode = coordinator.Execute();

        // Assert
        exitCode.Should().Be(ApplicationCoordinator.ExitCode.Success);
    }

    [Fact]
    public void Execute_InSilentModeWithPendingUpdates_CopiesFilesAndReturnsSuccess()
    {
        // Arrange
        // Create a pending update file
        File.WriteAllText(Path.Combine(_updateDirectory, "test.txt"), "content");

        // Mock a non-existent process so WaitForProcessExitSynchronously completes immediately
        ApplicationConfiguration config = new(99999, true); // Use high PID that doesn't exist
        ApplicationCoordinator coordinator = new(config);

        // Act
        int exitCode = coordinator.Execute();

        // Assert
        exitCode.Should().Be(ApplicationCoordinator.ExitCode.Success);
        File.Exists(Path.Combine(_testDirectory, "test.txt")).Should().BeTrue();
    }

    [Fact]
    public void Execute_InSilentModeWithRemovedFiles_DeletesFilesAndReturnsSuccess()
    {
        // Arrange
        // Create a file to be removed
        File.WriteAllText(Path.Combine(_testDirectory, "to_remove.txt"), "content");

        // Create removed files list
        string removedFilesPath = Path.Combine(_dataDirectory, NarcoNetConstants.RemovedFilesFileName);
        File.WriteAllText(removedFilesPath, "[\"to_remove.txt\"]");

        // Create a pending update to trigger execution
        File.WriteAllText(Path.Combine(_updateDirectory, "test.txt"), "content");

        ApplicationConfiguration config = new(99999, true);
        ApplicationCoordinator coordinator = new(config);

        // Act
        int exitCode = coordinator.Execute();

        // Assert
        exitCode.Should().Be(ApplicationCoordinator.ExitCode.Success);
        File.Exists(Path.Combine(_testDirectory, "to_remove.txt")).Should().BeFalse();
    }

    [Fact]
    public void ExitCode_HasCorrectValues()
    {
        // Assert
        ApplicationCoordinator.ExitCode.Success.Should().Be(0);
        ApplicationCoordinator.ExitCode.InvalidArguments.Should().Be(1);
        ApplicationCoordinator.ExitCode.EnvironmentValidationFailed.Should().Be(2);
        ApplicationCoordinator.ExitCode.UpdateFailed.Should().Be(3);
        ApplicationCoordinator.ExitCode.UserCancelled.Should().Be(4);
        ApplicationCoordinator.ExitCode.UnexpectedError.Should().Be(99);
    }
}
