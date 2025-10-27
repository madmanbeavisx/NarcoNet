using NarcoNet.Updater.Infrastructure;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.Tests.Infrastructure;

public class HealthCheckTests : IDisposable
{
  private readonly string _dataDirectory;
  private readonly string _testDirectory;
  private readonly string _updateDirectory;

  public HealthCheckTests()
  {
    _testDirectory = Path.Combine(Path.GetTempPath(), $"HealthCheckTest_{Guid.NewGuid()}");
    _dataDirectory = Path.Combine(_testDirectory, NarcoNetConstants.DataDirectoryName);
    _updateDirectory = Path.Combine(_dataDirectory, NarcoNetConstants.PendingUpdatesDirectoryName);

    Directory.CreateDirectory(_testDirectory);
    Directory.CreateDirectory(_dataDirectory);
    Directory.CreateDirectory(_updateDirectory);

    // Change to test directory for environment checks
    Directory.SetCurrentDirectory(_testDirectory);
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
  public void HealthCheckResult_Healthy_CreatesHealthyResult()
  {
    // Act
    HealthCheckResult result = HealthCheckResult.Healthy("Test healthy");

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
    result.Description.Should().Be("Test healthy");
    result.Data.Should().NotBeNull();
    result.Exception.Should().BeNull();
  }

  [Fact]
  public void HealthCheckResult_Degraded_CreatesDegradedResult()
  {
    // Act
    HealthCheckResult result = HealthCheckResult.Degraded("Test degraded");

    // Assert
    result.Status.Should().Be(HealthStatus.Degraded);
    result.Description.Should().Be("Test degraded");
  }

  [Fact]
  public void HealthCheckResult_Unhealthy_CreatesUnhealthyResult()
  {
    // Arrange
    Exception exception = new("Test exception");

    // Act
    HealthCheckResult result = HealthCheckResult.Unhealthy("Test unhealthy", exception);

    // Assert
    result.Status.Should().Be(HealthStatus.Unhealthy);
    result.Description.Should().Be("Test unhealthy");
    result.Exception.Should().Be(exception);
  }

  [Fact]
  public void HealthCheckResult_WithData_IncludesData()
  {
    // Arrange
    Dictionary<string, object> data = new() { { "Key", "Value" } };

    // Act
    HealthCheckResult result = HealthCheckResult.Healthy("Test", data);

    // Assert
    result.Data.Should().ContainKey("Key");
    result.Data["Key"].Should().Be("Value");
  }

  [Fact]
  public void HealthCheckResult_WithDuration_IncludesDuration()
  {
    // Arrange
    TimeSpan duration = TimeSpan.FromSeconds(2);

    // Act
    HealthCheckResult result = HealthCheckResult.Healthy("Test", duration: duration);

    // Assert
    result.Duration.Should().Be(duration);
  }

  [Fact]
  public async Task CheckEnvironmentAsync_WithoutTarkovExe_ReturnsUnhealthy()
  {
    // Act
    HealthCheckResult result = await HealthCheck.CheckEnvironmentAsync();

    // Assert
    result.Status.Should().Be(HealthStatus.Unhealthy);
    result.Description.Should().Contain("EscapeFromTarkov.exe not found");
    result.Data.Should().ContainKey("TarkovExeExists");
    result.Data["TarkovExeExists"].Should().Be(false);
  }

  [Fact]
  public async Task CheckEnvironmentAsync_WithoutDataDirectory_ReturnsUnhealthy()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_testDirectory, "EscapeFromTarkov.exe"), "dummy");
    Directory.Delete(_dataDirectory, true);

    // Act
    HealthCheckResult result = await HealthCheck.CheckEnvironmentAsync();

    // Assert
    result.Status.Should().Be(HealthStatus.Unhealthy);
    result.Description.Should().Contain("NarcoNet_Data directory not found");
    result.Data.Should().ContainKey("DataDirectoryExists");
    result.Data["DataDirectoryExists"].Should().Be(false);
  }

  [Fact]
  public async Task CheckEnvironmentAsync_WithValidEnvironment_ReturnsHealthy()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_testDirectory, "EscapeFromTarkov.exe"), "dummy");

    // Act
    HealthCheckResult result = await HealthCheck.CheckEnvironmentAsync();

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
    result.Description.Should().Contain("healthy and ready");
    result.Data.Should().ContainKey("TarkovExeExists");
    result.Data["TarkovExeExists"].Should().Be(true);
    result.Data.Should().ContainKey("DataDirectoryExists");
    result.Data["DataDirectoryExists"].Should().Be(true);
    result.Data.Should().ContainKey("WritePermissions");
    result.Data["WritePermissions"].Should().Be(true);
    result.Data.Should().ContainKey("AvailableDiskSpaceGB");
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_WithNonExistentDirectory_ReturnsHealthy()
  {
    // Arrange
    string nonExistentDir = Path.Combine(_testDirectory, "NonExistent");

    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(nonExistentDir);

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
    result.Description.Should().Contain("No pending updates");
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_WithEmptyDirectory_ReturnsHealthy()
  {
    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
    result.Description.Should().Contain("No pending updates");
    result.Data["UpdateFileCount"].Should().Be(0);
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_WithNormalFiles_ReturnsHealthy()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateDirectory, "config.json"), "{}");
    File.WriteAllText(Path.Combine(_updateDirectory, "data.txt"), "data");

    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Status.Should().Be(HealthStatus.Healthy);
    result.Data["UpdateFileCount"].Should().Be(2);
    result.Data.Should().ContainKey("TotalUpdateSizeMB");
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_WithSuspiciousFiles_ReturnsDegraded()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateDirectory, "malicious.exe"), "executable");
    File.WriteAllText(Path.Combine(_updateDirectory, "script.bat"), "script");

    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Status.Should().Be(HealthStatus.Degraded);
    result.Description.Should().Contain("potentially executable files");
    result.Data["SuspiciousFileCount"].Should().Be(2);
    result.Data.Should().ContainKey("SuspiciousFiles");
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_DetectsExeFiles()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateDirectory, "file.exe"), "content");

    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Status.Should().Be(HealthStatus.Degraded);
    List<string>? suspiciousFiles = result.Data["SuspiciousFiles"] as List<string>;
    suspiciousFiles.Should().Contain("file.exe");
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_DetectsDllFiles()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateDirectory, "library.dll"), "content");

    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Status.Should().Be(HealthStatus.Degraded);
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_DetectsPowerShellScripts()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_updateDirectory, "script.ps1"), "content");

    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Status.Should().Be(HealthStatus.Degraded);
  }

  [Fact]
  public async Task CheckAllAsync_RunsAllChecks()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_testDirectory, "EscapeFromTarkov.exe"), "dummy");

    // Act
    Dictionary<string, HealthCheckResult> results = await HealthCheck.CheckAllAsync();

    // Assert
    results.Should().ContainKey("Environment");
    results.Should().ContainKey("PendingUpdates");
    results["Environment"].Status.Should().Be(HealthStatus.Healthy);
    results["PendingUpdates"].Status.Should().Be(HealthStatus.Healthy);
  }

  [Fact]
  public void GetOverallStatus_WithAllHealthy_ReturnsHealthy()
  {
    // Arrange
    Dictionary<string, HealthCheckResult> results = new()
    {
      { "Check1", HealthCheckResult.Healthy("ok") },
      { "Check2", HealthCheckResult.Healthy("ok") }
    };

    // Act
    HealthStatus status = HealthCheck.GetOverallStatus(results);

    // Assert
    status.Should().Be(HealthStatus.Healthy);
  }

  [Fact]
  public void GetOverallStatus_WithOneDegraded_ReturnsDegraded()
  {
    // Arrange
    Dictionary<string, HealthCheckResult> results = new()
    {
      { "Check1", HealthCheckResult.Healthy("ok") },
      { "Check2", HealthCheckResult.Degraded("warning") }
    };

    // Act
    HealthStatus status = HealthCheck.GetOverallStatus(results);

    // Assert
    status.Should().Be(HealthStatus.Degraded);
  }

  [Fact]
  public void GetOverallStatus_WithOneUnhealthy_ReturnsUnhealthy()
  {
    // Arrange
    Dictionary<string, HealthCheckResult> results = new()
    {
      { "Check1", HealthCheckResult.Healthy("ok") },
      { "Check2", HealthCheckResult.Degraded("warning") },
      { "Check3", HealthCheckResult.Unhealthy("error") }
    };

    // Act
    HealthStatus status = HealthCheck.GetOverallStatus(results);

    // Assert
    status.Should().Be(HealthStatus.Unhealthy);
  }

  [Fact]
  public async Task CheckEnvironmentAsync_RecordsDuration()
  {
    // Arrange
    File.WriteAllText(Path.Combine(_testDirectory, "EscapeFromTarkov.exe"), "dummy");

    // Act
    HealthCheckResult result = await HealthCheck.CheckEnvironmentAsync();

    // Assert
    result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
  }

  [Fact]
  public async Task CheckPendingUpdatesAsync_RecordsDuration()
  {
    // Act
    HealthCheckResult result = await HealthCheck.CheckPendingUpdatesAsync(_updateDirectory);

    // Assert
    result.Duration.Should().BeGreaterThan(TimeSpan.Zero);
  }
}
