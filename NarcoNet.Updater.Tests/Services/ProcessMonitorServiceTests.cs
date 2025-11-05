using System.Diagnostics;

using NarcoNet.Updater.Services;
using NarcoNet.Updater.Tests.TestHelpers;

namespace NarcoNet.Updater.Tests.Services;

public class ProcessMonitorServiceTests
{
    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Act
        Action act = () => new ProcessMonitorService(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("logger");
    }

    [Fact]
    public void IsProcessRunning_WithCurrentProcess_ReturnsTrue()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        int currentProcessId = Process.GetCurrentProcess().Id;

        // Act
        bool isRunning = service.IsProcessRunning(currentProcessId);

        // Assert
        isRunning.Should().BeTrue();
    }

    [Fact]
    public void IsProcessRunning_WithNonExistentProcess_ReturnsFalse()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        var nonExistentProcessId = 999999;

        // Act
        bool isRunning = service.IsProcessRunning(nonExistentProcessId);

        // Assert
        isRunning.Should().BeFalse();
    }

    [Fact]
    public void IsProcessRunning_WithInvalidProcessId_ReturnsFalse()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        int invalidProcessId = -1;

        // Act
        bool isRunning = service.IsProcessRunning(invalidProcessId);

        // Assert
        isRunning.Should().BeFalse();
    }

    [Fact]
    public async Task WaitForProcessExitAsync_WithNonExistentProcess_CompletesImmediately()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        var nonExistentProcessId = 999999;

        // Act
        Stopwatch stopwatch = Stopwatch.StartNew();
        await service.WaitForProcessExitAsync(nonExistentProcessId, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(500);
        logger.ContainsMessage($"Process {nonExistentProcessId} has exited").Should().BeTrue();
    }

    [Fact]
    public async Task WaitForProcessExitAsync_WithCancellation_ThrowsTaskCanceledException()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        int currentProcessId = Process.GetCurrentProcess().Id;
        CancellationTokenSource cts = new();

        // Cancel after 100ms
        cts.CancelAfter(100);

        // Act
        Func<Task> act = async () => await service.WaitForProcessExitAsync(currentProcessId, cts.Token);

        // Assert
        await act.Should().ThrowAsync<TaskCanceledException>();
        logger.ContainsMessage("was cancelled").Should().BeTrue();
    }

    [Fact]
    public async Task WaitForProcessExitAsync_WithProgressCallback_InvokesCallback()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        int currentProcessId = Process.GetCurrentProcess().Id;
        CancellationTokenSource cts = new();
        var callbackInvocations = 0;

        void ProgressCallback(int iteration)
        {
            callbackInvocations++;
        }

        // Cancel after waiting for a few iterations
        cts.CancelAfter(2500);

        // Act
        try
        {
            await service.WaitForProcessExitAsync(currentProcessId, cts.Token, ProgressCallback);
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert
        callbackInvocations.Should().BeGreaterOrEqualTo(2);
    }

    [Fact]
    public async Task WaitForProcessExitAsync_WithShortLivedProcess_CompletesWhenProcessExits()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);

        // Start a short-lived process
        Process? process = Process.Start(new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = "/c echo test",
            CreateNoWindow = true,
            UseShellExecute = false
        });

        process.Should().NotBeNull();
        int processId = process!.Id;

        // Act
        Stopwatch stopwatch = Stopwatch.StartNew();
        await service.WaitForProcessExitAsync(processId, CancellationToken.None);
        stopwatch.Stop();

        // Assert
        stopwatch.ElapsedMilliseconds.Should().BeLessThan(5000);
        logger.ContainsMessage($"Process {processId} has exited").Should().BeTrue();
    }

    [Fact]
    public async Task WaitForProcessExitAsync_LogsWaitingMessages()
    {
        // Arrange
        TestLogger logger = new();
        ProcessMonitorService service = new(logger);
        int currentProcessId = Process.GetCurrentProcess().Id;
        CancellationTokenSource cts = new();

        cts.CancelAfter(1500);

        // Act
        try
        {
            await service.WaitForProcessExitAsync(currentProcessId, cts.Token);
        }
        catch (TaskCanceledException)
        {
            // Expected
        }

        // Assert
        logger.ContainsMessage("Waiting for process").Should().BeTrue();
        logger.ContainsMessage("still running").Should().BeTrue();
    }
}
