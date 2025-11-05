using Comfort.Common;
using EFT.UI;
using NarcoNet.UI;
using UnityEngine;

namespace NarcoNet.Services;

using SyncPathFileList = Dictionary<string, List<string>>;

/// <summary>
///     Manages client UI windows and user interactions
/// </summary>
public class ClientUIService : IClientUIService
{
    private readonly AlertWindow _downloadErrorWindow = new(
        new Vector2(640f, 240f),
        "Download failed!",
        "There was an error updating mod files.\nPlease check BepInEx/LogOutput.log for more information.",
        "QUIT"
    );

    private readonly ProgressWindow _progressWindow =
        new("Downloading Updates...", "Your game will need to be restarted\nafter update completes.");

    private readonly AlertWindow _restartWindow =
        new(new Vector2(480f, 200f), "Update Complete.", "Please restart your game to continue.");

    private readonly UpdateWindow _updateWindow = new("Installed mods do not match server", "Would you like to update?");

    private int _downloadCount;
    private int _totalDownloadCount;
    private Action? _currentCancelAction;
    private Action? _currentAcceptAction;
    private Action? _currentSkipAction;
    private Action? _currentRestartAction;
    private Action? _currentQuitAction;
    private string _updateChanges = "";

    /// <inheritdoc/>
    public bool IsAnyWindowActive =>
        _updateWindow.Active || _progressWindow.Active || _restartWindow.Active || _downloadErrorWindow.Active;

    /// <inheritdoc/>
    public void ShowUpdateWindow(List<string> optional, List<string> required, Action onAccept, Action? onSkip)
    {
        _updateChanges = (optional.Count != 0 ? string.Join("\n", optional) : "")
            + (optional.Count != 0 && required.Count != 0 ? "\n\n" : "")
            + (required.Count != 0 ? "[Enforced]\n" + string.Join("\n", required) : "");

        _currentAcceptAction = onAccept;
        _currentSkipAction = onSkip;
        _updateWindow.Show();
    }

    /// <inheritdoc/>
    public void ShowProgressWindow()
    {
        _progressWindow.Show();
    }

    /// <inheritdoc/>
    public void UpdateProgress(int current, int total, Action? onCancel)
    {
        _downloadCount = current;
        _totalDownloadCount = total;
        _currentCancelAction = onCancel;
    }

    /// <inheritdoc/>
    public void HideProgressWindow()
    {
        _progressWindow.Hide();
    }

    /// <inheritdoc/>
    public void ShowRestartWindow(Action onRestart)
    {
        _currentRestartAction = onRestart;
        _restartWindow.Show();
    }

    /// <inheritdoc/>
    public void ShowErrorWindow(Action onQuit)
    {
        _currentQuitAction = onQuit;
        _downloadErrorWindow.Show();
    }

    /// <inheritdoc/>
    public void HideAllWindows()
    {
        _updateWindow.Hide();
        _progressWindow.Hide();
        _restartWindow.Hide();
        _downloadErrorWindow.Hide();
    }

    /// <inheritdoc/>
    public void DrawWindows()
    {
        if (!Singleton<CommonUI>.Instantiated)
        {
            return;
        }

        if (_restartWindow.Active && _currentRestartAction != null)
        {
            _restartWindow.Draw(_currentRestartAction);
        }

        if (_progressWindow.Active)
        {
            _progressWindow.Draw(_downloadCount, _totalDownloadCount, _currentCancelAction);
        }

        if (_updateWindow.Active && _currentAcceptAction != null)
        {
            _updateWindow.Draw(_updateChanges, _currentAcceptAction, _currentSkipAction);
        }

        if (_downloadErrorWindow.Active && _currentQuitAction != null)
        {
            _downloadErrorWindow.Draw(_currentQuitAction);
        }
    }

    /// <inheritdoc/>
    public void HandleGameUIVisibility(bool updateWindowsActive)
    {
        if (updateWindowsActive)
        {
            if (Singleton<LoginUI>.Instantiated && Singleton<LoginUI>.Instance.gameObject.activeSelf)
            {
                Singleton<LoginUI>.Instance.gameObject.SetActive(false);
            }

            if (Singleton<PreloaderUI>.Instantiated && Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
            {
                Singleton<PreloaderUI>.Instance.gameObject.SetActive(false);
            }

            if (Singleton<CommonUI>.Instantiated && Singleton<CommonUI>.Instance.gameObject.activeSelf)
            {
                Singleton<CommonUI>.Instance.gameObject.SetActive(false);
            }
        }
        else
        {
            if (Singleton<LoginUI>.Instantiated && !Singleton<LoginUI>.Instance.gameObject.activeSelf)
            {
                Singleton<LoginUI>.Instance.gameObject.SetActive(true);
            }

            if (Singleton<PreloaderUI>.Instantiated && !Singleton<PreloaderUI>.Instance.gameObject.activeSelf)
            {
                Singleton<PreloaderUI>.Instance.gameObject.SetActive(true);
            }

            if (Singleton<CommonUI>.Instantiated && !Singleton<CommonUI>.Instance.gameObject.activeSelf)
            {
                Singleton<CommonUI>.Instance.gameObject.SetActive(true);
            }
        }
    }
}
