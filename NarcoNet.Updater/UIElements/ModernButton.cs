using System.ComponentModel;

namespace NarcoNet.Updater.UIElements;

/// <summary>
///     Modern themed button control
/// </summary>
public class ModernButton : Button
{
    private readonly Color _pressedColor = ModernColors.PrimaryDark;
    private Color _hoverColor = ModernColors.PrimaryLight;
    private bool _isHovering;
    private Color _normalColor = ModernColors.Primary;

    public ModernButton()
    {
        FlatStyle = FlatStyle.Flat;
        FlatAppearance.BorderSize = 0;
        BackColor = _normalColor;
        ForeColor = ModernColors.White;
        Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        Cursor = Cursors.Hand;
        Padding = new Padding(15, 8, 15, 8);

        MouseEnter += (s, e) =>
        {
            _isHovering = true;
            UpdateColors();
        };
        MouseLeave += (s, e) =>
        {
            _isHovering = false;
            UpdateColors();
        };
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color NormalColor
    {
        get => _normalColor;
        set
        {
            _normalColor = value;
            UpdateColors();
        }
    }

    [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
    public Color HoverColor
    {
        get => _hoverColor;
        set
        {
            _hoverColor = value;
            UpdateColors();
        }
    }

    protected override void OnMouseDown(MouseEventArgs mevent)
    {
        base.OnMouseDown(mevent);
        BackColor = _pressedColor;
    }

    protected override void OnMouseUp(MouseEventArgs mevent)
    {
        base.OnMouseUp(mevent);
        UpdateColors();
    }

    private void UpdateColors()
    {
        BackColor = _isHovering ? _hoverColor : _normalColor;
    }

    protected override void OnPaint(PaintEventArgs pevent)
    {
        base.OnPaint(pevent);

        // Draw subtle shadow
        if (_isHovering)
        {
            using SolidBrush shadowBrush = new(Color.FromArgb(40, 0, 0, 0));
            pevent.Graphics.FillRectangle(shadowBrush, 2, Height - 3, Width - 4, 3);
        }
    }
}
