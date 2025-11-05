namespace NarcoNet.Updater.Interfaces;

/// <summary>
///     Defines the contract for user interface operations.
/// </summary>
public interface IUserInterfaceService
{
    /// <summary>
    ///     Displays an error message to the user.
    /// </summary>
    /// <param name="message">The error message to display.</param>
    /// <param name="title">The title of the error dialog.</param>
    void ShowError(string message, string title = "Error");

    /// <summary>
    ///     Displays a warning message to the user.
    /// </summary>
    /// <param name="message">The warning message to display.</param>
    /// <param name="title">The title of the warning dialog.</param>
    void ShowWarning(string message, string title = "Warning");

    /// <summary>
    ///     Displays an informational message to the user.
    /// </summary>
    /// <param name="message">The information message to display.</param>
    /// <param name="title">The title of the information dialog.</param>
    void ShowInformation(string message, string title = "Information");

    /// <summary>
    ///     Displays a confirmation dialog and returns the user's choice.
    /// </summary>
    /// <param name="message">The confirmation message to display.</param>
    /// <param name="title">The title of the confirmation dialog.</param>
    /// <returns>True if the user confirmed; otherwise, false.</returns>
    bool ShowConfirmation(string message, string title = "Confirm");

    /// <summary>
    ///     Creates and displays a progress window for update operations.
    /// </summary>
    /// <param name="processId">The process ID to monitor.</param>
    /// <returns>The dialog result from the progress window.</returns>
    DialogResult ShowProgressWindow(int processId);
}
