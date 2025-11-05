namespace NarcoNet.Updater;

/// <summary>
///     Extension methods for theming standard controls
/// </summary>
public static class ControlTheming
{
    public static void ApplyModernTheme(this Form form)
    {
        form.BackColor = ModernColors.DarkMedium;
        form.ForeColor = ModernColors.White;
        form.Font = new Font("Segoe UI", 9F);
    }

    public static void ApplyModernTheme(this Button button)
    {
        button.FlatStyle = FlatStyle.Flat;
        button.FlatAppearance.BorderSize = 0;
        button.BackColor = ModernColors.Primary;
        button.ForeColor = ModernColors.White;
        button.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
        button.Cursor = Cursors.Hand;
    }

    public static void ApplyModernTheme(this Label label)
    {
        label.ForeColor = ModernColors.OffWhite;
        label.Font = new Font("Segoe UI", 10F);
    }
}
