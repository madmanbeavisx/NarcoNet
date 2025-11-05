using NarcoNet.Updater.Forms;
using NarcoNet.Updater.Interfaces;
using NarcoNet.Updater.UI;

namespace NarcoNet.Updater.Services;

/// <summary>
///     Provides user interface operations for displaying dialogs and windows.
/// </summary>
public class UserInterfaceService : IUserInterfaceService
{
    /// <inheritdoc />
    public void ShowError(string message, string title = "Error")
    {
        ModernMessageBox.ShowError(message, title);
    }

    /// <inheritdoc />
    public void ShowWarning(string message, string title = "Warning")
    {
        ModernMessageBox.ShowWarning(message, title);
    }

    /// <inheritdoc />
    public void ShowInformation(string message, string title = "Information")
    {
        ModernMessageBox.ShowInfo(message, title);
    }

    /// <inheritdoc />
    public bool ShowConfirmation(string message, string title = "Confirm")
    {
        return ModernMessageBox.ShowConfirmation(message, title);
    }

    /// <inheritdoc />
    public DialogResult ShowProgressWindow(int processId)
    {
        using UpdateProgressForm progressForm = new(processId);
        return progressForm.ShowDialog();
    }
}
