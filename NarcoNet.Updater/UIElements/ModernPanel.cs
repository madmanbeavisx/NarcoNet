using System.ComponentModel;
using System.Drawing.Drawing2D;

namespace NarcoNet.Updater.UIElements;

/// <summary>
///   Modern themed panel
/// </summary>
public class ModernPanel : Panel
{
  private int _cornerRadius = 12;

  public ModernPanel()
  {
    BackColor = ModernColors.Dark;
    DoubleBuffered = true;
  }

  [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
  public int CornerRadius
  {
    get => _cornerRadius;
    set
    {
      _cornerRadius = value;
      Invalidate();
    }
  }

  protected override void OnPaint(PaintEventArgs e)
  {
    base.OnPaint(e);

    if (_cornerRadius > 0)
    {
      Graphics g = e.Graphics;
      g.SmoothingMode = SmoothingMode.AntiAlias;

      using GraphicsPath path = new();
      Rectangle rect = new(0, 0, Width - 1, Height - 1);
      int radius = _cornerRadius;

      path.AddArc(rect.X, rect.Y, radius, radius, 180, 90);
      path.AddArc(rect.Right - radius, rect.Y, radius, radius, 270, 90);
      path.AddArc(rect.Right - radius, rect.Bottom - radius, radius, radius, 0, 90);
      path.AddArc(rect.X, rect.Bottom - radius, radius, radius, 90, 90);
      path.CloseFigure();

      Region = new Region(path);
    }
  }
}
