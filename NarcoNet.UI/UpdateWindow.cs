using NarcoNet.Utilities;

using UnityEngine;

namespace NarcoNet.UI;

public class UpdateWindow(
  string title,
  string message,
  string continueText = "CONTINUE",
  string cancelText = "SKIP UPDATE")
{
  private readonly UpdateBox _alertBox = new(title, message, continueText, cancelText);
  public bool Active { get; private set; }

  public void Show()
  {
    Active = true;
  }

  public void Hide()
  {
    Active = false;
  }

  public void Draw(string updatesText, Action onAccept, Action? onDecline)
  {
    float screenWidth = Screen.width;
    float screenHeight = Screen.height;

    const float windowWidth = 800f;
    const float windowHeight = 640f;

    GUILayout.BeginArea(new Rect((screenWidth - windowWidth) / 2f, (screenHeight - windowHeight) / 2f, windowWidth,
      windowHeight));
    if (onDecline != null) _alertBox.Draw(new Vector2(800f, 640f), updatesText, onAccept, onDecline);
    GUILayout.EndArea();
  }
}

internal class UpdateBox(string title, string message, string continueText, string cancelText) : Bordered
{
  private const int BorderThickness = 2;
  private const int CornerRadius = 12;

  private readonly UpdateButton _acceptButton = new(continueText, Colors.Primary, Colors.PrimaryLight,
    Colors.PrimaryDark, Colors.PrimaryLight);

  private readonly UpdateButton _declineButton = new(
    cancelText,
    Colors.Secondary,
    Colors.SecondaryLight,
    Colors.SecondaryDark,
    Colors.SecondaryLight,
    "Enforced updates will still be downloaded."
  );

  private readonly UpdateButtonTooltip _updateButtonTooltip = new();
  private Vector2 _scrollPosition = Vector2.zero;

  public void Draw(Vector2 size, string updatesText, Action onAccept, Action onDecline)
  {
    Rect borderRect = GUILayoutUtility.GetRect(size.x, size.y);

    // Draw shadow for depth
    Utility.DrawShadow(borderRect, 0, 6, 12);

    // Draw rounded border
    DrawBorder(borderRect, BorderThickness, Colors.PrimaryLight, CornerRadius);

    Rect alertRect = new(
      borderRect.x + BorderThickness,
      borderRect.y + BorderThickness,
      borderRect.width - (2 * BorderThickness),
      borderRect.height - (2 * BorderThickness)
    );

    // Draw modern gradient background
    DrawGradientBox(alertRect, Colors.Dark.SetAlpha(0.85f), Colors.DarkMedium.SetAlpha(0.75f), false,
      CornerRadius - BorderThickness);

    Rect infoRect = new(alertRect.x, alertRect.y, alertRect.width, 96f);
    Rect scrollRect = new(alertRect.x, alertRect.y + 96f, alertRect.width, alertRect.height - 96f - 48f);
    Rect actionsRect = new(alertRect.x, alertRect.y + alertRect.height - 48f, alertRect.width, 48f);

    GUIStyle titleStyle = new()
    {
      alignment = TextAnchor.LowerCenter,
      fontSize = 32,
      fontStyle = FontStyle.Bold,
      normal = { textColor = Colors.White }
    };

    GUIStyle messageStyle = new()
    {
      alignment = TextAnchor.MiddleCenter,
      fontSize = 18,
      normal = { textColor = Colors.OffWhite }
    };

    GUIStyle scrollStyle = new()
    {
      alignment = TextAnchor.UpperLeft,
      fontSize = 16,
      normal = { textColor = Colors.OffWhite }
    };

    Rect titleRect = new(infoRect.x, infoRect.y, infoRect.width, infoRect.height / 2);
    GUI.Label(titleRect, title, titleStyle);

    Rect messageRect = new(infoRect.x, infoRect.y + (infoRect.height / 2f), infoRect.width, infoRect.height / 2);
    GUI.Label(messageRect, message, messageStyle);

    GUIStyle scrollbarStyle = new(GUI.skin.verticalScrollbar)
    {
      normal = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
      active = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
      hover = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) },
      focused = { background = Utility.GetTexture(Colors.Grey.SetAlpha(0.2f)) }
    };
    GUIStyle scrollbarThumbStyle = new(GUI.skin.verticalScrollbarThumb)
    {
      normal = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.66f)) },
      active = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.5f)) },
      hover = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.66f)) },
      focused = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.5f)) }
    };

    float scrollHeight = scrollStyle.CalcHeight(new GUIContent(updatesText), alertRect.width - 40f);
    GUI.DrawTexture(scrollRect, Utility.GetTexture(Color.black.SetAlpha(0.5f)), ScaleMode.StretchToFill, true, 0);

    GUISkin oldSkin = GUI.skin;
    GUI.skin.verticalScrollbarThumb = scrollbarThumbStyle;
    GUI.skin.label.wordWrap = true;
    _scrollPosition = GUI.BeginScrollView(
      scrollRect,
      _scrollPosition,
      new Rect(0f, 0f, alertRect.width, scrollHeight + 32f),
      false,
      true,
      GUIStyle.none,
      scrollbarStyle
    );
    GUI.skin = oldSkin;
    GUI.Label(new Rect(16f, 16f, alertRect.width - 56f, scrollHeight), updatesText, scrollStyle);
    GUI.EndScrollView();

    if (onDecline != null &&
        _declineButton.Draw(new Rect(actionsRect.x, actionsRect.y, actionsRect.width / 2, actionsRect.height)))
      onDecline();
    if (
      onAccept != null
      && _acceptButton.Draw(
        new Rect(
          actionsRect.x + (onDecline == null ? 0 : actionsRect.width / 2),
          actionsRect.y,
          onDecline == null ? actionsRect.width : actionsRect.width / 2,
          actionsRect.height
        )
      )
    )
      onAccept();

    Rect tooltipRect = new(Event.current.mousePosition.x + 2f, Event.current.mousePosition.y - 20f, 275f, 20f);
    _updateButtonTooltip.Draw(tooltipRect, GUI.tooltip);
  }
}

