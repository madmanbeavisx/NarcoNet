namespace NarcoNet.Updater.UIElements;

/// <summary>
///   Modern themed label
/// </summary>
public sealed class ModernLabel : Label
{
  public ModernLabel()
  {
    Font = new Font("Segoe UI", 10F, FontStyle.Regular);
    ForeColor = ModernColors.OffWhite;
    AutoSize = true;
  }
}
