using NarcoNet.Services;

namespace NarcoNet.Tests.Services;

/// <summary>
///     Unit tests for ClientConfigService
/// </summary>
public class ClientConfigServiceTests
{
    [Fact]
    public void GetHeadlessDefaultExclusions_ReturnsExpectedList()
    {
        // Arrange
        var service = new ClientConfigService();

        // Act
        var exclusions = service.GetHeadlessDefaultExclusions();

        // Assert
        Assert.NotEmpty(exclusions);
        Assert.Contains("../BepInEx/plugins/AmandsGraphics.dll", exclusions);
        Assert.Contains("../BepInEx/plugins/AmandsSense.dll", exclusions);
        Assert.Contains("../BepInEx/plugins/DynamicMaps", exclusions);
    }

    [Fact]
    public void ThrowsException_WhenNotInitialized()
    {
        // Arrange
        var service = new ClientConfigService();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => service.DeleteRemovedFiles);
        Assert.Throws<InvalidOperationException>(() => service.SyncPathToggles);
        Assert.Throws<InvalidOperationException>(() => service.EnabledSyncPaths);
    }
}
