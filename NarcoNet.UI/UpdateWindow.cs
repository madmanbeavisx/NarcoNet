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
        if (onDecline != null)
        {
            _alertBox.Draw(new Vector2(800f, 640f), updatesText, onAccept, onDecline);
        }

        GUILayout.EndArea();
    }
}

internal class UpdateBox(string title, string message, string continueText, string cancelText) : Bordered
{
    private const int BorderThickness = 3;
    private const int CornerRadius = 16;

    private readonly UpdateButton _acceptButton = new(continueText, Colors.Primary, Colors.PrimaryLight,
        Colors.PrimaryDark);

    private readonly UpdateButton _declineButton = new(
        cancelText,
        Colors.Secondary,
        Colors.SecondaryLight,
        Colors.SecondaryDark,
        "Enforced updates will still be downloaded."
    );

    private readonly UpdateButtonTooltip _updateButtonTooltip = new();
    private Vector2 _scrollPosition = Vector2.zero;

    public void Draw(Vector2 size, string updatesText, Action? onAccept, Action? onDecline)
    {
        Rect borderRect = GUILayoutUtility.GetRect(size.x, size.y);

        // Draw stronger shadow for more depth
        Utility.DrawShadow(borderRect, 0, 12, 24);

        // Draw a glowing border effect
        DrawBorder(borderRect, BorderThickness, Colors.Primary.SetAlpha(0.8f), CornerRadius);

        Rect alertRect = new(
            borderRect.x + BorderThickness,
            borderRect.y + BorderThickness,
            borderRect.width - 2 * BorderThickness,
            borderRect.height - 2 * BorderThickness
        );

        // Draw a modern gradient background with better opacity
        DrawGradientBox(alertRect, Colors.Dark.SetAlpha(0.95f), Colors.DarkMedium.SetAlpha(0.92f), false,
            CornerRadius - BorderThickness);

        Rect infoRect = new(alertRect.x, alertRect.y + 24f, alertRect.width, 120f);
        Rect scrollRect = new(alertRect.x + 16f, alertRect.y + 160f, alertRect.width - 32f, alertRect.height - 160f - 72f);
        Rect actionsRect = new(alertRect.x, alertRect.y + alertRect.height - 64f, alertRect.width, 64f);

        GUIStyle titleStyle = new()
        {
            alignment = TextAnchor.LowerCenter,
            fontSize = 38,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Colors.White }
        };

        GUIStyle messageStyle = new()
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 20,
            normal = { textColor = Colors.OffWhite }
        };

        GUIStyle scrollStyle = new()
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 18,
            normal = { textColor = Colors.OffWhite }
        };

        Rect titleRect = new(infoRect.x, infoRect.y, infoRect.width, infoRect.height / 2);
        GUI.Label(titleRect, title, titleStyle);

        Rect messageRect = new(infoRect.x, infoRect.y + infoRect.height / 2f, infoRect.width, infoRect.height / 2);
        GUI.Label(messageRect, message, messageStyle);

        GUIStyle scrollbarStyle = new(GUI.skin.verticalScrollbar)
        {
            normal = { background = Utility.GetTexture(Colors.GreyDark.SetAlpha(0.3f)) },
            active = { background = Utility.GetTexture(Colors.GreyDark.SetAlpha(0.3f)) },
            hover = { background = Utility.GetTexture(Colors.GreyDark.SetAlpha(0.3f)) },
            focused = { background = Utility.GetTexture(Colors.GreyDark.SetAlpha(0.3f)) }
        };
        GUIStyle scrollbarThumbStyle = new(GUI.skin.verticalScrollbarThumb)
        {
            normal = { background = Utility.GetTexture(Colors.Primary.SetAlpha(0.75f)) },
            active = { background = Utility.GetTexture(Colors.PrimaryDark.SetAlpha(0.85f)) },
            hover = { background = Utility.GetTexture(Colors.PrimaryLight.SetAlpha(0.85f)) },
            focused = { background = Utility.GetTexture(Colors.PrimaryDark.SetAlpha(0.85f)) }
        };

        float scrollHeight = scrollStyle.CalcHeight(new GUIContent(updatesText), alertRect.width - 72f);

        // Draw scroll area background with a subtle border
        GUI.DrawTexture(scrollRect, Utility.GetTexture(Color.black.SetAlpha(0.6f)), ScaleMode.StretchToFill, true, 0);

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
        GUI.Label(new Rect(20f, 20f, alertRect.width - 88f, scrollHeight), updatesText, scrollStyle);
        GUI.EndScrollView();

        // Add padding between buttons
        var buttonPadding = 12f;
        var buttonMargin = 16f;

        if (onDecline != null &&
            _declineButton.Draw(new Rect(
                actionsRect.x + buttonMargin,
                actionsRect.y + 8f,
                (actionsRect.width - buttonPadding - 2 * buttonMargin) / 2,
                actionsRect.height - 16f)))
        {
            onDecline();
        }

        if (
            onAccept != null
            && _acceptButton.Draw(
                new Rect(
                    actionsRect.x + (onDecline == null ? buttonMargin : (actionsRect.width + buttonPadding) / 2),
                    actionsRect.y + 8f,
                    onDecline == null ? actionsRect.width - 2 * buttonMargin : (actionsRect.width - buttonPadding - 2 * buttonMargin) / 2,
                    actionsRect.height - 16f
                )
            )
        )
        {
            onAccept();
        }

        Rect tooltipRect = new(Event.current.mousePosition.x + 2f, Event.current.mousePosition.y - 20f, 275f, 20f);
        _updateButtonTooltip.Draw(tooltipRect, GUI.tooltip);
    }
}

