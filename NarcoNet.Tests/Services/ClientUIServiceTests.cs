using NarcoNet.Services;

namespace NarcoNet.Tests.Services;

/// <summary>
///     Unit tests for ClientUIService
/// </summary>
public class ClientUIServiceTests
{
    [Fact]
    public void Constructor_InitializesWithNoActiveWindows()
    {
        // Arrange & Act
        var service = new ClientUIService();

        // Assert
        Assert.False(service.IsAnyWindowActive);
    }

    [Fact]
    public void ShowUpdateWindow_ActivatesUpdateWindow()
    {
        // Arrange
        var service = new ClientUIService();
        var optional = new List<string> { "file1.dll", "file2.dll" };
        var required = new List<string> { "file3.dll" };
        Action onAccept = () => { };

        // Act
        service.ShowUpdateWindow(optional, required, onAccept, null);

        // Assert
        Assert.True(service.IsAnyWindowActive);
    }

    [Fact]
    public void ShowProgressWindow_ActivatesProgressWindow()
    {
        // Arrange
        var service = new ClientUIService();

        // Act
        service.ShowProgressWindow();

        // Assert
        Assert.True(service.IsAnyWindowActive);
    }

    [Fact]
    public void HideAllWindows_DeactivatesAllWindows()
    {
        // Arrange
        var service = new ClientUIService();
        service.ShowProgressWindow();

        // Act
        service.HideAllWindows();

        // Assert
        Assert.False(service.IsAnyWindowActive);
    }

    [Fact]
    public void UpdateProgress_UpdatesProgressValues()
    {
        // Arrange
        var service = new ClientUIService();
        service.ShowProgressWindow();

        // Act & Assert (no exception should be thrown)
        service.UpdateProgress(5, 10, null);
        Assert.True(service.IsAnyWindowActive);
    }

    [Fact]
    public void ShowRestartWindow_ActivatesRestartWindow()
    {
        // Arrange
        var service = new ClientUIService();
        Action onRestart = () => { };

        // Act
        service.ShowRestartWindow(onRestart);

        // Assert
        Assert.True(service.IsAnyWindowActive);
    }

    [Fact]
    public void ShowErrorWindow_ActivatesErrorWindow()
    {
        // Arrange
        var service = new ClientUIService();
        Action onQuit = () => { };

        // Act
        service.ShowErrorWindow(onQuit);

        // Assert
        Assert.True(service.IsAnyWindowActive);
    }
}
