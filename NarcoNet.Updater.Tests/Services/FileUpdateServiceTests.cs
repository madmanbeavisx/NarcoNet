using NarcoNet.Updater.Services;
using NarcoNet.Updater.Tests.TestHelpers;

using Newtonsoft.Json;

namespace NarcoNet.Updater.Tests.Services;

public class FileUpdateServiceTests : IDisposable
{
  private readonly TestLogger _logger;
  private readonly string _removedFilesManifestPath;
  private readonly string _testDirectory;
  private readonly string _updateStagingDirectory;

  public FileUpdateServiceTests()
  {
    _testDirectory = Path.Combine(Path.GetTempPath(), $"FileUpdateServiceTest_{Guid.NewGuid()}");
    _updateStagingDirectory = Path.Combine(_testDirectory, "Staging");
    _removedFilesManifestPath = Path.Combine(_testDirectory, "RemovedFiles.json");

    Directory.CreateDirectory(_testDirectory);
    Directory.CreateDirectory(_updateStagingDirectory);

    _logger = new TestLogger();
  }

  public void Dispose()
  {
    try
    {
      if (Directory.Exists(_testDirectory)) Directory.Delete(_testDirectory, true);
    }
    catch
    {
      // Ignore cleanup errors
    }
  }

  [Fact]
  public void Constructor_WithNullLogger_ThrowsArgumentNullException()
  {
    // Act
    Action act = () => new FileUpdateService(null!, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Assert
    act.Should().Throw<ArgumentNullException>()
      .WithParameterName("logger");
  }

  [Fact]
  public void Constructor_WithNullUpdateStagingDirectory_ThrowsArgumentNullException()
  {
    // Act
    Action act = () => new FileUpdateService(_logger, null!, _removedFilesManifestPath, _testDirectory);

    // Assert
    act.Should().Throw<ArgumentNullException>()
      .WithParameterName("updateStagingDirectory");
  }

  [Fact]
  public void Constructor_WithNullRemovedFilesManifestPath_ThrowsArgumentNullException()
  {
    // Act
    Action act = () => new FileUpdateService(_logger, _updateStagingDirectory, null!, _testDirectory);

    // Assert
    act.Should().Throw<ArgumentNullException>()
      .WithParameterName("removedFilesManifestPath");
  }

  [Fact]
  public void Constructor_WithNullTargetDirectory_ThrowsArgumentNullException()
  {
    // Act
    Action act = () => new FileUpdateService(_logger, _updateStagingDirectory, _removedFilesManifestPath, null!);

    // Assert
    act.Should().Throw<ArgumentNullException>()
      .WithParameterName("targetDirectory");
  }

  [Fact]
  public void HasPendingUpdates_WithEmptyDirectory_ReturnsFalse()
  {
    // Arrange
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    bool hasPendingUpdates = service.HasPendingUpdates();

    // Assert
    hasPendingUpdates.Should().BeFalse();
  }

  [Fact]
  public void HasPendingUpdates_WithFiles_ReturnsTrue()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateStagingDirectory, "test.txt"), "content");
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    bool hasPendingUpdates = service.HasPendingUpdates();

    // Assert
    hasPendingUpdates.Should().BeTrue();
  }

  [Fact]
  public void GetPendingUpdateFiles_WithNoFiles_ReturnsEmpty()
  {
    // Arrange
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    IEnumerable<string> files = service.GetPendingUpdateFiles();

    // Assert
    files.Should().BeEmpty();
  }

  [Fact]
  public void GetPendingUpdateFiles_WithFiles_ReturnsRelativePaths()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateStagingDirectory, "file1.txt"), "content");
    Directory.CreateDirectory(Path.Combine(_updateStagingDirectory, "subdir"));
    File.WriteAllText(Path.Combine(_updateStagingDirectory, "subdir", "file2.txt"), "content");

    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    List<string> files = service.GetPendingUpdateFiles().ToList();

    // Assert
    files.Should().HaveCount(2);
    files.Should().Contain("file1.txt");
    files.Should().Contain(Path.Combine("subdir", "file2.txt"));
  }

  [Fact]
  public async Task ApplyPendingUpdatesAsync_WithNoFiles_CompletesSuccessfully()
  {
    // Arrange
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.ApplyPendingUpdatesAsync();

    // Assert
    _logger.ContainsMessage("No pending updates").Should().BeTrue();
  }

  [Fact]
  public async Task ApplyPendingUpdatesAsync_WithFiles_CopiesFilesToTarget()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateStagingDirectory, "test.txt"), "test content");
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.ApplyPendingUpdatesAsync();

    // Assert
    string targetFile = Path.Combine(_testDirectory, "test.txt");
    File.Exists(targetFile).Should().BeTrue();
    File.ReadAllText(targetFile).Should().Be("test content");
    _logger.ContainsMessage("Successfully updated").Should().BeTrue();
  }

  [Fact]
  public async Task ApplyPendingUpdatesAsync_WithNestedFiles_CreatesDirectories()
  {
    // Arrange
    string nestedPath = Path.Combine(_updateStagingDirectory, "nested", "deep", "file.txt");
    Directory.CreateDirectory(Path.GetDirectoryName(nestedPath)!);
    File.WriteAllText(nestedPath, "nested content");

    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.ApplyPendingUpdatesAsync();

    // Assert
    string targetFile = Path.Combine(_testDirectory, "nested", "deep", "file.txt");
    File.Exists(targetFile).Should().BeTrue();
    File.ReadAllText(targetFile).Should().Be("nested content");
  }

  [Fact]
  public async Task ApplyPendingUpdatesAsync_WithExistingFile_OverwritesFile()
  {
    // Arrange
    string targetFile = Path.Combine(_testDirectory, "overwrite.txt");
    File.WriteAllText(targetFile, "old content");
    File.WriteAllText(Path.Combine(_updateStagingDirectory, "overwrite.txt"), "new content");

    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.ApplyPendingUpdatesAsync();

    // Assert
    File.ReadAllText(targetFile).Should().Be("new content");
  }

  [Fact]
  public async Task ApplyPendingUpdatesAsync_WithCancellation_ThrowsOperationCanceledException()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateStagingDirectory, "test.txt"), "content");
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);
    CancellationTokenSource cts = new();
    cts.Cancel();

    // Act
    Func<Task> act = async () => await service.ApplyPendingUpdatesAsync(cts.Token);

    // Assert
    await act.Should().ThrowAsync<OperationCanceledException>();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_WithNoManifest_CompletesSuccessfully()
  {
    // Arrange
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    _logger.ContainsMessage("No removed files manifest").Should().BeTrue();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_WithEmptyManifest_CompletesSuccessfully()
  {
    // Arrange
    File.WriteAllText(_removedFilesManifestPath, "[]");
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    _logger.ContainsMessage("No files to remove").Should().BeTrue();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_WithValidFiles_DeletesFiles()
  {
    // Arrange
    string fileToDelete = Path.Combine(_testDirectory, "delete_me.txt");
    File.WriteAllText(fileToDelete, "content");
    File.WriteAllText(_removedFilesManifestPath, "[\"delete_me.txt\"]");

    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    File.Exists(fileToDelete).Should().BeFalse();
    _logger.ContainsMessage("Successfully deleted").Should().BeTrue();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_WithNonExistentFile_LogsWarning()
  {
    // Arrange
    File.WriteAllText(_removedFilesManifestPath, "[\"nonexistent.txt\"]");
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    _logger.ContainsMessage("does not exist").Should().BeTrue();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_WithAbsolutePath_ThrowsInvalidOperationException()
  {
    // Arrange
    string absolutePath = Path.Combine(_testDirectory, "absolute.txt");
    string json = JsonConvert.SerializeObject(new[] { absolutePath });
    File.WriteAllText(_removedFilesManifestPath, json);
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    _logger.ContainsMessage("Failed to delete file").Should().BeTrue();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_WithPathTraversal_ThrowsInvalidOperationException()
  {
    // Arrange
    string json = JsonConvert.SerializeObject(new[] { "..\\..\\escape.txt" });
    File.WriteAllText(_removedFilesManifestPath, json);
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    _logger.ContainsMessage("Failed to delete file").Should().BeTrue();
  }

  [Fact]
  public async Task DeleteRemovedFilesAsync_DeletesManifestFileAfterProcessing()
  {
    // Arrange
    File.WriteAllText(_removedFilesManifestPath, "[]");
    FileUpdateService service = new(_logger, _updateStagingDirectory, _removedFilesManifestPath, _testDirectory);

    // Act
    await service.DeleteRemovedFilesAsync();

    // Assert
    File.Exists(_removedFilesManifestPath).Should().BeFalse();
  }
}