internal class UpdateButton(
    string text,
    Color normalColor,
    Color hoverColor,
    Color activeColor,
    string? tooltip = null) : Bordered
{
    private const int CornerRadius = 10;
    private bool _active;
    private float _hoverTransition;

    public bool Draw(Rect borderRect)
    {
        Rect buttonRect = borderRect;

        bool hovered = buttonRect.Contains(Event.current.mousePosition);

        // Smooth hover transition
        float targetTransition = hovered ? 1f : 0f;
        _hoverTransition = Mathf.Lerp(_hoverTransition, targetTransition, Time.deltaTime * 12f);

        if (hovered && Event.current.type == EventType.MouseDown)
        {
            _active = true;
        }

        if (_active && Event.current.type == EventType.MouseUp)
        {
            _active = false;
        }

        // Modern color selection with smooth transitions
        Color buttonColor = _active ? activeColor : Color.Lerp(normalColor, hoverColor, _hoverTransition);

        Color textColor = Colors.White;

        // Draw modern shadow with elevation
        int shadowOffset = _active ? 2 : 6;
        int shadowBlur = _active ? 8 : 16;
        Utility.DrawShadow(borderRect, 0, shadowOffset, shadowBlur);

        // Draw sleek rounded button background with subtle gradient
        DrawGradientBox(buttonRect, buttonColor, buttonColor.SetAlpha(buttonColor.a * 0.9f), false, CornerRadius);

        // Add highlight effect at top for depth
        if (!_active)
        {
            Rect highlightRect = new(buttonRect.x + 4, buttonRect.y + 4, buttonRect.width - 8, buttonRect.height * 0.3f);
            GUI.DrawTexture(highlightRect, Utility.GetTexture(Colors.Highlight), ScaleMode.StretchToFill, true);
        }

        // Add a glow effect on hover
        if (!(_hoverTransition > 0.1f))
            return GUI.Button(
                buttonRect,
                new GUIContent(text, tooltip),
                new GUIStyle
                {
                    fontSize = 22,
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter,
                    normal =
                    {
                        textColor = textColor
                    }
                }
            );
        Color glowColor = Colors.Primary.SetAlpha(0.2f * _hoverTransition);
        Rect glowRect = new(buttonRect.x - 2, buttonRect.y - 2, buttonRect.width + 4, buttonRect.height + 4);
        DrawRoundedBox(glowRect, glowColor, CornerRadius + 2);

        return GUI.Button(
            buttonRect,
            new GUIContent(text, tooltip),
            new GUIStyle
            {
                fontSize = 22,
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
        {
            return;
        }

        // Draw subtle shadow for tooltip
        Utility.DrawShadow(borderRect, 0, 2, 4);

        DrawBorder(borderRect, BorderThickness, Colors.PrimaryLight, CornerRadius);

        Rect tooltipRect = new(
            borderRect.x + BorderThickness,
            borderRect.y + BorderThickness,
            borderRect.width - 2 * BorderThickness,
            borderRect.height - 2 * BorderThickness
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
