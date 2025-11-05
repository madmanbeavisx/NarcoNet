using System.Drawing.Drawing2D;

namespace NarcoNet.Updater.UIElements;

/// <summary>
///     Modern themed progress bar
/// </summary>
public class ModernProgressBar : ProgressBar
{
    public ModernProgressBar()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        Height = 30;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        Graphics g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;

        // Background with rounded corners
        using (SolidBrush bgBrush = new(ModernColors.DarkMedium))
        {
            Rectangle bgRect = new(0, 0, Width, Height);
            FillRoundedRectangle(g, bgBrush, bgRect, 8);
        }

        // Progress fill with gradient
        if (Value > 0)
        {
            int progressWidth = (int)((float)Value / Maximum * Width);
            Rectangle progressRect = new(0, 0, progressWidth, Height);

            using (LinearGradientBrush gradientBrush = new(
                       progressRect,
                       ModernColors.Primary,
                       ModernColors.PrimaryLight,
                       LinearGradientMode.Horizontal))
            {
                FillRoundedRectangle(g, gradientBrush, progressRect, 8);
            }

            // Shine effect
            using (SolidBrush shineBrush = new(Color.FromArgb(40, 255, 255, 255)))
            {
                Rectangle shineRect = new(0, 0, progressWidth, Height / 2);
                FillRoundedRectangle(g, shineBrush, shineRect, 8);
            }
        }

        // Text
        string text = Style == ProgressBarStyle.Marquee
            ? "Processing..."
            : $"{Value}%";

        using (Font font = new("Segoe UI", 10F, FontStyle.Bold))
        using (SolidBrush textBrush = new(ModernColors.White))
        {
            SizeF textSize = g.MeasureString(text, font);
            float textX = (Width - textSize.Width) / 2;
            float textY = (Height - textSize.Height) / 2;

            // Text shadow
            using (SolidBrush shadowBrush = new(Color.FromArgb(150, 0, 0, 0)))
            {
                g.DrawString(text, font, shadowBrush, textX + 1, textY + 1);
            }

            g.DrawString(text, font, textBrush, textX, textY);
        }
    }

    private void FillRoundedRectangle(Graphics g, Brush brush, Rectangle rect, int radius)
    {
        if (radius <= 0)
        {
            g.FillRectangle(brush, rect);
            return;
        }

        using GraphicsPath path = new();
        path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
        path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
        path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
        path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
        path.CloseFigure();
        g.FillPath(brush, path);
    }
}
