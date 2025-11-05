using NarcoNet.Utilities;

using UnityEngine;

using Vector2 = UnityEngine.Vector2;

namespace NarcoNet.UI;

public class AlertWindow(Vector2 size, string title, string message, string buttonText = "EXIT GAME")
{
    private readonly AlertButton _alertButton = new(buttonText);
    private readonly InfoBox _infoBox = new(title, message);
    public bool Active { get; private set; }

    public void Show()
    {
        Active = true;
    }

    public void Hide()
    {
        Active = false;
    }

    public void Draw(Action restartAction)
    {
        float screenWidth = Screen.width;
        float screenHeight = Screen.height;

        const float windowWidth = 640f;
        const float windowHeight = 640f;

        GUILayout.BeginArea(new Rect((screenWidth - windowWidth) / 2f, (screenHeight - windowHeight) / 2f, windowWidth,
            windowHeight));
        GUILayout.BeginVertical();
        GUILayout.FlexibleSpace();

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        _infoBox.Draw(size);
        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.Space(64f);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        if (_alertButton.Draw(new Vector2(196f, 48f)))
        {
            restartAction();
        }

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.FlexibleSpace();
        GUILayout.EndVertical();
        GUILayout.EndArea();
    }
}

internal class AlertButton(string text) : Bordered
{
    private const int BorderThickness = 2;
    private const int CornerRadius = 8;

    private bool _active;
    private float _hoverTransition;

    public bool Draw(Vector2 size)
    {
        Rect borderRect = GUILayoutUtility.GetRect(size.x, size.y);

        Rect buttonRect = new(
            borderRect.x + BorderThickness,
            borderRect.y + BorderThickness,
            borderRect.width - 2 * BorderThickness,
            borderRect.height - 2 * BorderThickness
        );

        bool hovered = buttonRect.Contains(Event.current.mousePosition);

        // Smooth hover transition
        float targetTransition = hovered ? 1f : 0f;
        _hoverTransition = Mathf.Lerp(_hoverTransition, targetTransition, Time.deltaTime * 10f);

        if (hovered && Event.current.type == EventType.MouseDown)
        {
            _active = true;
        }

        if (_active && Event.current.type == EventType.MouseUp)
        {
            _active = false;
        }

        // Modern color selection with smooth transitions
        Color buttonColor;
        if (_active)
        {
            buttonColor = Colors.PrimaryDark;
        }
        else
        {
            buttonColor = Color.Lerp(Colors.Primary, Colors.PrimaryLight, _hoverTransition);
        }

        Color textColor = _active ? Colors.White : Colors.White;

        // Draw shadow for depth
        if (!_active)
        {
            Utility.DrawShadow(borderRect, 0, _active ? 1 : 3, _active ? 4 : 6);
        }

        // Draw rounded border with gradient
        DrawBorder(borderRect, BorderThickness, Colors.PrimaryLight, CornerRadius);

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
            new GUIContent(text),
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
