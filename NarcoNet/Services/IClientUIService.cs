using NarcoNet.Utilities;

namespace NarcoNet.Services;

using SyncPathFileList = Dictionary<string, List<string>>;

/// <summary>
///     Service interface for managing client UI windows and user interactions
/// </summary>
public interface IClientUIService
{
    /// <summary>
    ///     Gets whether any UI window is currently active
    /// </summary>
    bool IsAnyWindowActive { get; }

    /// <summary>
    ///     Shows the update confirmation window with the list of changes
    /// </summary>
    void ShowUpdateWindow(List<string> optional, List<string> required, Action onAccept, Action? onSkip);

    /// <summary>
    ///     Shows the download progress window
    /// </summary>
    void ShowProgressWindow();

    /// <summary>
    ///     Updates the download progress
    /// </summary>
    void UpdateProgress(int current, int total, Action? onCancel);

    /// <summary>
    ///     Hides the progress window
    /// </summary>
    void HideProgressWindow();

    /// <summary>
    ///     Shows the restart required window
    /// </summary>
    void ShowRestartWindow(Action onRestart);

    /// <summary>
    ///     Shows the download error window
    /// </summary>
    void ShowErrorWindow(Action onQuit);

    /// <summary>
    ///     Hides all windows
    /// </summary>
    void HideAllWindows();

    /// <summary>
    ///     Draws all active UI windows (called from OnGUI)
    /// </summary>
    void DrawWindows();

    /// <summary>
    ///     Handles game UI visibility when update windows are shown
    /// </summary>
    void HandleGameUIVisibility(bool updateWindowsActive);
}
