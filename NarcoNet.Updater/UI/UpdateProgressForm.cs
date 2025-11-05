using NarcoNet.Updater.Forms;
using NarcoNet.Updater.Interfaces;
using NarcoNet.Updater.Services;
using NarcoNet.Updater.UIElements;
using NarcoNet.Utilities;

namespace NarcoNet.Updater.UI;

/// <summary>
///     Displays a modern progress window for monitoring and executing update operations.
/// </summary>
public sealed class UpdateProgressForm : Form
{
    private readonly IFileUpdateService _fileUpdateService;
    private readonly ILogger _logger;
    private readonly IProcessMonitor _processMonitor;
    private readonly int _targetProcessId;
    private ModernButton _cancelButton = null!;
    private CancellationTokenSource? _cancellationTokenSource;
    private ModernPanel _mainPanel = null!;

    private ModernProgressBar _progressBar = null!;
    private ModernLabel _statusLabel = null!;
    private ModernLabel _titleLabel = null!;

    /// <summary>
    ///     Initializes a new instance of the <see cref="UpdateProgressForm" /> class.
    /// </summary>
    /// <param name="targetProcessId">The process ID to wait for before applying updates.</param>
    public UpdateProgressForm(int targetProcessId)
    {
        _targetProcessId = targetProcessId;
        _logger = FileLogger.Instance;

        // Initialize services with proper dependency injection
        _processMonitor = new ProcessMonitorService(_logger);

        string dataDirectory = Path.Combine(Environment.CurrentDirectory, NarcoNetConstants.DataDirectoryName);
        string updateDirectory = Path.Combine(dataDirectory, NarcoNetConstants.PendingUpdatesDirectoryName);
        string removedFilesPath = Path.Combine(dataDirectory, NarcoNetConstants.RemovedFilesFileName);
        string updateManifestPath = Path.Combine(dataDirectory, NarcoNetConstants.UpdateManifestFileName);

        _fileUpdateService = new FileUpdateService(
            _logger,
            updateDirectory,
            removedFilesPath,
            Environment.CurrentDirectory,
            updateManifestPath
        );

        InitializeUserInterface();
        StartUpdateProcessAsync();
    }

    /// <summary>
    ///     Initializes all UI components and configures the form appearance.
    /// </summary>
    private void InitializeUserInterface()
    {
        ConfigureFormProperties();
        CreateMainPanel();
        CreateTitleLabel();
        CreateStatusLabel();
        CreateProgressBar();
        CreateCancelButton();
        AttachEventHandlers();
    }

    /// <summary>
    ///     Configures the main form properties.
    /// </summary>
    private void ConfigureFormProperties()
    {
        this.ApplyModernTheme();
        Text = NarcoNetConstants.FullProductName + " - Updater";
        Size = new Size(600, 280);
        FormBorderStyle = FormBorderStyle.None;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
    }

    /// <summary>
    ///     Creates the main container panel with rounded corners.
    /// </summary>
    private void CreateMainPanel()
    {
        _mainPanel = new ModernPanel
        {
            Location = new Point(10, 10),
            Size = new Size(580, 260),
            CornerRadius = 16
        };

        Controls.Add(_mainPanel);
    }

    /// <summary>
    ///     Creates the title label at the top of the form.
    /// </summary>
    private void CreateTitleLabel()
    {
        _titleLabel = new ModernLabel
        {
            Text = "NarcoNet Updater",
            Location = new Point(30, 25),
            Size = new Size(520, 40),
            Font = new Font("Segoe UI", 18F, FontStyle.Bold),
            ForeColor = ModernColors.PrimaryLight,
            TextAlign = ContentAlignment.MiddleCenter
        };

        _mainPanel.Controls.Add(_titleLabel);
    }

    /// <summary>
    ///     Creates the status label for displaying operation progress.
    /// </summary>
    private void CreateStatusLabel()
    {
        _statusLabel = new ModernLabel
        {
            Text = NarcoNetConstants.Messages.WaitingForTarkovClose,
            Location = new Point(30, 80),
            Size = new Size(520, 30),
            Font = new Font("Segoe UI", 11F, FontStyle.Regular),
            TextAlign = ContentAlignment.MiddleCenter
        };

        _mainPanel.Controls.Add(_statusLabel);
    }

