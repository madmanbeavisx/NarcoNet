using System.Drawing.Drawing2D;

using NarcoNet.Updater.UIElements;

namespace NarcoNet.Updater.Forms;

/// <summary>
///     Modern themed message box replacement
/// </summary>
public class ModernMessageBox : Form
{
    private readonly ModernButton? _cancelButton;
    private readonly PictureBox _iconBox;
    private readonly ModernLabel _messageLabel;
    private readonly ModernButton _okButton;
    private readonly ModernLabel _titleLabel;

    private ModernMessageBox(string title, string message, MessageBoxButtons buttons, MessageBoxIcon icon)
    {
        InitializeBaseForm(title);

        ModernPanel mainPanel = new()
        {
            Location = new Point(10, 10),
            Size = new Size(460, 0), // Height calculated below
            CornerRadius = 12
        };

        _titleLabel = new ModernLabel
        {
            Text = title,
            Location = new Point(70, 20),
            Size = new Size(370, 30),
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = ModernColors.White
        };

        _iconBox = new PictureBox
        {
            Location = new Point(20, 20),
            Size = new Size(40, 40),
            SizeMode = PictureBoxSizeMode.CenterImage
        };
        SetIcon(icon);

        _messageLabel = new ModernLabel
        {
            Text = message,
            Location = new Point(20, 70),
            Size = new Size(420, 0),
            Font = new Font("Segoe UI", 10F),
            ForeColor = ModernColors.OffWhite,
            AutoSize = true,
            MaximumSize = new Size(420, 0)
        };

        // Calculate message height
        using (Graphics g = CreateGraphics())
        {
            SizeF textSize = g.MeasureString(message, _messageLabel.Font, 420);
            _messageLabel.Height = (int)Math.Ceiling(textSize.Height) + 10;
        }

        int buttonY = _messageLabel.Bottom + 20;
        int panelHeight = buttonY + 60;

        mainPanel.Height = panelHeight;

        // Create buttons based on type
        if (buttons == MessageBoxButtons.OK)
        {
            _okButton = CreateButton("OK", ModernColors.Primary, ModernColors.PrimaryLight);
            _okButton.Location = new Point(170, buttonY);
            _okButton.Click += (s, e) =>
            {
                Result = DialogResult.OK;
                Close();
            };
            mainPanel.Controls.Add(_okButton);
        }
        else if (buttons == MessageBoxButtons.OKCancel)
        {
            _okButton = CreateButton("OK", ModernColors.Primary, ModernColors.PrimaryLight);
            _okButton.Location = new Point(130, buttonY);
            _okButton.Click += (s, e) =>
            {
                Result = DialogResult.OK;
                Close();
            };

            _cancelButton = CreateButton("CANCEL", ModernColors.Secondary, ModernColors.SecondaryLight);
            _cancelButton.Location = new Point(250, buttonY);
            _cancelButton.Click += (s, e) =>
            {
                Result = DialogResult.Cancel;
                Close();
            };

            mainPanel.Controls.Add(_okButton);
            mainPanel.Controls.Add(_cancelButton);
        }
        else if (buttons == MessageBoxButtons.YesNo)
        {
            _okButton = CreateButton("YES", ModernColors.Success, ModernColors.Success);
            _okButton.Location = new Point(130, buttonY);
            _okButton.Click += (s, e) =>
            {
                Result = DialogResult.Yes;
                Close();
            };

            _cancelButton = CreateButton("NO", ModernColors.Secondary, ModernColors.SecondaryLight);
            _cancelButton.Location = new Point(250, buttonY);
            _cancelButton.Click += (s, e) =>
            {
                Result = DialogResult.No;
                Close();
            };

            mainPanel.Controls.Add(_okButton);
            mainPanel.Controls.Add(_cancelButton);
        }
        else
        {
            _okButton = CreateButton("OK", ModernColors.Primary, ModernColors.PrimaryLight);
            _okButton.Location = new Point(170, buttonY);
            _okButton.Click += (s, e) =>
            {
                Result = DialogResult.OK;
                Close();
            };
            mainPanel.Controls.Add(_okButton);
        }

        mainPanel.Controls.Add(_titleLabel);
        mainPanel.Controls.Add(_iconBox);
        mainPanel.Controls.Add(_messageLabel);

        Controls.Add(mainPanel);
        Height = panelHeight + 30;

        // Add shadow effect
        Paint += (s, e) =>
        {
            using SolidBrush shadowBrush = new(Color.FromArgb(100, 0, 0, 0));
            e.Graphics.FillRectangle(shadowBrush, 12, 12, Width - 24, Height - 24);
        };
    }

    public DialogResult Result { get; private set; } = DialogResult.Cancel;

    private void InitializeBaseForm(string title)
    {
        Text = title;
        Size = new Size(480, 250);
        FormBorderStyle = FormBorderStyle.None;
        StartPosition = FormStartPosition.CenterScreen;
        this.ApplyModernTheme();
        ShowInTaskbar = false;
        TopMost = true;
    }

    private ModernButton CreateButton(string text, Color normal, Color hover)
    {
        return new ModernButton
        {
            Text = text,
            Size = new Size(110, 40),
            NormalColor = normal,
            HoverColor = hover
        };
    }

    private void SetIcon(MessageBoxIcon icon)
    {
        Color iconColor = icon switch
        {
            MessageBoxIcon.Error => ModernColors.Error,
            MessageBoxIcon.Warning => ModernColors.Warning,
            MessageBoxIcon.Information => ModernColors.Info,
            MessageBoxIcon.Question => ModernColors.Accent,
            _ => ModernColors.Grey
        };

        // Create a simple colored circle as icon
        Bitmap bitmap = new(40, 40);
        using (Graphics g = Graphics.FromImage(bitmap))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (SolidBrush brush = new(iconColor))
            {
                g.FillEllipse(brush, 2, 2, 36, 36);
            }

            // Draw icon symbol
            string symbol = icon switch
            {
                MessageBoxIcon.Error => "X",
                MessageBoxIcon.Warning => "!",
                MessageBoxIcon.Information => "i",
                MessageBoxIcon.Question => "?",
                _ => ""
            };

            using (Font font = new("Segoe UI", 18F, FontStyle.Bold))
            using (SolidBrush brush = new(ModernColors.White))
            {
                StringFormat sf = new()
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString(symbol, font, brush, new Rectangle(0, 0, 40, 40), sf);
            }
        }

        _iconBox.Image = bitmap;
    }

    /// <summary>
    ///     Show a modern message box
    /// </summary>
    public static DialogResult Show(string message, string title = "NarcoNet",
        MessageBoxButtons buttons = MessageBoxButtons.OK, MessageBoxIcon icon = MessageBoxIcon.Information)
    {
        using ModernMessageBox msgBox = new(title, message, buttons, icon);
        msgBox.ShowDialog();
        return msgBox.Result;
    }

    /// <summary>
    ///     Show an error message
    /// </summary>
    public static void ShowError(string message, string title = "Error")
    {
        Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    ///     Show a warning message
    /// </summary>
    public static void ShowWarning(string message, string title = "Warning")
    {
        Show(message, title, MessageBoxButtons.OK, MessageBoxIcon.Warning);
    }

    /// <summary>
    ///     Show an information message
    /// </summary>
    public static void ShowInfo(string message, string title = "Information")
    {
        Show(message, title);
    }

    /// <summary>
    ///     Show a confirmation dialog
    /// </summary>
    public static bool ShowConfirmation(string message, string title = "Confirm")
    {
        return Show(message, title, MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes;
    }
}
