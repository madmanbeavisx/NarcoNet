using NarcoNet.Utilities;

using UnityEngine;

namespace NarcoNet.UI;

internal class InfoBox(string title, string message, int borderThickness = 2, bool transparent = false) : Bordered
{
  private const int CornerRadius = 12;

  public void Draw(Vector2 size)
  {
    Rect borderRect = GUILayoutUtility.GetRect(size.x, size.y);

    // Draw shadow for depth
    Utility.DrawShadow(borderRect);

    // Draw rounded border
    DrawBorder(borderRect, borderThickness, Colors.PrimaryLight, CornerRadius);

    Rect infoRect =
      new(
        borderRect.x + borderThickness,
        borderRect.y + borderThickness,
        borderRect.width - (2 * borderThickness),
        borderRect.height - (2 * borderThickness)
      );

    if (!transparent)
      // Draw gradient background for modern look
      DrawGradientBox(infoRect, Colors.Dark.SetAlpha(0.7f), Colors.DarkMedium.SetAlpha(0.6f), false,
        CornerRadius - borderThickness);

    // Add subtle highlight at top for depth
    Rect highlightRect = new(infoRect.x, infoRect.y, infoRect.width, 2f);
    GUI.DrawTexture(highlightRect, Utility.GetTexture(Colors.Highlight), ScaleMode.StretchToFill, true, 0);

    GUIStyle titleStyle =
      new()
      {
        alignment = TextAnchor.LowerCenter,
        fontSize = 32,
        fontStyle = FontStyle.Bold,
        normal = { textColor = Colors.White }
      };

    GUIStyle messageStyle =
      new()
      {
        alignment = TextAnchor.MiddleCenter,
        fontSize = 18,
        normal = { textColor = Colors.OffWhite }
      };

    Rect titleRect = new(infoRect.x + 16f, infoRect.y + 8f, infoRect.width - 32f, infoRect.height / 2.75f);
    GUI.Label(titleRect, title, titleStyle);

    Rect messageRect = new(infoRect.x + 16f, infoRect.y + (infoRect.height / 2.5f), infoRect.width - 32f,
      infoRect.height / 2);
    GUI.Label(messageRect, message, messageStyle);
  }
}