internal class UpdateButton(
  string text,
  Color normalColor,
  Color hoverColor,
  Color activeColor,
  Color borderColor,
  string tooltip = null) : Bordered
{
  private const int BorderThickness = 2;
  private const int CornerRadius = 6;
  private bool _active;
  private float _hoverTransition;

  public bool Draw(Rect borderRect)
  {
    Rect buttonRect = new(
      borderRect.x + BorderThickness,
      borderRect.y + BorderThickness,
      borderRect.width - (2 * BorderThickness),
      borderRect.height - (2 * BorderThickness)
    );

    bool hovered = buttonRect.Contains(Event.current.mousePosition);

    // Smooth hover transition
    float targetTransition = hovered ? 1f : 0f;
    _hoverTransition = Mathf.Lerp(_hoverTransition, targetTransition, Time.deltaTime * 10f);

    if (hovered && Event.current.type == EventType.MouseDown)
      _active = true;
    if (_active && Event.current.type == EventType.MouseUp)
      _active = false;

    // Modern color selection with smooth transitions
    Color buttonColor;
    if (_active)
      buttonColor = activeColor;
    else
      buttonColor = Color.Lerp(normalColor, hoverColor, _hoverTransition);

    Color textColor = Colors.White;

    // Draw subtle shadow
    if (!_active)
      Utility.DrawShadow(borderRect, 0, _active ? 1 : 2, _active ? 3 : 5);

    // Draw rounded border
    DrawBorder(borderRect, BorderThickness, borderColor, CornerRadius);

    // Draw gradient button background
    DrawGradientBox(buttonRect, buttonColor, buttonColor.SetAlpha(buttonColor.a * 0.85f), false,
      CornerRadius - BorderThickness);

    // Add highlight effect at top
    if (!_active)
    {
      Rect highlightRect = new(buttonRect.x, buttonRect.y, buttonRect.width, buttonRect.height * 0.4f);
      GUI.DrawTexture(highlightRect, Utility.GetTexture(Colors.Highlight), ScaleMode.StretchToFill, true);
    }

    return GUI.Button(
      buttonRect,
      new GUIContent(text, tooltip),
      new GUIStyle
      {
        fontSize = 20,
        fontStyle = FontStyle.Bold,
        alignment = TextAnchor.MiddleCenter,
        normal = { textColor = textColor }
      }
    );
  }
}

internal class UpdateButtonTooltip : Bordered
{
  private const int BorderThickness = 1;
  private const int CornerRadius = 4;

  public void Draw(Rect borderRect, string text)
  {
    if (text == string.Empty)
      return;

    // Draw subtle shadow for tooltip
    Utility.DrawShadow(borderRect, 0, 2, 4);

    DrawBorder(borderRect, BorderThickness, Colors.PrimaryLight, CornerRadius);

    Rect tooltipRect = new(
      borderRect.x + BorderThickness,
      borderRect.y + BorderThickness,
      borderRect.width - (2 * BorderThickness),
      borderRect.height - (2 * BorderThickness)
    );

    Rect labelRect = new(tooltipRect.x + 6f, tooltipRect.y, tooltipRect.width - 12f, tooltipRect.height);

    // Draw modern gradient background
    DrawRoundedBox(tooltipRect, Colors.Dark.SetAlpha(0.95f), CornerRadius - BorderThickness);

    GUI.Label(
      labelRect,
      text,
      new GUIStyle
      {
        fontSize = 14,
        alignment = TextAnchor.MiddleLeft,
        normal = { textColor = Colors.OffWhite }
      }
    );
  }
}
