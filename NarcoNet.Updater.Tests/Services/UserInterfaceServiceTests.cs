using NarcoNet.Updater.Services;

namespace NarcoNet.Updater.Tests.Services;

public class UserInterfaceServiceTests
{
    [Fact]
    public void Constructor_CreatesServiceSuccessfully()
    {
        // Act
        UserInterfaceService service = new();

        // Assert
        service.Should().NotBeNull();
    }

    // Note: The remaining methods in UserInterfaceService (ShowError, ShowWarning, ShowInformation, 
    // ShowConfirmation, ShowProgressWindow) all display UI elements and would require:
    // 1. A running Windows Forms message loop
    // 2. User interaction or UI automation
    // 
    // These methods are best tested through integration tests or manual testing.
    // Unit testing GUI code typically focuses on the underlying logic rather than the UI itself.
    // The service acts as a thin wrapper around ModernMessageBox and UpdateProgressForm,
    // which should have their own tests if complex logic exists.
}