    /// <summary>
    ///     Creates the progress bar for visual feedback.
    /// </summary>
    private void CreateProgressBar()
    {
        _progressBar = new ModernProgressBar
        {
            Location = new Point(30, 125),
            Size = new Size(520, 35),
            Style = ProgressBarStyle.Marquee,
            MarqueeAnimationSpeed = 20
        };

        _mainPanel.Controls.Add(_progressBar);
    }

    /// <summary>
    ///     Creates the cancel button for aborting the update process.
    /// </summary>
    private void CreateCancelButton()
    {
        _cancelButton = new ModernButton
        {
            Text = "CANCEL UPDATE",
            Location = new Point(220, 185),
            Size = new Size(140, 40),
            NormalColor = ModernColors.Secondary,
            HoverColor = ModernColors.SecondaryLight
        };

        _cancelButton.Click += OnCancelButtonClick;
        _mainPanel.Controls.Add(_cancelButton);
    }

    /// <summary>
    ///     Attaches event handlers to form events.
    /// </summary>
    private void AttachEventHandlers()
    {
        FormClosing += OnFormClosing;
        Paint += OnFormPaint;
    }

    /// <summary>
    ///     Starts the asynchronous update process.
    /// </summary>
    private async void StartUpdateProcessAsync()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            await ExecuteUpdateSequenceAsync(_cancellationTokenSource.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Update cancelled by user");
            Close();
        }
        catch (Exception ex)
        {
            _logger.LogException(ex, "Critical error during update");
            ModernMessageBox.ShowError(
                $"A critical error occurred during the update: {ex.Message}",
                "Update Failed"
            );
            Close();
        }
    }

    /// <summary>
    ///     Executes the complete update sequence.
    /// </summary>
    private async Task ExecuteUpdateSequenceAsync(CancellationToken cancellationToken)
    {
        // Step 1: Wait for target process to exit
        await WaitForProcessExitAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Step 2: Apply pending file updates
        UpdateStatus(NarcoNetConstants.Messages.CopyingFiles);
        await _fileUpdateService.ApplyPendingUpdatesAsync(cancellationToken);

        if (cancellationToken.IsCancellationRequested)
        {
            return;
        }

        // Step 3: Delete removed files
        UpdateStatus(NarcoNetConstants.Messages.DeletingFiles);
        await _fileUpdateService.DeleteRemovedFilesAsync(cancellationToken);

        // Step 4: Complete
        CompleteUpdate();
    }

    /// <summary>
    ///     Waits for the target process to exit before proceeding.
    /// </summary>
    private async Task WaitForProcessExitAsync(CancellationToken cancellationToken)
    {
        await _processMonitor.WaitForProcessExitAsync(
            _targetProcessId,
            cancellationToken,
            secondsWaited => UpdateStatus($"{NarcoNetConstants.Messages.WaitingForTarkovClose} ({secondsWaited}s)")
        );
    }

    /// <summary>
    ///     Finalizes the update process and closes the form.
    /// </summary>
    private async void CompleteUpdate()
    {
        UpdateStatus(NarcoNetConstants.Messages.UpdateComplete);
        _progressBar.Style = ProgressBarStyle.Continuous;
        _progressBar.Value = 100;

        await Task.Delay(1500);
        Close();
    }

    /// <summary>
    ///     Updates the status label with a new message (thread-safe).
    /// </summary>
    /// <param name="message">The status message to display.</param>
    private void UpdateStatus(string message)
    {
        if (_statusLabel.InvokeRequired)
        {
            _statusLabel.Invoke(new Action(() => _statusLabel.Text = message));
        }
        else
        {
            _statusLabel.Text = message;
        }

        _logger.LogInformation(message);
    }

    /// <summary>
    ///     Handles the cancel button click event.
    /// </summary>
    private void OnCancelButtonClick(object? sender, EventArgs e)
    {
        _cancellationTokenSource?.Cancel();
        Close();
    }

    /// <summary>
    ///     Handles the form closing event to ensure cleanup.
    /// </summary>
    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        _cancellationTokenSource?.Cancel();
    }

    /// <summary>
    ///     Handles custom paint for drop shadow effect.
    /// </summary>
    private void OnFormPaint(object? sender, PaintEventArgs e)
    {
        using SolidBrush shadowBrush = new(Color.FromArgb(100, 0, 0, 0));
        e.Graphics.FillRectangle(shadowBrush, 12, 12, 580, 260);
    }

    /// <summary>
    ///     Disposes resources used by the form.
    /// </summary>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _cancellationTokenSource?.Dispose();
        }

        base.Dispose(disposing);
    }
}
