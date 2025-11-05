using UnityEngine;

namespace NarcoNet.UI;

public class Bordered
{
    protected static void DrawBorder(Rect rect, int thickness, Color color, int cornerRadius = 0)
    {
        if (cornerRadius > 0)
        {
            DrawRoundedBorder(rect, thickness, color, cornerRadius);
        }
        else
        {
            Texture2D borderTexture = Utility.GetTexture(color);
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, rect.width, thickness), borderTexture); // Top
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMax - thickness, rect.width, thickness), borderTexture); // Bottom
            GUI.DrawTexture(new Rect(rect.xMin, rect.yMin, thickness, rect.height), borderTexture); // Left
            GUI.DrawTexture(new Rect(rect.xMax - thickness, rect.yMin, thickness, rect.height), borderTexture); // Right
        }
    }

    protected static void DrawRoundedBorder(Rect rect, int thickness, Color color, int cornerRadius)
    {
        // Draw rounded border by drawing a larger rounded rect and then drawing a smaller one on top
        int width = (int)rect.width;
        int height = (int)rect.height;

        Texture2D outerTexture = Utility.GetRoundedTexture(width, height, cornerRadius, color);
        Texture2D innerTexture = Utility.GetRoundedTexture(
            width - thickness * 2,
            height - thickness * 2,
            Mathf.Max(0, cornerRadius - thickness),
            new Color(0, 0, 0, 0)
        );

        GUI.DrawTexture(rect, outerTexture, ScaleMode.StretchToFill, true);
        GUI.DrawTexture(
            new Rect(rect.x + thickness, rect.y + thickness, rect.width - thickness * 2, rect.height - thickness * 2),
            innerTexture,
            ScaleMode.StretchToFill,
            true
        );
    }

    protected static void DrawRoundedBox(Rect rect, Color color, int cornerRadius = 8, bool withShadow = false)
    {
        if (withShadow)
        {
            Utility.DrawShadow(rect, 0, 3, 6);
        }

        Texture2D texture = Utility.GetRoundedTexture((int)rect.width, (int)rect.height, cornerRadius, color);
        GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, true);
    }

    protected static void DrawGradientBox(Rect rect, Color colorStart, Color colorEnd, bool horizontal = false,
        int cornerRadius = 0, bool withShadow = false)
    {
        if (withShadow)
        {
            Utility.DrawShadow(rect, 0, 3, 6);
        }

        if (cornerRadius > 0)
        {
            // For rounded corners with gradient, we'll use a simple approach
            // Create gradient then mask it with rounded shape
            Texture2D gradientTexture = Utility.GetGradientTexture(
                colorStart,
                colorEnd,
                horizontal ? (int)rect.width : 1,
                horizontal ? 1 : (int)rect.height,
                horizontal
            );
            // Note: This is a simplified approach. For perfect rounded gradients,
            // you'd need to generate a rounded texture with gradient applied
            GUI.DrawTexture(rect, gradientTexture, ScaleMode.StretchToFill, true);
        }
        else
        {
            Texture2D gradientTexture = Utility.GetGradientTexture(
                colorStart,
                colorEnd,
                horizontal ? (int)rect.width : 1,
                horizontal ? 1 : (int)rect.height,
                horizontal
            );
            GUI.DrawTexture(rect, gradientTexture, ScaleMode.StretchToFill, true);
        }
    }
}
